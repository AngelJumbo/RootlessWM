using System.Text.Json;
using RootlessWM.Protocol;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementResponseSerializationTests
{
    [Fact]
    public void CommandAckResponse_Success_RoundTrips()
    {
        var response = new CommandAckResponse(RequestId.New(), true, null, null);

        AssertRoundTrips(response);
    }

    [Fact]
    public void CommandAckResponse_Failure_RoundTrips()
    {
        var response = new CommandAckResponse(RequestId.New(), false, ProtocolErrorCode.CommandFailed, "boom");

        AssertRoundTrips(response);
    }

    [Fact]
    public void SnapshotResponse_WithNestedSnapshot_RoundTrips()
    {
        var snapshot = new ManagementStateSnapshot(
            StateRevision.None.Next(),
            true,
            0,
            9,
            0.5,
            8,
            4,
            1,
            new[]
            {
                new ManagedWindowSnapshot(1, 0, false, false, new WindowBoundsSnapshot(0, 0, 800, 600)),
                new ManagedWindowSnapshot(2, 1, true, false, new WindowBoundsSnapshot(10, 10, 400, 300))
            });

        var response = new SnapshotResponse(RequestId.New(), true, null, null, snapshot);

        AssertRoundTrips(response);
    }

    private static void AssertRoundTrips(ManagementResponse response)
    {
        var json = JsonSerializer.Serialize(response, ManagementProtocolJsonContext.Default.ManagementResponse);

        var deserialized = JsonSerializer.Deserialize(json, ManagementProtocolJsonContext.Default.ManagementResponse);
        var reserialized = JsonSerializer.Serialize(deserialized, ManagementProtocolJsonContext.Default.ManagementResponse);

        // Records with list-typed members do not get structural equality, so
        // round-tripping is verified by comparing the re-serialized JSON.
        Assert.Equal(json, reserialized);
    }
}
