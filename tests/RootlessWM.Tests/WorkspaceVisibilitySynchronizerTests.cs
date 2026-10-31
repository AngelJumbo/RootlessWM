using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceVisibilitySynchronizerTests
{
    [Fact]
    public void Synchronize_ShowsCurrentWorkspaceAndHidesOtherWorkspaces()
    {
        var state = new WorkspaceState(2);
        state.Synchronize([(nint)1, (nint)2]);
        _ = state.MoveToAdjacentWorkspace((nint)2, (nint)100, 1);
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);
        _ = ownership.Assign((nint)2, (nint)100);
        var commander = new RecordingWindowCommander();
        var management = new ManagementState();
        _ = management.Enable();
        var synchronizer = new WorkspaceVisibilitySynchronizer(state, ownership, commander, management);

        synchronizer.Synchronize([(nint)1, (nint)2]);

        Assert.Equal([(nint)1], commander.ShownHandles);
        Assert.Equal([(nint)2], commander.HiddenHandles);
    }

    [Fact]
    public void Synchronize_UsesAnIndependentWorkspaceSelectionForEachMonitor()
    {
        var state = new WorkspaceState(2);
        state.Synchronize([(nint)1, (nint)2]);
        _ = state.Switch((nint)100, 1);
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);
        _ = ownership.Assign((nint)2, (nint)200);
        var commander = new RecordingWindowCommander();
        var management = new ManagementState();
        _ = management.Enable();
        var synchronizer = new WorkspaceVisibilitySynchronizer(state, ownership, commander, management);

        synchronizer.Synchronize([(nint)1, (nint)2]);

        Assert.Equal([(nint)2], commander.ShownHandles);
        Assert.Equal([(nint)1], commander.HiddenHandles);
    }

    [Fact]
    public void Synchronize_WhileDisabled_DoesNotChangeWindowVisibility()
    {
        var state = new WorkspaceState(2);
        state.Synchronize([(nint)1, (nint)2]);
        _ = state.MoveToAdjacentWorkspace((nint)2, (nint)100, 1);
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);
        _ = ownership.Assign((nint)2, (nint)100);
        var commander = new RecordingWindowCommander();
        var management = new ManagementState();
        _ = management.Enable();
        var synchronizer = new WorkspaceVisibilitySynchronizer(state, ownership, commander, management);
        synchronizer.Synchronize([(nint)1, (nint)2]);
        commander.ShownHandles.Clear();
        commander.HiddenHandles.Clear();
        _ = management.Disable();

        // Unlock notifications must not re-hide windows restored when management was disabled.
        synchronizer.Synchronize([(nint)1, (nint)2]);
        synchronizer.Synchronize([(nint)1, (nint)2]);

        Assert.Empty(commander.ShownHandles);
        Assert.Empty(commander.HiddenHandles);
        Assert.True(state.IsInCurrentWorkspace((nint)1, (nint)100));
        Assert.False(state.IsInCurrentWorkspace((nint)2, (nint)100));

        _ = management.Enable();
        synchronizer.Synchronize([(nint)1, (nint)2]);

        Assert.Equal([(nint)1], commander.ShownHandles);
        Assert.Equal([(nint)2], commander.HiddenHandles);
    }

    private sealed class RecordingWindowCommander : IWindowCommander
    {
        public List<nint> ShownHandles { get; } = [];

        public List<nint> HiddenHandles { get; } = [];

        public bool Focus(nint handle) => true;

        public bool Close(nint handle) => true;

        public bool Show(nint handle)
        {
            ShownHandles.Add(handle);
            return true;
        }

        public bool Hide(nint handle)
        {
            HiddenHandles.Add(handle);
            return true;
        }
    }
}
