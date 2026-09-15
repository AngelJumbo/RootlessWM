namespace RootlessWM.Protocol.Transport;

public static class PipeTransportOptions
{
    public const int MaxMessageSizeBytes = 64 * 1024;

    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
}
