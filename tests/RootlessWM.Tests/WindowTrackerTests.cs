using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WindowTrackerTests
{
    [Fact]
    public void Observe_ExistingWindowPreservesOriginalBoundsAndUpdatesEligibility()
    {
        var tracker = new WindowTracker(new WindowEligibilityClassifier());
        var original = CreateCandidate(isMinimized: false, bounds: new WindowBounds(10, 20, 400, 300));
        var updated = CreateCandidate(isMinimized: true, bounds: new WindowBounds(30, 40, 500, 350));

        _ = tracker.Observe(original);
        var tracked = tracker.Observe(updated);

        Assert.Equal(WindowEligibility.Minimized, tracked.Eligibility);
        Assert.Equal(original.Bounds, tracked.OriginalBounds);
        Assert.Equal(updated.Bounds, tracked.Candidate.Bounds);
    }

    [Fact]
    public void Remove_UnknownHandle_IsIdempotent()
    {
        var tracker = new WindowTracker(new WindowEligibilityClassifier());

        var removed = tracker.Remove((nint)99);

        Assert.False(removed);
        Assert.Empty(tracker.Snapshot());
    }

    [Fact]
    public void Contains_TrackedAndUnknownHandles_ReturnsExpectedValue()
    {
        var tracker = new WindowTracker(new WindowEligibilityClassifier());
        _ = tracker.Observe(CreateCandidate(handle: (nint)42));

        Assert.True(tracker.Contains((nint)42));
        Assert.False(tracker.Contains((nint)99));
    }

    [Fact]
    public void Remove_WindowThatBecameIneligible_RemovesItFromTracking()
    {
        var tracker = new WindowTracker(new WindowEligibilityClassifier());
        _ = tracker.Observe(CreateCandidate(handle: (nint)42));

        var removed = tracker.Remove((nint)42);

        Assert.True(removed);
        Assert.False(tracker.Contains((nint)42));
    }

    [Fact]
    public void Seed_TracksAllWindowsForAuditing()
    {
        var tracker = new WindowTracker(new WindowEligibilityClassifier());
        var visible = CreateCandidate(handle: (nint)1);
        var hidden = CreateCandidate(handle: (nint)2, isVisible: false);

        var tracked = tracker.Seed([visible, hidden]);

        Assert.Equal(2, tracked.Count);
        Assert.Contains(tracked, window => window.Eligibility == WindowEligibility.Managed);
        Assert.Contains(tracked, window => window.Eligibility == WindowEligibility.NotVisible);
    }

    private static WindowCandidate CreateCandidate(
        nint? handle = null,
        bool isVisible = true,
        bool isMinimized = false,
        WindowBounds? bounds = null)
    {
        return new WindowCandidate(
            handle ?? (nint)42,
            isVisible,
            false,
            false,
            isMinimized,
            "SampleWindowClass",
            "sample-app",
            bounds ?? new WindowBounds(12, 34, 800, 600));
    }
}
