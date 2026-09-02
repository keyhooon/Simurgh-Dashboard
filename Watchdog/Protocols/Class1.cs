using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Watchdog.Contracts;

// [COMPLIANCE] Enforcing strict nullable context for modern .NET 8/9 safety guarantees.
#nullable enable

namespace Watchdog.Protocols
{
    /// <summary>
    /// Contract for binary serialization of IpcMessage instances into raw pipe frames.
    /// Handles the transformation of domain objects into contiguous memory streams.
    /// </summary>
    public interface IMessageEncoder
    {
        /// <summary>
        /// Encodes an IpcMessage into a pooled or pre-allocated byte buffer.
        /// [MEMORY SENSITIVE] The caller is absolutely responsible for returning the rented buffer 
        /// via ArrayPool<byte>.Shared.Return() to prevent catastrophic LOH (Large Object Heap) fragmentation.
        /// </summary>
        byte[] Encode(IpcMessage message, out int totalBytesWritten);

        /// <summary>
        /// Encodes an IpcMessage directly into an existing memory destination.
        /// Useful for zero-copy scenarios where the transport layer provides its own buffer (e.g., PipeWriter.GetSpan).
        /// </summary>
        int Encode(IpcMessage message, Span<byte> destination);
    }

    /// <summary>
    /// Contract for deserializing binary pipe payloads into domain IpcMessage instances.
    /// Reconstructs memory-mapped structs and payload slices into managed wrappers.
    /// </summary>
    public interface IMessageDecoder
    {
        /// <summary>
        /// Decodes a sliced frame (header + metadata + payload) into an IpcMessage.
        /// [DESIGN DETAIL] rentPayloadBuffer = true transfers memory ownership to the IpcMessage.
        /// The message must be disposed to return the payload to the ArrayPool.
        /// </summary>
        IpcMessage Decode(ReadOnlySpan<byte> frameBuffer, bool rentPayloadBuffer = true);
    }

    /// <summary>
    /// Zero-copy, high-throughput binary frame encoder optimized for .NET 8/9.
    /// 
    /// Wire Layout Configuration:
    /// Offset 00: [40-byte Fixed MessageHeader (Blittable Struct)] 
    /// Offset 40: [2-byte SenderLength (Int16, LittleEndian)]
    /// Offset 42: [Dynamic: Sender UTF8 Bytes] 
    /// Offset XX: [2-byte TargetLength (Int16, LittleEndian)]
    /// Offset YY: [Dynamic: Target UTF8 Bytes] 
    /// Offset ZZ: [Dynamic: Raw Binary Payload Bytes]
    /// </summary>
    public sealed class MessageEncoder : IMessageEncoder
    {
        public byte[] Encode(IpcMessage message, out int totalBytesWritten)
        {
            ArgumentNullException.ThrowIfNull(message);

            // [PERFORMANCE] GetByteCount is fast, but we must pre-calculate exact buffer requirements 
            // to avoid resizing dynamically during the encoding phase.
            int senderByteCount = string.IsNullOrEmpty(message.SenderApplicationId)
                ? 0
                : Encoding.UTF8.GetByteCount(message.SenderApplicationId);

            int targetByteCount = string.IsNullOrEmpty(message.TargetApplicationId)
                ? 0
                : Encoding.UTF8.GetByteCount(message.TargetApplicationId);

            // [MATH DETAIL] Total Length Formula: 
            // $L_{total} = L_{header} + 2 \times 2 + L_{sender} + L_{target} + L_{payload}$
            int requiredLength = ProtocolConstants.HeaderSize +
                                 sizeof(short) + senderByteCount +
                                 sizeof(short) + targetByteCount +
                                 message.PayloadLength;

            // [MEMORY] Renting from ArrayPool to ensure zero Gen0/Gen1 GC allocations for the buffer itself.
            byte[] buffer = ArrayPool<byte>.Shared.Rent(requiredLength);
            Span<byte> span = new Span<byte>(buffer, 0, requiredLength);

            totalBytesWritten = EncodeInternal(message, span, senderByteCount, targetByteCount);
            return buffer;
        }

        public int Encode(IpcMessage message, Span<byte> destination)
        {
            ArgumentNullException.ThrowIfNull(message);

            int senderByteCount = string.IsNullOrEmpty(message.SenderApplicationId)
                ? 0
                : Encoding.UTF8.GetByteCount(message.SenderApplicationId);

            int targetByteCount = string.IsNullOrEmpty(message.TargetApplicationId)
                ? 0
                : Encoding.UTF8.GetByteCount(message.TargetApplicationId);

            int requiredLength = ProtocolConstants.HeaderSize +
                                 sizeof(short) + senderByteCount +
                                 sizeof(short) + targetByteCount +
                                 message.PayloadLength;

            if (destination.Length < requiredLength)
            {
                throw new ArgumentException(
                    $"Destination buffer too small. Required: {requiredLength} bytes, Available: {destination.Length} bytes.",
                    nameof(destination));
            }

            return EncodeInternal(message, destination, senderByteCount, targetByteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EncodeInternal(
            IpcMessage message,
            Span<byte> destination,
            int senderByteCount,
            int targetByteCount)
        {
            var header = message.Header;

            // [DATA INTEGRITY] Ensure the header reflects the actual payload length being written.
            header.PayloadLength = message.PayloadLength;

            // [CURIOSITY] Using MemoryMarshal.Write enables direct memory blitting of the struct.
            // This relies on [StructLayout(LayoutKind.Sequential, Pack = 1)] being strictly defined on MessageHeader.
            MemoryMarshal.Write(destination.Slice(0, ProtocolConstants.HeaderSize), ref header);
            int cursor = ProtocolConstants.HeaderSize;

            // [ENDIANNESS] Explicitly using LittleEndian guarantees cross-platform deterministic layouts 
            // (e.g., if a Linux ARM client talks to a Windows x64 server).
            BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(cursor, sizeof(short)), (short)senderByteCount);
            cursor += sizeof(short);

            if (senderByteCount > 0)
            {
                Encoding.UTF8.GetBytes(
                    message.SenderApplicationId.AsSpan(),
                    destination.Slice(cursor, senderByteCount));
                cursor += senderByteCount;
            }

            BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(cursor, sizeof(short)), (short)targetByteCount);
            cursor += sizeof(short);

            if (targetByteCount > 0)
            {
                Encoding.UTF8.GetBytes(
                    message.TargetApplicationId.AsSpan(),
                    destination.Slice(cursor, targetByteCount));
                cursor += targetByteCount;
            }

            // [PAYLOAD WRITING] Span.CopyTo is mapped to highly optimized native memmove instructions.
            if (message.PayloadLength > 0 && message.Payload != null)
            {
                message.Payload.AsSpan(0, message.PayloadLength)
                    .CopyTo(destination.Slice(cursor, message.PayloadLength));
                cursor += message.PayloadLength;
            }

            return cursor; // Returns the exact number of bytes written to the wire.
        }
    }

    /// <summary>
    /// Binary frame decoder parsing stream buffers without intermediate object allocations.
    /// Reconstructed to fix layout traversal bugs and properly extract variable length UTF-8 identities.
    /// </summary>
    public sealed class MessageDecoder : IMessageDecoder
    {
        public IpcMessage Decode(ReadOnlySpan<byte> frameBuffer, bool rentPayloadBuffer = true)
        {
            if (frameBuffer.Length < ProtocolConstants.HeaderSize)
            {
                throw new InvalidDataException(
                    $"Frame buffer underflow. Minimum required: {ProtocolConstants.HeaderSize} bytes, Received: {frameBuffer.Length} bytes.");
            }

            // [ZERO ALLOCATION] Reinterprets the underlying byte span directly into our struct format.
            var header = MemoryMarshal.Read<MessageHeader>(frameBuffer.Slice(0, ProtocolConstants.HeaderSize));

            // [SECURITY] Protocol Magic validation prevents parsing arbitrary garbage from port scanners 
            // or misconfigured clients, dropping the connection immediately.
            if (header.Magic != ProtocolConstants.MagicMarker)
            {
                throw new InvalidDataException($"Protocol magic mismatch. Expected: 0x{ProtocolConstants.MagicMarker:X4}, Actual: 0x{header.Magic:X4}");
            }

            // [COMPATIBILITY] Reject incompatible protocol versions gracefully.
            if (header.Version != ProtocolConstants.ProtocolVersion)
            {
                throw new InvalidDataException($"Protocol version mismatch. Supported: {ProtocolConstants.ProtocolVersion}, Received: {header.Version}");
            }

            int cursor = ProtocolConstants.HeaderSize;

            // 1. Extract Sender Identity
            if (frameBuffer.Length < cursor + sizeof(short))
                throw new InvalidDataException("Malformed frame: truncated sender length prefix.");

            short senderLen = BinaryPrimitives.ReadInt16LittleEndian(frameBuffer.Slice(cursor, sizeof(short)));
            cursor += sizeof(short);

            if (senderLen < 0 || frameBuffer.Length < cursor + senderLen)
                throw new InvalidDataException("Malformed frame: invalid or truncated sender string data.");

            string senderId = senderLen == 0
                ? string.Empty
                : Encoding.UTF8.GetString(frameBuffer.Slice(cursor, senderLen));
            cursor += senderLen;

            // 2. Extract Target Identity
            if (frameBuffer.Length < cursor + sizeof(short))
                throw new InvalidDataException("Malformed frame: truncated target length prefix.");

            short targetLen = BinaryPrimitives.ReadInt16LittleEndian(frameBuffer.Slice(cursor, sizeof(short)));
            cursor += sizeof(short);

            if (targetLen < 0 || frameBuffer.Length < cursor + targetLen)
                throw new InvalidDataException("Malformed frame: invalid or truncated target string data.");

            string targetId = targetLen == 0
                ? string.Empty
                : Encoding.UTF8.GetString(frameBuffer.Slice(cursor, targetLen));
            cursor += targetLen;

            // 3. Extract Payload
            int payloadLength = header.PayloadLength;
            if (payloadLength < 0 || payloadLength > ProtocolConstants.MaxPayloadLength)
            {
                // [SECURITY] Bounding the payload length defends against OutOfMemoryException (OOM) attacks 
                // triggered by maliciously crafted headers claiming multi-gigabyte payloads.
                throw new InvalidDataException($"Invalid payload length specified in header: {payloadLength} bytes. Maximum allowed: {ProtocolConstants.MaxPayloadLength}");
            }

            if (frameBuffer.Length < cursor + payloadLength)
            {
                throw new InvalidDataException(
                    $"Malformed frame: incomplete payload. Expected: {payloadLength} bytes, Available: {frameBuffer.Length - cursor} bytes.");
            }

            byte[]? payloadBuffer = null;
            if (payloadLength > 0)
            {
                var payloadSpan = frameBuffer.Slice(cursor, payloadLength);
                if (rentPayloadBuffer)
                {
                    // [MEMORY TRANSFER] The buffer rented here MUST be released by the consumer of the resulting IpcMessage.
                    payloadBuffer = ArrayPool<byte>.Shared.Rent(payloadLength);
                    payloadSpan.CopyTo(payloadBuffer.AsSpan(0, payloadLength));
                }
                else
                {
                    // Fallback to allocation if rent is bypassed (not recommended for high throughput).
                    payloadBuffer = payloadSpan.ToArray();
                }
            }

            // Note: IpcMessage assumes ownership of the `payloadBuffer` array reference.
            return new IpcMessage(
                header,
                senderId,
                targetId,
                payloadBuffer,
                payloadLength,
                isRentedPayload: rentPayloadBuffer && payloadLength > 0);
        }
    }

    /// <summary>
    /// Slices incoming continuous ReadOnlySequence byte streams into complete framed spans without fragmentation errors.
    /// Operates directly on the System.IO.Pipelines internal segments.
    /// </summary>
    public static class FrameReader
    {
        /// <summary>
        /// Inspects the sequence buffer to determine if a complete protocol frame is available for consumption.
        /// Returns true and slices the exact frame sequence if enough bytes have accumulated from the network.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadFrame(
            ref ReadOnlySequence<byte> buffer,
            out ReadOnlySequence<byte> frameSequence)
        {
            frameSequence = default;

            if (buffer.Length < ProtocolConstants.HeaderSize)
                return false; // Wait for more data from the socket

            // [PIPELINE OPTIMIZATION] Use stack memory to peek the header if the sequence spans multiple memory segments.
            Span<byte> headerBuffer = stackalloc byte[ProtocolConstants.HeaderSize];
            buffer.Slice(0, ProtocolConstants.HeaderSize).CopyTo(headerBuffer);

            var header = MemoryMarshal.Read<MessageHeader>(headerBuffer);

            if (header.Magic != ProtocolConstants.MagicMarker)
            {
                throw new InvalidDataException($"Corrupted frame marker: 0x{header.Magic:X4}");
            }

            if (header.PayloadLength < 0 || header.PayloadLength > ProtocolConstants.MaxPayloadLength)
            {
                throw new InvalidDataException($"Out of bounds payload size detected: {header.PayloadLength}");
            }

            // Minimum possible viable frame length (Header + 2x empty string prefixes)
            if (buffer.Length < ProtocolConstants.HeaderSize + (sizeof(short) * 2))
                return false;

            // [ADVANCED SLICING] We must determine the variable lengths of the strings to know where the payload ends.
            // SequenceReader provides ultra-fast cursor movement over non-contiguous ReadOnlySequence memory chunks.
            SequenceReader<byte> reader = new SequenceReader<byte>(buffer.Slice(ProtocolConstants.HeaderSize));

            if (!reader.TryReadLittleEndian(out short senderLen) || senderLen < 0)
                return false;

            if (reader.Remaining < senderLen + sizeof(short))
                return false;

            reader.Advance(senderLen); // Skip the actual UTF-8 sender bytes

            if (!reader.TryReadLittleEndian(out short targetLen) || targetLen < 0)
                return false;

            // Total bytes required for this distinct message block
            long totalFrameLength = ProtocolConstants.HeaderSize +
                                    sizeof(short) + senderLen +
                                    sizeof(short) + targetLen +
                                    header.PayloadLength;

            if (buffer.Length < totalFrameLength)
                return false; // The TCP packet might have been fragmented here. Wait for the rest.

            // Slice out exact frame window for the decoder
            frameSequence = buffer.Slice(0, totalFrameLength);

            // Advance the original buffer reference so the next iteration processes the next frame in the stream
            buffer = buffer.Slice(totalFrameLength);
            return true;
        }
    }
}
