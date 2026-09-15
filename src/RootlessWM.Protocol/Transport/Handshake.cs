namespace RootlessWM.Protocol.Transport;

public sealed record HandshakeRequest(int ProtocolVersion, string UserSid, int SessionId, int ProcessId, Guid Nonce);

public sealed record HandshakeResponse(bool Accepted, int ProtocolVersion, Guid Nonce, ProtocolErrorCode? ErrorCode, string? ErrorMessage);

public sealed record HandshakeOutcome(bool IsAccepted, ProtocolErrorCode? ErrorCode, string? ErrorMessage)
{
    public static HandshakeOutcome Accept() => new(true, null, null);

    public static HandshakeOutcome Reject(ProtocolErrorCode errorCode, string errorMessage) => new(false, errorCode, errorMessage);
}
