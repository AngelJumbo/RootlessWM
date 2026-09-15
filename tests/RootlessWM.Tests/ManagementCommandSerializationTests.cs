using System.Text.Json;
using RootlessWM.Protocol;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementCommandSerializationTests
{
    [Fact]
    public void ApplyManagementOptionsCommand_RoundTrips()
    {
        var command = new ApplyManagementOptionsCommand(
            RequestId.New(),
            new ManagementOptionsPayload(0.55, 8, 4, "MasterStack", 1, new[] { "explorer.exe" }, new[] { "Shell_TrayWnd" }, true, "HideWhenTiled"));

        AssertRoundTrips(command, "ApplyManagementOptions");
    }

    [Fact]
    public void ExecuteTilingCommandCommand_RoundTrips()
    {
        var command = new ExecuteTilingCommandCommand(RequestId.New(), ManagementTilingCommand.PromoteToMaster, 12345);

        AssertRoundTrips(command, "ExecuteTilingCommand");
    }

    [Fact]
    public void ToggleExplorerVisibilityCommand_RoundTrips()
    {
        AssertRoundTrips(new ToggleExplorerVisibilityCommand(RequestId.New()), "ToggleExplorerVisibility");
    }

    [Fact]
    public void SetMouseFocusSuspendedCommand_RoundTrips()
    {
        AssertRoundTrips(new SetMouseFocusSuspendedCommand(RequestId.New(), true), "SetMouseFocusSuspended");
    }

    [Fact]
    public void SetSessionLockedCommand_RoundTrips()
    {
        AssertRoundTrips(new SetSessionLockedCommand(RequestId.New(), false), "SetSessionLocked");
    }

    [Fact]
    public void ResyncCommand_RoundTrips()
    {
        AssertRoundTrips(new ResyncCommand(RequestId.New()), "Resync");
    }

    [Fact]
    public void RequestSnapshotCommand_RoundTrips()
    {
        AssertRoundTrips(new RequestSnapshotCommand(RequestId.New()), "RequestSnapshot");
    }

    [Fact]
    public void ShutdownCommand_RoundTrips()
    {
        AssertRoundTrips(new ShutdownCommand(RequestId.New()), "Shutdown");
    }

    [Fact]
    public void Deserialize_UnrecognizedDiscriminator_Throws()
    {
        var json = """{"commandType":"notARealCommand"}""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, ManagementProtocolJsonContext.Default.ManagementCommand));
    }

    private static void AssertRoundTrips(ManagementCommand command, string expectedDiscriminator)
    {
        var json = JsonSerializer.Serialize(command, ManagementProtocolJsonContext.Default.ManagementCommand);

        Assert.Contains($"\"commandType\":\"{expectedDiscriminator}\"", json);

        var deserialized = JsonSerializer.Deserialize(json, ManagementProtocolJsonContext.Default.ManagementCommand);
        var reserialized = JsonSerializer.Serialize(deserialized, ManagementProtocolJsonContext.Default.ManagementCommand);

        // Records with list-typed members do not get structural equality, so
        // round-tripping is verified by comparing the re-serialized JSON.
        Assert.Equal(json, reserialized);
    }
}
