using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class TilingCommandProcessorTests
{
    [Fact]
    public void Execute_PromoteToMaster_RetilesAfterStateChange()
    {
        var state = CreateState();
        var processor = new TilingCommandProcessor(state, new RecordingWindowCommander());
        var retileCount = 0;

        var executed = processor.Execute(TilingCommand.PromoteToMaster, (nint)3, () => retileCount++);

        Assert.True(executed);
        Assert.Equal(1, retileCount);
        Assert.Equal([(nint)3, (nint)1, (nint)2], state.TiledHandles);
    }

    [Fact]
    public void Execute_FocusNext_FocusesTheNextTiledWindowWithoutRetiling()
    {
        var windowCommander = new RecordingWindowCommander();
        var processor = new TilingCommandProcessor(CreateState(), windowCommander);

        var executed = processor.Execute(TilingCommand.FocusNext, (nint)3, static () => throw new InvalidOperationException());

        Assert.True(executed);
        Assert.Equal((nint)1, windowCommander.FocusedHandle);
    }

    [Fact]
    public void Execute_FocusPrevious_FocusesThePreviousTiledWindowWithoutRetiling()
    {
        var windowCommander = new RecordingWindowCommander();
        var processor = new TilingCommandProcessor(CreateState(), windowCommander);

        var executed = processor.Execute(TilingCommand.FocusPrevious, (nint)1, static () => throw new InvalidOperationException());

        Assert.True(executed);
        Assert.Equal((nint)3, windowCommander.FocusedHandle);
    }

    [Fact]
    public void TryGetFocusTarget_FocusNext_ReturnsTheCyclicDestination()
    {
        var processor = new TilingCommandProcessor(CreateState(), new RecordingWindowCommander());

        var found = processor.TryGetFocusTarget(TilingCommand.FocusNext, (nint)3, out var targetHandle);

        Assert.True(found);
        Assert.Equal((nint)1, targetHandle);
    }

    [Fact]
    public void TryGetFocusTarget_WithFocusScope_CyclesOnlyWithinScope()
    {
        var processor = new TilingCommandProcessor(CreateState(), new RecordingWindowCommander());

        var found = processor.TryGetFocusTarget(
            TilingCommand.FocusNext,
            (nint)3,
            out var targetHandle,
            [(nint)1, (nint)3]);

        Assert.True(found);
        Assert.Equal((nint)1, targetHandle);
    }

    [Fact]
    public void TryGetFocusTarget_NonFocusCommand_ReturnsFalse()
    {
        var processor = new TilingCommandProcessor(CreateState(), new RecordingWindowCommander());

        var found = processor.TryGetFocusTarget(TilingCommand.Close, (nint)1, out var targetHandle);

        Assert.False(found);
        Assert.Equal(nint.Zero, targetHandle);
    }

    [Fact]
    public void Execute_SwapWithPrevious_RetilesAfterStateChange()
    {
        var state = CreateState();
        var processor = new TilingCommandProcessor(state, new RecordingWindowCommander());
        var retileCount = 0;

        var executed = processor.Execute(TilingCommand.SwapWithPrevious, (nint)3, () => retileCount++);

        Assert.True(executed);
        Assert.Equal(1, retileCount);
        Assert.Equal([(nint)1, (nint)3, (nint)2], state.TiledHandles);
    }

    [Fact]
    public void Execute_Close_RequestsCloseForTheActiveWindow()
    {
        var windowCommander = new RecordingWindowCommander();
        var processor = new TilingCommandProcessor(CreateState(), windowCommander);

        var executed = processor.Execute(TilingCommand.Close, (nint)2, static () => throw new InvalidOperationException());

        Assert.True(executed);
        Assert.Equal((nint)2, windowCommander.ClosedHandle);
    }

    private static TilingState CreateState()
    {
        var state = new TilingState();
        state.Synchronize([
            CreateWindow((nint)1),
            CreateWindow((nint)2),
            CreateWindow((nint)3)
        ]);
        return state;
    }

    private static TrackedWindow CreateWindow(nint handle)
    {
        var bounds = new WindowBounds(0, 0, 800, 600);
        return new TrackedWindow(new WindowCandidate(handle, true, false, false, false, "Sample", "sample", bounds), WindowEligibility.Managed, bounds);
    }

    private sealed class RecordingWindowCommander : IWindowCommander
    {
        public nint? FocusedHandle { get; private set; }

        public nint? ClosedHandle { get; private set; }

        public bool Focus(nint handle)
        {
            FocusedHandle = handle;
            return true;
        }

        public bool Close(nint handle)
        {
            ClosedHandle = handle;
            return true;
        }

        public bool Show(nint handle) => true;

        public bool Hide(nint handle) => true;
    }
}
