namespace RootlessWM.Protocol.Transport;

public sealed class PipeProtocolException(ProtocolErrorCode errorCode, string message) : Exception(message)
{
    public ProtocolErrorCode ErrorCode { get; } = errorCode;
}
