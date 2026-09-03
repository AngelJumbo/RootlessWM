using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceCommandProcessorTests
{
    [Fact]
    public void Execute_NextWorkspace_UpdatesVisibilityAndRetiles()
    {
        var state = new WorkspaceState(3);
        state.Synchronize([(nint)1]);
        var processor = new WorkspaceCommandProcessor(state);
        var visibilityUpdates = 0;
        var retileCount = 0;

        var executed = processor.Execute(
            TilingCommand.NextWorkspace,
            (nint)1,
            (nint)100,
            () => visibilityUpdates++,
            () => retileCount++);

        Assert.True(executed);
        Assert.Equal(1, visibilityUpdates);
        Assert.Equal(1, retileCount);
        Assert.Equal(1, state.GetCurrentWorkspace((nint)100));
    }

    [Fact]
    public void Execute_MoveToPreviousWorkspace_MovesActiveWindowAndRetiles()
    {
        var state = new WorkspaceState(3);
        state.Synchronize([(nint)1]);
        var processor = new WorkspaceCommandProcessor(state);
        var retileCount = 0;

        var executed = processor.Execute(
            TilingCommand.MoveToPreviousWorkspace,
            (nint)1,
            (nint)100,
            static () => { },
            () => retileCount++);

        Assert.True(executed);
        Assert.Equal(1, retileCount);
        _ = state.Switch((nint)100, -1);
        Assert.Equal([(nint)1], state.GetWindows((nint)100, [(nint)1], _ => (nint)100));
    }

    [Fact]
    public void Execute_SelectWorkspace9_SelectsTheExactWorkspaceOnTheActiveMonitor()
    {
        var state = new WorkspaceState();
        var processor = new WorkspaceCommandProcessor(state);
        var visibilityUpdates = 0;
        var retileCount = 0;

        var executed = processor.Execute(
            TilingCommand.SelectWorkspace9,
            (nint)1,
            (nint)100,
            () => visibilityUpdates++,
            () => retileCount++);

        Assert.True(executed);
        Assert.Equal(8, state.GetCurrentWorkspace((nint)100));
        Assert.Equal(1, visibilityUpdates);
        Assert.Equal(1, retileCount);
    }

    [Fact]
    public void Execute_MoveToWorkspace4_MovesActiveWindowToTheExactWorkspace()
    {
        var state = new WorkspaceState();
        state.Synchronize([(nint)1]);
        var processor = new WorkspaceCommandProcessor(state);

        var executed = processor.Execute(
            TilingCommand.MoveToWorkspace4,
            (nint)1,
            (nint)100,
            static () => { },
            static () => { });

        Assert.True(executed);
        _ = state.Select((nint)100, 3);
        Assert.Equal([(nint)1], state.GetWindows((nint)100, [(nint)1], _ => (nint)100));
    }
}
