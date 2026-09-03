using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class TilingStateTests
{
    [Fact]
    public void Synchronize_TracksOnlyManagedWindowsWithLargestWindowAsMaster()
    {
        var state = new TilingState();

        state.Synchronize([
            CreateTrackedWindow((nint)3, WindowEligibility.Managed, new WindowBounds(0, 0, 300, 200)),
            CreateTrackedWindow((nint)1, WindowEligibility.Managed, new WindowBounds(0, 0, 800, 600)),
            CreateTrackedWindow((nint)2, WindowEligibility.Minimized, new WindowBounds(0, 0, 1200, 800))
        ]);

        Assert.Equal([(nint)1, (nint)3], state.TiledHandles);
    }

    [Fact]
    public void PromoteToMaster_ReordersTiledHandles()
    {
        var state = CreateSynchronizedState();

        var promoted = state.PromoteToMaster((nint)3);

        Assert.True(promoted);
        Assert.Equal([(nint)3, (nint)1, (nint)2], state.TiledHandles);
    }

    [Fact]
    public void SwapWithNext_LastWindowDoesNotChangeOrder()
    {
        var state = CreateSynchronizedState();

        var swapped = state.SwapWithNext((nint)3);

        Assert.False(swapped);
        Assert.Equal([(nint)1, (nint)2, (nint)3], state.TiledHandles);
    }

    [Fact]
    public void SwapWithPrevious_ReordersTheActiveWindow()
    {
        var state = CreateSynchronizedState();

        var swapped = state.SwapWithPrevious((nint)3);

        Assert.True(swapped);
        Assert.Equal([(nint)1, (nint)3, (nint)2], state.TiledHandles);
    }

    [Fact]
    public void ToggleFloating_RemovesThenReturnsWindowToTileOrder()
    {
        var state = CreateSynchronizedState();

        Assert.True(state.ToggleFloating((nint)2));
        Assert.Equal([(nint)1, (nint)3], state.TiledHandles);
        Assert.Contains((nint)2, state.FloatingHandles);

        Assert.True(state.ToggleFloating((nint)2));
        Assert.Equal([(nint)1, (nint)3, (nint)2], state.TiledHandles);
        Assert.DoesNotContain((nint)2, state.FloatingHandles);
    }

    [Fact]
    public void Synchronize_RemovesNoLongerEligibleFloatingWindows()
    {
        var state = CreateSynchronizedState();
        _ = state.ToggleFloating((nint)2);

        state.Synchronize([CreateTrackedWindow((nint)1, WindowEligibility.Managed)]);

        Assert.Equal([(nint)1], state.TiledHandles);
        Assert.Empty(state.FloatingHandles);
    }

    private static TilingState CreateSynchronizedState()
    {
        var state = new TilingState();
        state.Synchronize([
            CreateTrackedWindow((nint)1, WindowEligibility.Managed),
            CreateTrackedWindow((nint)2, WindowEligibility.Managed),
            CreateTrackedWindow((nint)3, WindowEligibility.Managed)
        ]);
        return state;
    }

    private static TrackedWindow CreateTrackedWindow(
        nint handle,
        WindowEligibility eligibility,
        WindowBounds? bounds = null)
    {
        var originalBounds = bounds ?? new WindowBounds(10, 20, 400, 300);
        var candidate = new WindowCandidate(handle, true, false, false, false, "SampleWindowClass", "sample-app", originalBounds);
        return new TrackedWindow(candidate, eligibility, originalBounds);
    }
}
