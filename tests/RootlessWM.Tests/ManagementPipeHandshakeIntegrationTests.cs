using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using RootlessWM.Protocol;
using RootlessWM.Protocol.Transport;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementPipeHandshakeIntegrationTests
{
    private static readonly SecurityIdentifier ExpectedSid = WindowsIdentity.GetCurrent().User!;
    private const int ExpectedSessionId = 1;

    [Fact]
    public async Task Handshake_ValidRequest_IsAccepted()
    {
        var request = new HandshakeRequest(ProtocolVersion.Current, ExpectedSid.Value, ExpectedSessionId, Environment.ProcessId, Guid.NewGuid());

        var response = await RunHandshakeAsync(request);

        Assert.True(response.Accepted);
    }

    [Fact]
    public async Task Handshake_VersionMismatch_IsRejected()
    {
        var request = new HandshakeRequest(ProtocolVersion.Current + 1, ExpectedSid.Value, ExpectedSessionId, Environment.ProcessId, Guid.NewGuid());

        var response = await RunHandshakeAsync(request);

        Assert.False(response.Accepted);
        Assert.Equal(ProtocolErrorCode.ProtocolVersionMismatch, response.ErrorCode);
    }

    [Fact]
    public async Task Handshake_SidMismatch_IsRejected()
    {
        var spoofedSid = new SecurityIdentifier("S-1-5-21-1111111111-2222222222-3333333333-9999");
        var request = new HandshakeRequest(ProtocolVersion.Current, spoofedSid.Value, ExpectedSessionId, Environment.ProcessId, Guid.NewGuid());

        var response = await RunHandshakeAsync(request);

        Assert.False(response.Accepted);
        Assert.Equal(ProtocolErrorCode.UnauthorizedUser, response.ErrorCode);
    }

    // Uses a pair of anonymous pipes as the real duplex transport, exercising
    // PipeFrameIo and ManagementPipeAuthenticator exactly as a named pipe
    // would, without depending on named-pipe server naming/ACL setup, which
    // is covered separately by ManagementPipeNameTests / ManagementPipeSecurityFactoryTests.
    private static async Task<HandshakeResponse> RunHandshakeAsync(HandshakeRequest request)
    {
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource(timeout);

        await using var clientToServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        await using var clientToServerReader = new AnonymousPipeClientStream(PipeDirection.In, clientToServer.ClientSafePipeHandle);
        await using var serverToClient = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
        await using var serverToClientReader = new AnonymousPipeClientStream(PipeDirection.In, serverToClient.ClientSafePipeHandle);

        var requestJson = JsonSerializer.SerializeToUtf8Bytes(request);
        await PipeFrameIo.WriteFrameAsync(clientToServer, requestJson, PipeTransportOptions.MaxMessageSizeBytes, cts.Token);

        var receivedBytes = await PipeFrameIo.ReadFrameAsync(clientToServerReader, PipeTransportOptions.MaxMessageSizeBytes, cts.Token);
        var receivedRequest = JsonSerializer.Deserialize<HandshakeRequest>(receivedBytes)!;

        var authenticator = new ManagementPipeAuthenticator(ExpectedSid, ExpectedSessionId);
        var outcome = authenticator.Validate(receivedRequest, ExpectedSid, ExpectedSessionId);

        var response = new HandshakeResponse(outcome.IsAccepted, ProtocolVersion.Current, receivedRequest.Nonce, outcome.ErrorCode, outcome.ErrorMessage);
        var responseJson = JsonSerializer.SerializeToUtf8Bytes(response);
        await PipeFrameIo.WriteFrameAsync(serverToClient, responseJson, PipeTransportOptions.MaxMessageSizeBytes, cts.Token);

        var receivedResponseBytes = await PipeFrameIo.ReadFrameAsync(serverToClientReader, PipeTransportOptions.MaxMessageSizeBytes, cts.Token);
        return JsonSerializer.Deserialize<HandshakeResponse>(receivedResponseBytes)!;
    }
}
