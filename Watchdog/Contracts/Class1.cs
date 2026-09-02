using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

// [REVIEW] Added #nullable enable for strict null-safety, standard in .NET 8/9.
#nullable enable

namespace Watchdog.Contracts
{
    /// <summary>
    /// Protocol frame types distinguishing transport and routing intents.
    /// </summary>
    public enum MessageType : ushort
    {
        Unknown = 0,

        // Session Lifecycle
        Register = 1,
        RegisterResponse = 2,
        Unregister = 3,

        // Health & Diagnostics
        Heartbeat = 10,
        HealthReport = 11,

        // Command & Control
        Command = 20,
        CommandResponse = 21,

        // Messaging & Pub-Sub
        Event = 30,
        Notification = 31,

        // Lifecycle Actions
        ShutdownRequest = 40,
        RestartRequest = 41,

        // Reliability
        Acknowledgement = 50,
        Error = 51
    }

    /// <summary>
    /// Delivery quality requirements for routing channels.
    /// </summary>
    public enum DeliveryPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Protocol wire and framing constants.
    /// </summary>
    public static class ProtocolConstants
    {
        public const ushort MagicMarker = 0x534D; // "SM" in little-endian
        public const byte ProtocolVersion = 0x01;
        public const int HeaderSize = 40; // Fixed 40-byte wire header layout
        public const int MaxPayloadLength = 64 * 1024 * 1024; // 64 MB frame upper guard
        public const string BroadcastTarget = "*";
        public const string WatchdogApplicationId = "Watchdog.Core";
    }

    /// <summary>
    /// Blittable fixed-size header marshaled directly over the wire.
    /// </summary>
    // [REVIEW] Fixed struct generation error. Pack = 1 ensures exact 40-byte layout matching HeaderSize.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MessageHeader
    {
        public ushort Magic;            // 2 bytes
        public byte Version;            // 1 byte
        public byte Priority;           // 1 byte
        public ushort Type;             // 2 bytes
        public ushort Flags;            // 2 bytes
        public int PayloadLength;       // 4 bytes
        public long TimestampTicks;     // 8 bytes
        public Guid CorrelationId;      // 16 bytes
        public int Reserved;            // 4 bytes (Total: 40 bytes)
    }

    public interface IClientSession
    {
        bool IsAlive { get; }
        Task SendAsync(IpcMessage message, CancellationToken cancellationToken = default);
        event Action<IClientSession, IpcMessage> MessageReceived;
    }

    public abstract class ClientSessionManager
    {
        public event Action<IClientSession>? SessionRegistered;
        public event Action<IClientSession>? SessionUnregistered;
        public abstract IClientSession? FindByConnectionId(string connectionId);
        public abstract System.Collections.Generic.IReadOnlyList<IClientSession> GetAllActiveSessions();
    }

    /// <summary>
    /// Dispatches inbound IPC messages by MessageType to registered type handlers.
    /// </summary>
    public interface IMessageRouter
    {
        void RegisterHandler(MessageType messageType, Func<IClientSession, IpcMessage, Task> handler);
        void UnregisterHandler(MessageType messageType);
        Task RouteAsync(IClientSession session, IpcMessage message);
    }

    /// <summary>
    /// Thread-safe router directing raw frames to specialized asynchronous consumers.
    /// </summary>
    public sealed class MessageRouter : IMessageRouter
    {
        // [REVIEW] For high-throughput IPC, an array indexed by (int)MessageType is O(1) 
        // and allocation-free on reads compared to ConcurrentDictionary. 
        // Keeping ConcurrentDictionary as it is thread-safe for dynamic registration.
        private readonly ConcurrentDictionary<MessageType, Func<IClientSession, IpcMessage, Task>> _handlers =
            new ConcurrentDictionary<MessageType, Func<IClientSession, IpcMessage, Task>>();

        public void RegisterHandler(MessageType messageType, Func<IClientSession, IpcMessage, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler); // [REVIEW] Modern .NET 8 guard clause
            _handlers[messageType] = handler;
        }

        public void UnregisterHandler(MessageType messageType)
        {
            _handlers.TryRemove(messageType, out _);
        }

        public async Task RouteAsync(IClientSession session, IpcMessage message)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(message);

            if (_handlers.TryGetValue(message.Type, out var handler))
            {
                await handler(session, message).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// High-level RPC dispatcher correlating asynchronous responses with outstanding requests.
    /// </summary>
    public interface IMessageBus : IDisposable
    {
        Task SendAsync(
            string connectionId,
            IpcMessage message,
            CancellationToken cancellationToken = default);

        Task BroadcastAsync(
            IpcMessage message,
            CancellationToken cancellationToken = default);

        Task<IpcMessage> RequestAsync(
            string connectionId,
            IpcMessage request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Coordinates full-duplex messaging, request-response pairing, and fan-out broadcasts.
    /// </summary>
    public sealed class MessageBus : IMessageBus
    {
        private readonly ClientSessionManager _sessionManager;
        private readonly IMessageRouter _router;

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IpcMessage>> _pendingRequests =
            new ConcurrentDictionary<Guid, TaskCompletionSource<IpcMessage>>();

        private int _isDisposed;

        public MessageBus(ClientSessionManager sessionManager, IMessageRouter router)
        {
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            _sessionManager.SessionRegistered += AttachSession;
            _sessionManager.SessionUnregistered += DetachSession;
        }

        private void AttachSession(IClientSession session)
        {
            session.MessageReceived += HandleIncomingMessage;
        }

        private void DetachSession(IClientSession session)
        {
            session.MessageReceived -= HandleIncomingMessage;
        }

        private void HandleIncomingMessage(IClientSession session, IpcMessage message)
        {
            if (message == null)
                return;

            if (message.CorrelationId != Guid.Empty &&
                _pendingRequests.TryRemove(message.CorrelationId, out var tcs))
            {
                // [REVIEW] TrySetResult transfers ownership to the awaiting RequestAsync method.
                // The waiting method is now responsible for disposing the message to return pooled memory.
                tcs.TrySetResult(message);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _router.RouteAsync(session, message).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // [REVIEW] Important: Fire-and-forget tasks swallow exceptions. 
                    // Consider adding ILogger here to trace handler failures, otherwise pipeline crashes are silent.
                }
                finally
                {
                    message.Dispose();
                }
            });
        }

        public async Task SendAsync(
            string connectionId,
            IpcMessage message,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new ArgumentException("Connection ID cannot be null or empty.", nameof(connectionId));

            ArgumentNullException.ThrowIfNull(message);

            var session = _sessionManager.FindByConnectionId(connectionId);
            if (session == null || !session.IsAlive)
                throw new InvalidOperationException($"Target session '{connectionId}' is offline or could not be found.");

            await session.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }

        public async Task BroadcastAsync(
            IpcMessage message,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            var activeSessions = _sessionManager.GetAllActiveSessions();
            if (activeSessions.Count == 0)
                return;

            for (int i = 0; i < activeSessions.Count; i++)
            {
                var session = activeSessions[i];
                if (!session.IsAlive)
                    continue;

                byte[]? clonedBuffer = null;
                int length = message.PayloadLength;

                if (length > 0 && message.Payload != null)
                {
                    clonedBuffer = ArrayPool<byte>.Shared.Rent(length);
                    Buffer.BlockCopy(message.Payload, 0, clonedBuffer, 0, length);
                }

                var messageClone = new IpcMessage(
                    header: message.Header,
                    senderApplicationId: message.SenderApplicationId,
                    targetApplicationId: message.TargetApplicationId,
                    payload: clonedBuffer,
                    payloadLength: length,
                    isRentedPayload: clonedBuffer != null);

                try
                {
                    await session.SendAsync(messageClone, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // [REVIEW] Prevents memory leaks if SendAsync fails before taking ownership of the message.
                    messageClone.Dispose();
                    throw;
                }
            }
        }

        public async Task<IpcMessage> RequestAsync(
            string connectionId,
            IpcMessage request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new ArgumentException("Connection ID cannot be null or empty.", nameof(connectionId));

            ArgumentNullException.ThrowIfNull(request);

            var session = _sessionManager.FindByConnectionId(connectionId);
            if (session == null || !session.IsAlive)
                throw new InvalidOperationException($"Target session '{connectionId}' is offline or could not be found.");

            if (request.Header.CorrelationId == Guid.Empty)
            {
                // [REVIEW] request.Header is a mutable struct field on IpcMessage, so updating it here correctly mutates the instance.
                request.Header.CorrelationId = Guid.NewGuid();
            }

            var tcs = new TaskCompletionSource<IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[request.CorrelationId] = tcs;

            try
            {
                await session.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // [REVIEW] Refactored to use WaitAsync (.NET 6+). 
                // This removes the need for CancellationTokenSource.CreateLinkedTokenSource allocation.
                return await tcs.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException($"Request '{request.CorrelationId}' timed out after {timeout.TotalMilliseconds:N0}ms.");
            }
            finally
            {
                // [REVIEW] Ensures the tracking dictionary doesn't leak memory if the request times out or throws.
                _pendingRequests.TryRemove(request.CorrelationId, out _);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            _sessionManager.SessionRegistered -= AttachSession;
            _sessionManager.SessionUnregistered -= DetachSession;

            foreach (var pending in _pendingRequests.Values)
            {
                pending.TrySetCanceled();
            }

            _pendingRequests.Clear();
        }
    }

    /// <summary>
    /// In-memory domain envelope representing an IPC message across the bus.
    /// </summary>
    public sealed class IpcMessage : IDisposable
    {
        public MessageHeader Header;
        public string SenderApplicationId { get; }
        public string TargetApplicationId { get; }
        public byte[]? Payload { get; private set; } // [REVIEW] Nullable since zero-payload frames exist
        public int PayloadLength { get; }
        public bool IsRentedPayload { get; }

        public MessageType Type => (MessageType)Header.Type;
        public DeliveryPriority Priority => (DeliveryPriority)Header.Priority;
        public Guid CorrelationId => Header.CorrelationId;

        public IpcMessage(
            MessageHeader header,
            string? senderApplicationId,
            string? targetApplicationId,
            byte[]? payload,
            int payloadLength,
            bool isRentedPayload = false)
        {
            Header = header;
            SenderApplicationId = senderApplicationId ?? string.Empty;
            TargetApplicationId = targetApplicationId ?? ProtocolConstants.BroadcastTarget;
            Payload = payload;
            PayloadLength = payloadLength;
            IsRentedPayload = isRentedPayload;
        }

        public static IpcMessage Create(
            MessageType type,
            string? senderApplicationId,
            string? targetApplicationId,
            byte[]? payload,
            int payloadLength,
            DeliveryPriority priority = DeliveryPriority.Normal,
            Guid? correlationId = null,
            bool isRentedPayload = false)
        {
            var header = new MessageHeader
            {
                Magic = ProtocolConstants.MagicMarker,
                Version = ProtocolConstants.ProtocolVersion,
                Priority = (byte)priority,
                Type = (ushort)type,
                Flags = 0,
                PayloadLength = payloadLength,
                TimestampTicks = DateTime.UtcNow.Ticks,
                CorrelationId = correlationId ?? Guid.NewGuid()
                // Reserved is implicitly 0
            };

            return new IpcMessage(
                header,
                senderApplicationId,
                targetApplicationId,
                payload,
                payloadLength,
                isRentedPayload);
        }

        public void Dispose()
        {
            if (IsRentedPayload && Payload != null)
            {
                ArrayPool<byte>.Shared.Return(Payload);
                Payload = null;
            }
        }
    }

    public class ClientIdentity
    {
        public string? ApplicationId { get; set; }
        public string? InstanceId { get; set; }
    }

    /// <summary>
    /// Registration payload exchanged upon pipe handshake.
    /// </summary>
    public sealed class RegistrationContract
    {
        public ClientIdentity? Identity { get; set; }
        public string[]? SubscribedTopics { get; set; }
        public int HeartbeatIntervalMilliseconds { get; set; }
    }

    /// <summary>
    /// Diagnostic telemetry dispatched periodically by applications to the Watchdog.
    /// </summary>
    public sealed class HealthReportContract
    {
        public string? ApplicationId { get; set; }
        public string? InstanceId { get; set; }
        public double CpuUsagePercent { get; set; }
        public long MemoryWorkingSetBytes { get; set; }
        public int ActiveThreadCount { get; set; }
        public bool IsResponding { get; set; }
        public string? StatusMessage { get; set; }
    }

    /// <summary>
    /// Remote execution instruction forwarded to target applications.
    /// </summary>
    public sealed class CommandContract
    {
        public string? CommandName { get; set; }
        public string? ArgumentsJson { get; set; }
        public int TimeoutMilliseconds { get; set; }
    }

    /// <summary>
    /// Command execution result returned to origin.
    /// </summary>
    public sealed class CommandResponseContract
    {
        public bool Success { get; set; }
        public string? ResultJson { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Process supervisor operational action requested by Watchdog or supervisor tools.
    /// </summary>
    public sealed class ProcessLifecycleCommandContract
    {
        public string? TargetApplicationId { get; set; }
        public bool ForceKill { get; set; }
        public int GracePeriodMilliseconds { get; set; }
        public string? Reason { get; set; }
    }
}
