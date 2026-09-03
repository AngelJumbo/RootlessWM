using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WindowTilerTests
{
    [Fact]
    public void Tile_AppliesOnlyManagedWindowsInTrackedOrder()
    {
        var placementApplier = new RecordingPlacementApplier();
        var tiler = new WindowTiler(new MasterStackLayout(), placementApplier);
        var trackedWindows = new[]
        {
            CreateTrackedWindow((nint)1, WindowEligibility.Managed),
            CreateTrackedWindow((nint)2, WindowEligibility.ToolWindow),
            CreateTrackedWindow((nint)3, WindowEligibility.Managed)
        };

        var operation = tiler.Tile(new WindowBounds(0, 0, 1000, 800), trackedWindows);

        Assert.Equal(2, operation.PlannedPlacements.Count);
        Assert.Equal([(nint)1, (nint)3], placementApplier.Placements.Select(placement => placement.Handle));
        Assert.All(operation.PlacementResults, result => Assert.True(result.Applied));
    }

    [Fact]
    public void Tile_ApplierFailure_DoesNotPreventOtherPlacements()
    {
        var placementApplier = new RecordingPlacementApplier(failingHandle: (nint)1);
        var tiler = new WindowTiler(new MasterStackLayout(), placementApplier);
        var trackedWindows = new[]
        {
            CreateTrackedWindow((nint)1, WindowEligibility.Managed),
            CreateTrackedWindow((nint)2, WindowEligibility.Managed)
        };

        var operation = tiler.Tile(new WindowBounds(0, 0, 1000, 800), trackedWindows);

        Assert.Equal(2, placementApplier.Placements.Count);
        Assert.False(operation.PlacementResults[0].Applied);
        Assert.True(operation.PlacementResults[1].Applied);
    }

    [Fact]
    public void Tile_OrderedHandles_PreservesMasterAndStackOrder()
    {
        var placementApplier = new RecordingPlacementApplier();
        var tiler = new WindowTiler(new MasterStackLayout(), placementApplier);

        _ = tiler.Tile(new WindowBounds(0, 0, 1000, 800), [(nint)3, (nint)1, (nint)2]);

        Assert.Equal([(nint)3, (nint)1, (nint)2], placementApplier.Placements.Select(placement => placement.Handle));
    }

    [Fact]
    public void Tile_ApplierFailure_ReportsTheFailedHandleForLayoutRecovery()
    {
        var placementApplier = new RecordingPlacementApplier(failingHandle: (nint)2);
        var tiler = new WindowTiler(new MasterStackLayout(), placementApplier);

        var operation = tiler.Tile(new WindowBounds(0, 0, 1000, 800), [(nint)1, (nint)2, (nint)3]);

        Assert.Equal((nint)2, Assert.Single(operation.PlacementResults, result => !result.Applied).Handle);
    }

    private static TrackedWindow CreateTrackedWindow(nint handle, WindowEligibility eligibility)
    {
        var bounds = new WindowBounds(10, 20, 400, 300);
        var candidate = new WindowCandidate(handle, true, false, false, false, "SampleWindowClass", "sample-app", bounds);
        return new TrackedWindow(candidate, eligibility, bounds);
    }

    private sealed class RecordingPlacementApplier(nint? failingHandle = null) : IWindowPlacementApplier
    {
        public List<WindowPlacement> Placements { get; } = [];

        public PlacementResult Apply(WindowPlacement placement)
        {
            Placements.Add(placement);
            return placement.Handle == failingHandle
                ? PlacementResult.Skipped(placement.Handle, "simulated_failure")
                : PlacementResult.Success(placement.Handle);
        }
    }
}
