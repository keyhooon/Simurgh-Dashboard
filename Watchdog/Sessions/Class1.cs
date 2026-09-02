using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Watchdog.Contracts;
using Watchdog.Protocols;
using static MessageDecoder;

// [COMPLIANCE] Strict null-safety enforced standard for modern .NET 8/9 architectures.
#nullable enable

namespace Watchdog.Sessions
{
    /// <summary>
    /// Lifecycle events and status contract for an active client connection session.
    /// </summary>
    public interface IClientSession : IDisposable
    {
        string ConnectionId { get; }
        ClientIdentity? Identity { get; }
        bool IsAlive { get; }
        DateTime LastHeartbeatUtc { get; }

        event Action<IClientSession, IpcMessage>? MessageReceived;
        event Action<IClientSession>? SessionDisconnected;

        Task StartAsync(CancellationToken cancellationToken);
        Task SendAsync(IpcMessage message, CancellationToken cancellationToken);
        void SetIdentity(ClientIdentity identity);
        void RecordHeartbeat();
        Task TerminateAsync();
    }

    /// <summary>
    /// Implements continuous frame reading, encoding, decoupled egress queuing, 
    /// and lifecycle monitoring for a single connected client application.
    /// </summary>
    public sealed class ClientSession : IClientSession
    {
        private readonly IClientConnection _connection;
        private readonly IMessageEncoder _encoder;
        private readonly IMessageDecoder _decoder;
        private readonly ILogger<ClientSession>? _logger;

        // [DETAIL] Channels provide an optimal lock-free bounded queue for applying backpressure 
        // to outbound publishers if the transport egress speed falls behind ingress volume.
        private readonly Channel<IpcMessage> _outgoingChannel;
        private readonly CancellationTokenSource _sessionCts = new();

        // [DETAIL] Linked CTS prevents a classic memory leak where local CancellationToken registrations 
        // remain permanently pinned to a long-lived parent application token.
        private CancellationTokenSource? _linkedCts;

        private Task? _receiveLoopTask;
        private Task? _sendLoopTask;
        private int _isDisposed;
        private long _lastHeartbeatTicks;

        public string ConnectionId => _connection.ConnectionId;
        public ClientIdentity? Identity { get; private set; }

        // [DETAIL] Safe volatility check using Interlocked against the disposal state.
        public bool IsAlive => Volatile.Read(ref _isDisposed) == 0 && _connection.IsConnected;

        public DateTime LastHeartbeatUtc => new DateTime(
            Interlocked.Read(ref _lastHeartbeatTicks),
            DateTimeKind.Utc);

        public event Action<IClientSession, IpcMessage>? MessageReceived;
        public event Action<IClientSession>? SessionDisconnected;

        public ClientSession(
            IClientConnection connection,
            IMessageEncoder encoder,
            IMessageDecoder decoder,
            ILogger<ClientSession>? logger = null,
            int outgoingQueueCapacity = 2048)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentNullException.ThrowIfNull(decoder);

            _connection = connection;
            _encoder = encoder;
            _decoder = decoder;
            _logger = logger;

            var channelOptions = new BoundedChannelOptions(outgoingQueueCapacity)
            {
                // [DETAIL] FullMode.Wait applies natural backpressure. Publishers await SendAsync 
                // when the queue is full, preventing OutOfMemoryException under massive bursts.
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,  // Dedicated SendLoopAsync thread
                SingleWriter = false  // ThreadPool tasks/MessageBus will enqueue concurrently
            };

            _outgoingChannel = Channel.CreateBounded<IpcMessage>(channelOptions);
            _lastHeartbeatTicks = DateTime.UtcNow.Ticks;
        }

        public void SetIdentity(ClientIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(identity);
            Identity = identity;
            _logger?.LogInformation("Session {ConnectionId} authenticated as {ApplicationId} ({InstanceId}).",
                ConnectionId, identity.ApplicationId, identity.InstanceId);
        }

        public void RecordHeartbeat()
        {
            Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sessionCts.Token);
            var linkedToken = _linkedCts.Token;

            // [DETAIL] Long-running loops detached from the caller's synchronization context.
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(linkedToken), linkedToken);
            _sendLoopTask = Task.Run(() => SendLoopAsync(linkedToken), linkedToken);

            _logger?.LogTrace("Session {ConnectionId} started ingress/egress loops.", ConnectionId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Ingress processing: slices stream into frames via System.IO.Pipelines.
        /// Avoids large buffer allocations by keeping payload within the unmanaged memory pools.
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            PipeReader reader = _connection.Input;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (FrameReader.TryReadFrame(ref buffer, out ReadOnlySequence<byte> frameSequence))
                    {
                        ProcessIncomingFrame(frameSequence);
                    }

                    // [DETAIL] Advancing the pipeline cursor frees unmanaged memory segments 
                    // inside the Pipe, returning them to the internal MemoryPool.
                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted || result.IsCanceled)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogTrace("Receive loop canceled for session {ConnectionId}.", ConnectionId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fatal error reading from transport in session {ConnectionId}.", ConnectionId);
            }
            finally
            {
                // [DETAIL] Ensures transport teardown occurs immediately if the ingress pipe collapses.
                await TerminateAsync().ConfigureAwait(false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void ProcessIncomingFrame(ReadOnlySequence<byte> frameSequence)
        {
            byte[]? rentedFrame = null;
            try
            {
                ReadOnlySpan<byte> frameSpan;

                // [DETAIL] Fast path: the pipeline memory is contiguous. Zero allocation.
                if (frameSequence.IsSingleSegment)
                {
                    frameSpan = frameSequence.FirstSpan;
                }
                else
                {
                    // [DETAIL] Slow path: frame crosses a memory chunk boundary. Rent contiguous memory to normalize it.
                    rentedFrame = ArrayPool<byte>.Shared.Rent((int)frameSequence.Length);
                    frameSequence.CopyTo(rentedFrame);
                    frameSpan = rentedFrame.AsSpan(0, (int)frameSequence.Length);
                }

                // [DETAIL] IMessageDecoder must assume ownership of memory duplication for the actual message payload
                // if it sets rentPayloadBuffer: true. The rentedFrame below will be destroyed after this method.
                IpcMessage message = _decoder.Decode(frameSpan, rentPayloadBuffer: true);

                if (message.Type == MessageType.Heartbeat)
                {
                    RecordHeartbeat();
                    // Optional: short-circuit hearbeats to avoid polluting higher application layers.
                    // message.Dispose(); return; 
                }

                MessageReceived?.Invoke(this, message);
            }
            finally
            {
                if (rentedFrame != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedFrame);
                }
            }
        }

        /// <summary>
        /// Egress processing: strictly isolated thread draining the Channel and encoding to PipeWriter.
        /// </summary>
        private async Task SendLoopAsync(CancellationToken cancellationToken)
        {
            var reader = _outgoingChannel.Reader;

            try
            {
                // WaitToReadAsync is ultra-efficient, yielding the state machine until an item arrives.
                while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (reader.TryRead(out IpcMessage? message))
                    {
                        byte[]? buffer = null;
                        try
                        {
                            buffer = _encoder.Encode(message, out int totalBytes);
                            var memorySegment = new ReadOnlyMemory<byte>(buffer, 0, totalBytes);

                            await _connection.SendAsync(memorySegment, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (buffer != null)
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }

                            // [DETAIL] Egress pipeline owns the ultimate lifecycle of outgoing IpcMessage references.
                            message.Dispose();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogTrace("Send loop canceled for session {ConnectionId}.", ConnectionId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fatal error writing to transport in session {ConnectionId}.", ConnectionId);
            }
            finally
            {
                await TerminateAsync().ConfigureAwait(false);
            }
        }

        public async Task SendAsync(IpcMessage message, CancellationToken cancellationToken)
        {
            if (!IsAlive)
            {
                message.Dispose(); // Prevent leakage if we short-circuit rejection.
                throw new InvalidOperationException($"Session {ConnectionId} is no longer active.");
            }

            try
            {
                // [DETAIL] Propagates to bounded channel. Awaits here if channel is congested.
                await _outgoingChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Cleanup on failure to enqueue
                message.Dispose();
                throw;
            }
        }

        public async Task TerminateAsync()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            _logger?.LogInformation("Terminating session {ConnectionId}.", ConnectionId);

            _sessionCts.Cancel();
            _outgoingChannel.Writer.TryComplete();

            await _connection.CloseAsync().ConfigureAwait(false);

            SessionDisconnected?.Invoke(this);

            _sessionCts.Dispose();

            // [DETAIL] Unregisters the internal callback from the parent token tree.
            _linkedCts?.Dispose();
        }

        public void Dispose()
        {
            // Block gracefully during synchronous disposal triggers.
            TerminateAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Thread-safe registry container for client sessions.
    /// Replaced heavily allocating LINQ operations with dictionary enumerators for O(1)/O(N) zero-allocation reads.
    /// </summary>
    public sealed class ClientSessionManager : IDisposable
    {
        // [DETAIL] Ordinal comparison is optimal for string indexing.
        private readonly ConcurrentDictionary<string, IClientSession> _sessionsByConnId = new(StringComparer.Ordinal);
        private readonly ILogger<ClientSessionManager>? _logger;

        public event Action<IClientSession>? SessionRegistered;
        public event Action<IClientSession>? SessionUnregistered;

        public ClientSessionManager(ILogger<ClientSessionManager>? logger = null)
        {
            _logger = logger;
        }

        public bool TryRegister(IClientSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            if (_sessionsByConnId.TryAdd(session.ConnectionId, session))
            {
                session.SessionDisconnected += HandleSessionDisconnected;
                SessionRegistered?.Invoke(session);

                _logger?.LogInformation("Registered session {ConnectionId}. Total active: {Count}",
                    session.ConnectionId, _sessionsByConnId.Count);
                return true;
            }

            return false;
        }

        public bool TryUnregister(string connectionId, out IClientSession? session)
        {
            if (_sessionsByConnId.TryRemove(connectionId, out session))
            {
                session.SessionDisconnected -= HandleSessionDisconnected;
                SessionUnregistered?.Invoke(session);

                _logger?.LogInformation("Unregistered session {ConnectionId}. Total active: {Count}",
                    connectionId, _sessionsByConnId.Count);
                return true;
            }

            return false;
        }

        private void HandleSessionDisconnected(IClientSession session)
        {
            TryUnregister(session.ConnectionId, out _);
        }

        public IClientSession? FindByConnectionId(string connectionId)
        {
            _sessionsByConnId.TryGetValue(connectionId, out var session);
            return session;
        }

        public IClientSession? FindByApplicationId(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId)) return null;

            // [DETAIL] Avoiding LINQ .Values.FirstOrDefault() to prevent closure/enumerator allocations.
            foreach (var kvp in _sessionsByConnId)
            {
                var session = kvp.Value;
                if (session.IsAlive && string.Equals(session.Identity?.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }
            return null;
        }

        public IClientSession? FindByInstanceId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId)) return null;

            foreach (var kvp in _sessionsByConnId)
            {
                var session = kvp.Value;
                if (session.IsAlive && string.Equals(session.Identity?.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }
            return null;
        }

        public IReadOnlyList<IClientSession> FindAllByApplicationId(string applicationId)
        {
            if (string.IsNullOrWhiteSpace(applicationId)) return Array.Empty<IClientSession>();

            // [DETAIL] Initializing with an estimated capacity reduces array resizes during accumulation.
            var list = new List<IClientSession>(_sessionsByConnId.Count / 2);
            foreach (var kvp in _sessionsByConnId)
            {
                var session = kvp.Value;
                if (session.IsAlive && string.Equals(session.Identity?.ApplicationId, applicationId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(session);
                }
            }
            return list;
        }

        public IReadOnlyList<IClientSession> GetAllActiveSessions()
        {
            var list = new List<IClientSession>(_sessionsByConnId.Count);
            foreach (var kvp in _sessionsByConnId)
            {
                var session = kvp.Value;
                if (session.IsAlive)
                {
                    list.Add(session);
                }
            }
            return list;
        }

        public void Dispose()
        {
            foreach (var kvp in _sessionsByConnId)
            {
                kvp.Value.Dispose();
            }
            _sessionsByConnId.Clear();
        }
    }
}
