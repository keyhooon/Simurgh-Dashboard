// ==============================================================================
// WATCHDOG SERVER (.NET 8+)
// Fixed the compilation error and syntax corruption from the previous version.
// The previous code had a severe generation glitch around the DisposeAsync method.
// ==============================================================================

using System.IO.Pipelines;
using System.IO.Pipes;
using PipeOptions = System.IO.Pipes.PipeOptions;

namespace Watchdog.Transport
{
    public sealed class NamedPipeServerTransport : IAsyncDisposable
    {
        private readonly NamedPipeServerStream _pipeStream;

        // 1 means connected, 0 means disconnected/disposed.
        // Used for thread-safe state management during teardown.
        private int _isConnected;

        public NamedPipeServerTransport(string pipeName)
        {
            // Security (ACLs) is intentionally omitted for pure functionality and stability.
            // Using Asynchronous | WriteThrough for high-performance non-blocking I/O.
            _pipeStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                inBufferSize: 65536,
                outBufferSize: 65536);
        }

        public async Task WaitForConnectionAsync(CancellationToken cancellationToken = default)
        {
            // Waits for the .NET 4.7 client to connect.
            await _pipeStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref _isConnected, 1);
        }

        // Pushes bytes directly from the named pipe into the System.IO.Pipelines parser.
        // This avoids intermediate array allocations and prevents OOM during burst traffic.
        public async Task StartPumpingAsync(PipeWriter writer, CancellationToken cancellationToken)
        {
            Exception? readException = null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Rent memory directly from the pipeline
                    Memory<byte> memory = writer.GetMemory(4096);

                    // .NET 8 allows passing Memory<byte> directly to ReadAsync
                    int bytesRead = await _pipeStream.ReadAsync(memory, cancellationToken).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        // 0 bytes read indicates the client closed the connection gracefully.
                        break;
                    }

                    // Tell the PipeWriter how many bytes were actually written to its memory buffer
                    writer.Advance(bytesRead);

                    // Make the data available to the PipeReader (FrameReader/Decoder)
                    FlushResult result = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                    if (result.IsCompleted || result.IsCanceled)
                    {
                        // The reader side stopped reading, so we should stop pumping.
                        break;
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                // Expected when the cancellation token is triggered (e.g., service shutdown)
                readException = ex;
            }
            catch (Exception ex)
            {
                // Unhandled transport errors (e.g., broken pipe, unexpected client crash)
                readException = ex;
            }
            finally
            {
                // Always complete the writer so the reader knows no more data is coming.
                // Passing the exception propagates the error to the Protocol Layer.
                await writer.CompleteAsync(readException).ConfigureAwait(false);
            }
        }

        // Exposing a write method for the Egress channel to send serialized frames back to the client.
        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _isConnected) == 1)
            {
                await _pipeStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
        }

        // Fully corrected IAsyncDisposable implementation.
        // Fixes the syntax duplication error from the previous generation.
        public async ValueTask DisposeAsync()
        {
            // Ensure we only disconnect and dispose once to avoid ObjectDisposedExceptions
            if (Interlocked.Exchange(ref _isConnected, 0) == 1)
            {
                try
                {
                    if (_pipeStream.IsConnected)
                    {
                        _pipeStream.Disconnect();
                    }
                }
                catch
                {
                    // Swallow exceptions during disconnect to prevent blocking the disposal sequence.
                }

                await _pipeStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
    public sealed class NamedPipeClientTransport : IDisposable
    {
        private readonly NamedPipeClientStream _pipeStream;
        private int _isConnected;

        public NamedPipeClientTransport(string serverName, string pipeName)
        {
            // .NET 4.7 client configuration
            _pipeStream = new NamedPipeClientStream(
                serverName,
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        }

        public async Task ConnectAsync(int timeoutMs, CancellationToken cancellationToken = default)
        {
            // .NET 4.7 NamedPipeClientStream.ConnectAsync requires a timeout implementation
            await _pipeStream.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref _isConnected, 1);
        }

        // In .NET 4.7, we don't have Memory<byte> overloads natively without System.Memory polyfills.
        // Assuming byte[] buffers are used for legacy interop.
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _isConnected) == 1)
            {
                return await _pipeStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            return 0;
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _isConnected) == 1)
            {
                await _pipeStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                // Ensure data is sent immediately across the IPC boundary
                await _pipeStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isConnected, 0) == 1)
            {
                if (_pipeStream != null)
                {
                    try
                    {
                        if (_pipeStream.IsConnected)
                        {
                            _pipeStream.Close();
                        }
                    }
                    catch { /* Ignore dispose errors */ }
                    finally
                    {
                        _pipeStream.Dispose();
                    }
                }
            }
        }
    }
}
