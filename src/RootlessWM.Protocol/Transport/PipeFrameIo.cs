using System.Buffers.Binary;

namespace RootlessWM.Protocol.Transport;

// Wire framing: a 4-byte big-endian length prefix followed by the payload.
public static class PipeFrameIo
{
    private const int HeaderLength = 4;

    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> payload, int maxMessageSizeBytes, CancellationToken cancellationToken)
    {
        if (payload.Length > maxMessageSizeBytes)
        {
            throw new PipeProtocolException(ProtocolErrorCode.MessageTooLarge,
                $"Payload of {payload.Length} bytes exceeds the {maxMessageSizeBytes} byte limit.");
        }

        var header = new byte[HeaderLength];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<byte[]> ReadFrameAsync(Stream stream, int maxMessageSizeBytes, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderLength];
        await ReadExactAsync(stream, header, ProtocolErrorCode.MalformedMessage, cancellationToken).ConfigureAwait(false);

        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 0)
        {
            throw new PipeProtocolException(ProtocolErrorCode.MalformedMessage, "Frame declared a negative length.");
        }

        if (length > maxMessageSizeBytes)
        {
            throw new PipeProtocolException(ProtocolErrorCode.MessageTooLarge,
                $"Declared frame length {length} exceeds the {maxMessageSizeBytes} byte limit.");
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, ProtocolErrorCode.MalformedMessage, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, ProtocolErrorCode truncationErrorCode, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new PipeProtocolException(truncationErrorCode, "The stream ended before the frame was fully read.");
            }

            offset += read;
        }
    }
}
