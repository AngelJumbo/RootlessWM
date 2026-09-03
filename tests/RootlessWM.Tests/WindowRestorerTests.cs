using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WindowRestorerTests
{
    [Fact]
    public void Restore_ApplierFailure_DoesNotPreventOtherWindowsFromRestoring()
    {
        var boundsRestorer = new RecordingBoundsRestorer(failingHandle: (nint)1);
        var restorer = new WindowRestorer(boundsRestorer);
        var states = new[]
        {
            new ManagedWindowState(1, new WindowBounds(10, 20, 300, 400)),
            new ManagedWindowState(2, new WindowBounds(30, 40, 500, 600))
        };

        var results = restorer.Restore(states);

        Assert.Equal(2, boundsRestorer.Placements.Count);
        Assert.False(results[0].Applied);
        Assert.True(results[1].Applied);
        Assert.Equal(states[1].OriginalBounds, boundsRestorer.Placements[1].Bounds);
    }

    [Fact]
    public void AddIfMissing_NewManagedWindow_PreservesItsOriginalBounds()
    {
        var existing = new[] { new ManagedWindowState(1, new WindowBounds(1, 2, 3, 4)) };
        var originalBounds = new WindowBounds(10, 20, 300, 400);
        var trackedWindow = new TrackedWindow(
            new WindowCandidate((nint)2, true, false, false, false, "Sample", "sample", originalBounds),
            WindowEligibility.Managed,
            originalBounds);

        var states = ManagedWindowStateSet.AddIfMissing(existing, trackedWindow);

        Assert.Equal(2, states.Count);
        Assert.Equal(new ManagedWindowState(2, originalBounds), states[1]);
    }

    [Fact]
    public void AddIfMissing_ExistingManagedWindow_ReturnsOriginalStateCollection()
    {
        var existing = new[] { new ManagedWindowState(1, new WindowBounds(1, 2, 3, 4)) };
        var trackedWindow = new TrackedWindow(
            new WindowCandidate((nint)1, true, false, false, false, "Sample", "sample", new WindowBounds(10, 20, 300, 400)),
            WindowEligibility.Managed,
            new WindowBounds(10, 20, 300, 400));

        var states = ManagedWindowStateSet.AddIfMissing(existing, trackedWindow);

        Assert.Same(existing, states);
    }

    [Theory]
    [InlineData("invalid_window")]
    [InlineData("invalid_bounds")]
    [InlineData("restore_window_pos_failed_5")]
    public void RetainsForRetry_UnrecoverableRestoreFailure_ReturnsFalse(string reason)
    {
        var result = PlacementResult.Skipped((nint)1, reason);

        Assert.False(RestoreResultPolicy.RetainsForRetry(result));
    }

    [Fact]
    public void RetainsForRetry_TransientNativeRestoreFailure_ReturnsTrue()
    {
        var result = PlacementResult.Skipped((nint)1, "restore_window_pos_failed_87");

        Assert.True(RestoreResultPolicy.RetainsForRetry(result));
    }

    private sealed class RecordingBoundsRestorer(nint? failingHandle = null) : IWindowBoundsRestorer
    {
        public List<WindowPlacement> Placements { get; } = [];

        public PlacementResult Restore(WindowPlacement placement)
        {
            Placements.Add(placement);
            return placement.Handle == failingHandle
                ? PlacementResult.Skipped(placement.Handle, "simulated_failure")
                : PlacementResult.Success(placement.Handle);
        }
    }
}
