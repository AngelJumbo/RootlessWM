using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class MonitorCommandProcessorTests
{
    [Fact]
    public void Execute_FocusNextMonitor_FocusesFirstTiledWindowOnNextMonitor()
    {
        var state = CreateState((nint)1, (nint)2, (nint)3);
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);
        _ = ownership.Assign((nint)2, (nint)200);
        _ = ownership.Assign((nint)3, (nint)200);
        var commander = new RecordingWindowCommander();
        var processor = new MonitorCommandProcessor(state, ownership, commander);

        var executed = processor.Execute(TilingCommand.FocusNextMonitor, (nint)1, [(nint)100, (nint)200], static () => { });

        Assert.True(executed);
        Assert.Equal((nint)2, commander.FocusedHandle);
    }

    [Fact]
    public void TryGetFocusTarget_FocusPreviousMonitor_ReturnsFirstTiledWindowOnPreviousMonitor()
    {
        var state = CreateState((nint)1, (nint)2);
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);
        _ = ownership.Assign((nint)2, (nint)200);
        var processor = new MonitorCommandProcessor(state, ownership, new RecordingWindowCommander());

        var found = processor.TryGetFocusTarget(
            TilingCommand.FocusPreviousMonitor,
            (nint)1,
            [(nint)100, (nint)200],
            out var targetHandle);

        Assert.True(found);
        Assert.Equal((nint)2, targetHandle);
    }

    [Fact]
    public void Execute_MoveToNextMonitor_ReassignsAndRetiles()
    {
        var state = CreateState((nint)1, (nint)2);
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);
        _ = ownership.Assign((nint)2, (nint)200);
        var processor = new MonitorCommandProcessor(state, ownership, new RecordingWindowCommander());
        var retileCount = 0;

        var executed = processor.Execute(TilingCommand.MoveToNextMonitor, (nint)1, [(nint)100, (nint)200], () => retileCount++);

        Assert.True(executed);
        Assert.Equal(1, retileCount);
        Assert.Equal([(nint)1, (nint)2], ownership.GetWindows((nint)200, state.TiledHandles));
    }

    private static TilingState CreateState(params nint[] handles)
    {
        var state = new TilingState();
        state.Synchronize(handles.Select(handle => new TrackedWindow(
            new WindowCandidate(handle, true, false, false, false, "Sample", "sample", new WindowBounds(0, 0, 800, 600)),
            WindowEligibility.Managed,
            new WindowBounds(0, 0, 800, 600))));
        return state;
    }

    private sealed class RecordingWindowCommander : IWindowCommander
    {
        public nint? FocusedHandle { get; private set; }

        public bool Focus(nint handle)
        {
            FocusedHandle = handle;
            return true;
        }

        public bool Close(nint handle) => true;

        public bool Show(nint handle) => true;

        public bool Hide(nint handle) => true;
    }
}
