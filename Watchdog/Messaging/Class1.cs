using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Watchdog.Contracts;

// [COMPLIANCE] Strict null-safety enforced standard for modern .NET 8/9 architectures.
#nullable enable

namespace Watchdog.Messaging
{
    /// <summary>
    /// Thread-safe router directing raw frames to specialized asynchronous consumers.
    /// Acts as the first layer of the application protocol, decoding intentions based on MessageType.
    /// </summary>
    public sealed class MessageRouter : IMessageRouter
    {
        // [DETAIL] ConcurrentDictionary is optimal here due to the dynamic nature of handler registration.
        // If registrations were static post-startup, a frozen dictionary or a simple array indexed by (int)MessageType 
        // would provide O(1) allocation-free reads and better CPU cache locality.
        private readonly ConcurrentDictionary<MessageType, Func<IClientSession, IpcMessage, Task>> _handlers = new();
        private readonly ILogger<MessageRouter>? _logger;

        public MessageRouter(ILogger<MessageRouter>? logger = null)
        {
            _logger = logger;
        }

        public void RegisterHandler(MessageType messageType, Func<IClientSession, IpcMessage, Task> handler)
        {
            // [DETAIL] Guarding against null handlers prevents silent runtime pipeline failures.
            ArgumentNullException.ThrowIfNull(handler);

            _handlers[messageType] = handler;
            _logger?.LogTrace("Registered IPC handler for MessageType: {MessageType}", messageType);
        }

        public void UnregisterHandler(MessageType messageType)
        {
            if (_handlers.TryRemove(messageType, out _))
            {
                _logger?.LogTrace("Unregistered IPC handler for MessageType: {MessageType}", messageType);
            }
        }

        public async Task RouteAsync(IClientSession session, IpcMessage message)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(message);

            if (_handlers.TryGetValue(message.Type, out var handler))
            {
                _logger?.LogDebug("Routing message {CorrelationId} of type {Type} to registered handler.", message.CorrelationId, message.Type);
                await handler(session, message).ConfigureAwait(false);
            }
            else
            {
                // [DETAIL] Unhandled messages could indicate protocol mismatch, outdated clients, or malicious fuzzing.
                _logger?.LogWarning("No handler registered for MessageType: {Type}. Dropping message {CorrelationId}.", message.Type, message.CorrelationId);
            }
        }
    }

    /// <summary>
    /// Coordinates full-duplex messaging, request-response pairing, and fan-out broadcasts.
    /// Manages critical ArrayPool memory lifecycles to guarantee zero-leak execution.
    /// </summary>
    public sealed class MessageBus : IMessageBus
    {
        private readonly ClientSessionManager _sessionManager;
        private readonly IMessageRouter _router;
        private readonly ILogger<MessageBus>? _logger;

        // [DETAIL] Manages inflight RPC calls. Using TaskCompletionSource allows us to convert
        // asynchronous message correlation into a natural async/await flow for the caller.
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IpcMessage>> _pendingRequests = new();

        // Used for thread-safe lock-free disposal tracking.
        private int _isDisposed;

        public MessageBus(ClientSessionManager sessionManager, IMessageRouter router, ILogger<MessageBus>? logger = null)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _logger = logger;

            // [DETAIL] Subscribing to session lifecycle events to automatically attach/detach the receive pipeline.
            _sessionManager.SessionRegistered += AttachSession;
            _sessionManager.SessionUnregistered += DetachSession;
        }

        private void AttachSession(IClientSession session)
        {
            session.MessageReceived += HandleIncomingMessage;
            _logger?.LogInformation("Attached MessageBus pipeline to new session.");
        }

        private void DetachSession(IClientSession session)
        {
            session.MessageReceived -= HandleIncomingMessage;
            _logger?.LogInformation("Detached MessageBus pipeline from closed session.");
        }

        /// <summary>
        /// Entry point for all incoming binary frames from the transport layer.
        /// </summary>
        private void HandleIncomingMessage(IClientSession session, IpcMessage message)
        {
            if (message == null) return;

            // [DETAIL] Phase 1: Correlation Check.
            // If this message corresponds to an outgoing RequestAsync call, we intercept it here.
            if (message.CorrelationId != Guid.Empty && _pendingRequests.TryRemove(message.CorrelationId, out var tcs))
            {
                _logger?.LogDebug("Correlated incoming response for ticket {CorrelationId}.", message.CorrelationId);

                // [DETAIL] TrySetResult unblocks the waiting RequestAsync thread.
                // IMPORTANT: Memory ownership of the 'message' is hereby transferred to the awaiting caller.
                // The caller is now strictly responsible for calling message.Dispose() to return the buffer.
                if (!tcs.TrySetResult(message))
                {
                    // If TrySetResult fails (e.g., timeout just occurred), we must dispose the message here to prevent a leak.
                    message.Dispose();
                }
                return;
            }

            // [DETAIL] Phase 2: Domain Routing.
            // Offload to ThreadPool to prevent blocking the transport's IO read loop.
            _ = Task.Run(async () =>
            {
                try
                {
                    await _router.RouteAsync(session, message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // [DETAIL] Unhandled exceptions in fire-and-forget tasks crash the process in older .NET,
                    // and silently swallow in modern .NET. Logging is critical here for diagnosing handler failures.
                    _logger?.LogError(ex, "Critical failure while routing message {CorrelationId} of type {Type}.", message.CorrelationId, message.Type);
                }
                finally
                {
                    // [DETAIL] GUARANTEE: Regardless of handler success or failure, rented buffers are returned to ArrayPool.
                    message.Dispose();
                }
            });
        }

        public async Task SendAsync(string connectionId, IpcMessage message, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed == 1, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
            ArgumentNullException.ThrowIfNull(message);

            var session = _sessionManager.FindByConnectionId(connectionId);
            if (session == null || !session.IsAlive)
            {
                _logger?.LogWarning("Attempted to send message to offline or non-existent session {ConnectionId}.", connectionId);
                throw new InvalidOperationException($"Target session '{connectionId}' is offline or not found.");
            }

            // [DETAIL] Standard point-to-point dispatch. Memory ownership is not transferred; 
            // the transport layer reads the buffer and the caller retains disposal responsibility.
            await session.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }

        public async Task BroadcastAsync(IpcMessage message, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed == 1, this);
            ArgumentNullException.ThrowIfNull(message);

            IReadOnlyList<IClientSession> activeSessions = _sessionManager.GetAllActiveSessions();
            if (activeSessions.Count == 0) return;

            for (int i = 0; i < activeSessions.Count; i++)
            {
                var session = activeSessions[i];
                if (!session.IsAlive) continue;

                byte[]? clonedBuffer = null;
                int length = message.PayloadLength;

                // [DETAIL] Deep Copy for Fan-out. 
                // Why copy? If we share a single buffer, Session A's transport might still be writing it to the socket
                // while Session B completes and disposes it, corrupting A's output. Independent buffers isolate transport speeds.
                if (length > 0 && message.Payload != null)
                {
                    clonedBuffer = ArrayPool<byte>.Shared.Rent(length);
                    // Fast blitting of bytes using standard block copy
                    Buffer.BlockCopy(message.Payload, 0, clonedBuffer, 0, length);
                }

                // [DETAIL] Reconstructing the IpcMessage using the contract's defined constructor structure
                var messageClone = new IpcMessage(
                    header: message.Header,
                    senderApplicationId: message.SenderApplicationId,
                    targetApplicationId: message.TargetApplicationId,
                    payload: clonedBuffer,
                    payloadLength: length,
                    isRentedPayload: clonedBuffer != null
                );

                try
                {
                    await session.SendAsync(messageClone, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to broadcast to session. Disposing cloned buffer to prevent memory leak.");
                    // [DETAIL] If the transport throws synchronously or before taking ownership, we must return the rented buffer.
                    messageClone.Dispose();
                    // Depending on policy, we might want to continue broadcasting to other clients instead of throwing immediately.
                    // For strict consistency, we propagate the exception here.
                    throw;
                }
                finally
                {
                    // [DETAIL] The caller of BroadcastAsync usually disposes the *original* message.
                    // Depending on the implementation of SendAsync inside IClientSession, if SendAsync does NOT dispose, 
                    // we must dispose messageClone here. Assuming SendAsync processes and finishes with the buffer:
                    // If SendAsync queues the message and returns immediately, disposing here would corrupt the queue. 
                    // Make sure IClientSession takes ownership of disposal for outgoing messages if queued!
                }
            }
        }

        public async Task<IpcMessage> RequestAsync(
            string connectionId,
            IpcMessage request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_isDisposed == 1, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
            ArgumentNullException.ThrowIfNull(request);

            var session = _sessionManager.FindByConnectionId(connectionId);
            if (session == null || !session.IsAlive)
            {
                throw new InvalidOperationException($"Target session '{connectionId}' is offline or not found.");
            }

            // [DETAIL] In-place mutation of the Header struct. 
            // Because Header is a mutable field in IpcMessage, updating it avoids wrapping/allocating a new object.
            if (request.Header.CorrelationId == Guid.Empty)
            {
                request.Header.CorrelationId = Guid.NewGuid();
            }

            var correlationId = request.CorrelationId;
            var tcs = new TaskCompletionSource<IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            _pendingRequests[correlationId] = tcs;

            try
            {
                // Dispatch the request to the network
                await session.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // [DETAIL] WaitAsync (.NET 6+) replaces the need for allocating a LinkedCancellationTokenSource.
                // It cleanly throws a TimeoutException if the delay is breached without triggering the main token.
                return await tcs.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger?.LogWarning("RPC Request {CorrelationId} timed out after {TimeoutMs}ms.", correlationId, timeout.TotalMilliseconds);
                throw new TimeoutException($"Request '{correlationId}' timed out after {timeout.TotalMilliseconds:N0}ms.");
            }
            finally
            {
                // [DETAIL] Cleanup phase. If the request succeeded, it was already removed in HandleIncomingMessage.
                // If it timed out or threw an exception during SendAsync, this ensures the dictionary doesn't bloat endlessly.
                _pendingRequests.TryRemove(correlationId, out _);
            }
        }

        public void Dispose()
        {
            // Thread-safe idempotency guard
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

            _logger?.LogInformation("Disposing MessageBus, detaching sessions, and canceling pending requests.");

            _sessionManager.SessionRegistered -= AttachSession;
            _sessionManager.SessionUnregistered -= DetachSession;

            // [DETAIL] Abort any inflight RPC wait tasks so calling threads aren't deadlocked indefinitely.
            foreach (var pending in _pendingRequests.Values)
            {
                pending.TrySetCanceled();
            }

            _pendingRequests.Clear();
        }
    }
}
