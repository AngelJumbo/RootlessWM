using System.Security.Principal;

namespace RootlessWM.Protocol.Transport;

public interface IManagementPipeAuthenticator
{
    HandshakeOutcome Validate(HandshakeRequest request, SecurityIdentifier remoteUserSid, int remoteSessionId);
}

public sealed class ManagementPipeAuthenticator(SecurityIdentifier expectedUserSid, int expectedSessionId) : IManagementPipeAuthenticator
{
    public HandshakeOutcome Validate(HandshakeRequest request, SecurityIdentifier remoteUserSid, int remoteSessionId)
    {
        if (request.ProtocolVersion != ProtocolVersion.Current)
        {
            return HandshakeOutcome.Reject(ProtocolErrorCode.ProtocolVersionMismatch,
                $"Expected protocol version {ProtocolVersion.Current}, got {request.ProtocolVersion}.");
        }

        if (!remoteUserSid.Equals(expectedUserSid) || !string.Equals(request.UserSid, expectedUserSid.Value, StringComparison.OrdinalIgnoreCase))
        {
            return HandshakeOutcome.Reject(ProtocolErrorCode.UnauthorizedUser, "The connecting user is not authorized.");
        }

        if (remoteSessionId != expectedSessionId || request.SessionId != expectedSessionId)
        {
            return HandshakeOutcome.Reject(ProtocolErrorCode.UnauthorizedSession, "The connecting session is not authorized.");
        }

        return HandshakeOutcome.Accept();
    }
}
