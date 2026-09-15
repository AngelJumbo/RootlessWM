using System.Security.Principal;
using RootlessWM.Protocol;
using RootlessWM.Protocol.Transport;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementPipeAuthenticatorTests
{
    private static readonly SecurityIdentifier ExpectedSid = new("S-1-5-21-1111111111-2222222222-3333333333-1001");
    private static readonly SecurityIdentifier OtherSid = new("S-1-5-21-1111111111-2222222222-3333333333-1002");
    private const int ExpectedSessionId = 1;

    [Fact]
    public void Validate_MatchingRequest_IsAccepted()
    {
        var authenticator = new ManagementPipeAuthenticator(ExpectedSid, ExpectedSessionId);
        var request = new HandshakeRequest(ProtocolVersion.Current, ExpectedSid.Value, ExpectedSessionId, Environment.ProcessId, Guid.NewGuid());

        var outcome = authenticator.Validate(request, ExpectedSid, ExpectedSessionId);

        Assert.True(outcome.IsAccepted);
        Assert.Null(outcome.ErrorCode);
    }

    [Fact]
    public void Validate_WrongProtocolVersion_IsRejected()
    {
        var authenticator = new ManagementPipeAuthenticator(ExpectedSid, ExpectedSessionId);
        var request = new HandshakeRequest(ProtocolVersion.Current + 1, ExpectedSid.Value, ExpectedSessionId, Environment.ProcessId, Guid.NewGuid());

        var outcome = authenticator.Validate(request, ExpectedSid, ExpectedSessionId);

        Assert.False(outcome.IsAccepted);
        Assert.Equal(ProtocolErrorCode.ProtocolVersionMismatch, outcome.ErrorCode);
    }

    [Fact]
    public void Validate_RemoteUserSidMismatch_IsRejected()
    {
        var authenticator = new ManagementPipeAuthenticator(ExpectedSid, ExpectedSessionId);
        var request = new HandshakeRequest(ProtocolVersion.Current, ExpectedSid.Value, ExpectedSessionId, Environment.ProcessId, Guid.NewGuid());

        var outcome = authenticator.Validate(request, OtherSid, ExpectedSessionId);

        Assert.False(outcome.IsAccepted);
        Assert.Equal(ProtocolErrorCode.UnauthorizedUser, outcome.ErrorCode);
    }

    [Fact]
    public void Validate_SpoofedClaimedSid_IsRejected()
    {
        var authenticator = new ManagementPipeAuthenticator(ExpectedSid, ExpectedSessionId);
        var request = new HandshakeRequest(ProtocolVersion.Current, OtherSid.Value, ExpectedSessionId, Environment.ProcessId, Guid.NewGuid());

        var outcome = authenticator.Validate(request, ExpectedSid, ExpectedSessionId);

        Assert.False(outcome.IsAccepted);
        Assert.Equal(ProtocolErrorCode.UnauthorizedUser, outcome.ErrorCode);
    }

    [Fact]
    public void Validate_SessionMismatch_IsRejected()
    {
        var authenticator = new ManagementPipeAuthenticator(ExpectedSid, ExpectedSessionId);
        var request = new HandshakeRequest(ProtocolVersion.Current, ExpectedSid.Value, ExpectedSessionId + 1, Environment.ProcessId, Guid.NewGuid());

        var outcome = authenticator.Validate(request, ExpectedSid, ExpectedSessionId);

        Assert.False(outcome.IsAccepted);
        Assert.Equal(ProtocolErrorCode.UnauthorizedSession, outcome.ErrorCode);
    }
}
