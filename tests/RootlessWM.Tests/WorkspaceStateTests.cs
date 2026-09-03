using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceStateTests
{
    [Fact]
    public void Synchronize_AssignsNewWindowsToCurrentWorkspaceAndPreservesOrder()
    {
        var state = new WorkspaceState(3);
        state.Synchronize([(nint)2, (nint)1]);

        Assert.Equal(
            [(nint)1, (nint)2],
            state.GetWindows((nint)100, [(nint)1, (nint)2], _ => (nint)100));
    }

    [Fact]
    public void Switch_IsIndependentForEachMonitor()
    {
        var state = new WorkspaceState(2);
        state.Synchronize([(nint)1]);
        _ = state.Switch((nint)100, 1);
        state.Synchronize([(nint)1, (nint)2]);

        Assert.Equal(1, state.GetCurrentWorkspace((nint)100));
        Assert.Equal(0, state.GetCurrentWorkspace((nint)200));
    }

    [Fact]
    public void MoveToAdjacentWorkspace_MovesWindowRelativeToItsMonitorSelection()
    {
        var state = new WorkspaceState(3);
        state.Synchronize([(nint)1]);

        var moved = state.MoveToAdjacentWorkspace((nint)1, (nint)100, 1);

        Assert.True(moved);
        Assert.Equal(0, state.GetCurrentWorkspace((nint)100));
        Assert.False(state.IsInCurrentWorkspace((nint)1, (nint)100));
        _ = state.Switch((nint)100, 1);
        Assert.Equal([(nint)1], state.GetWindows((nint)100, [(nint)1], _ => (nint)100));
    }

    [Fact]
    public void MoveToCurrentWorkspace_ReassignsWindowToDestinationMonitorWorkspace()
    {
        var state = new WorkspaceState(3);
        _ = state.Switch((nint)100, 1);
        state.AssignToCurrentWorkspaceIfMissing((nint)1, (nint)100);

        var moved = state.MoveToCurrentWorkspace((nint)1, (nint)200);

        Assert.True(moved);
        Assert.True(state.IsInCurrentWorkspace((nint)1, (nint)200));
        Assert.False(state.MoveToCurrentWorkspace((nint)1, (nint)200));
    }

    [Fact]
    public void Synchronize_FloatingWindowOnAnotherWorkspace_KeepsItsWorkspaceAssignment()
    {
        var tilingState = new TilingState();
        tilingState.Synchronize([Managed((nint)1)]);
        var state = new WorkspaceState(3);
        _ = state.Switch((nint)100, 2);
        state.AssignToCurrentWorkspaceIfMissing((nint)1, (nint)100);
        state.Synchronize([.. tilingState.TiledHandles, .. tilingState.FloatingHandles]);

        _ = tilingState.ToggleFloating((nint)1);
        state.Synchronize([.. tilingState.TiledHandles, .. tilingState.FloatingHandles]);
        _ = tilingState.ToggleFloating((nint)1);
        state.Synchronize([.. tilingState.TiledHandles, .. tilingState.FloatingHandles]);

        Assert.True(state.HasWorkspace((nint)1));
        Assert.True(state.IsInCurrentWorkspace((nint)1, (nint)100));
    }

    [Fact]
    public void Synchronize_NewWindowOnMonitorWithActiveWorkspace_UsesThatWorkspace()
    {
        var state = new WorkspaceState(9);
        _ = state.Select((nint)200, 8);

        state.Synchronize([(nint)1], _ => state.GetCurrentWorkspace((nint)200));

        Assert.True(state.IsInCurrentWorkspace((nint)1, (nint)200));
        Assert.False(state.IsInCurrentWorkspace((nint)1, (nint)100));
    }

    private static TrackedWindow Managed(nint handle)
    {
        return new TrackedWindow(
            new WindowCandidate(handle, true, false, false, false, "Class", null, new WindowBounds(0, 0, 800, 600)),
            WindowEligibility.Managed,
            new WindowBounds(0, 0, 800, 600));
    }
}
