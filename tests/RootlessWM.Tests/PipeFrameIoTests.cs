using System.Text;
using RootlessWM.Protocol;
using RootlessWM.Protocol.Transport;
using Xunit;

namespace RootlessWM.Tests;

public sealed class PipeFrameIoTests
{
    [Fact]
    public async Task WriteThenReadFrame_RoundTripsPayload()
    {
        using var stream = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes("hello frame");

        await PipeFrameIo.WriteFrameAsync(stream, payload, PipeTransportOptions.MaxMessageSizeBytes, CancellationToken.None);
        stream.Position = 0;
        var read = await PipeFrameIo.ReadFrameAsync(stream, PipeTransportOptions.MaxMessageSizeBytes, CancellationToken.None);

        Assert.Equal(payload, read);
    }

    [Fact]
    public async Task WriteFrame_PayloadTooLarge_ThrowsAndWritesNothing()
    {
        using var stream = new MemoryStream();
        var payload = new byte[16];

        var exception = await Assert.ThrowsAsync<PipeProtocolException>(
            () => PipeFrameIo.WriteFrameAsync(stream, payload, maxMessageSizeBytes: 8, CancellationToken.None));

        Assert.Equal(ProtocolErrorCode.MessageTooLarge, exception.ErrorCode);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task ReadFrame_DeclaredLengthExceedsLimit_ThrowsWithoutReadingBody()
    {
        using var stream = new MemoryStream();
        await PipeFrameIo.WriteFrameAsync(stream, new byte[16], 64, CancellationToken.None);
        stream.Position = 0;

        var exception = await Assert.ThrowsAsync<PipeProtocolException>(
            () => PipeFrameIo.ReadFrameAsync(stream, maxMessageSizeBytes: 8, CancellationToken.None));

        Assert.Equal(ProtocolErrorCode.MessageTooLarge, exception.ErrorCode);
    }

    [Fact]
    public async Task ReadFrame_TruncatedStream_ThrowsMalformedMessage()
    {
        using var stream = new MemoryStream(new byte[] { 0, 0, 0 });

        var exception = await Assert.ThrowsAsync<PipeProtocolException>(
            () => PipeFrameIo.ReadFrameAsync(stream, PipeTransportOptions.MaxMessageSizeBytes, CancellationToken.None));

        Assert.Equal(ProtocolErrorCode.MalformedMessage, exception.ErrorCode);
    }

    [Fact]
    public async Task ReadFrame_AlreadyCanceledToken_ThrowsOperationCanceled()
    {
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PipeFrameIo.ReadFrameAsync(stream, PipeTransportOptions.MaxMessageSizeBytes, cts.Token));
    }
}
