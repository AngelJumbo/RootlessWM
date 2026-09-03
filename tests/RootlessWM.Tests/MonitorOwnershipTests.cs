using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class MonitorOwnershipTests
{
    [Fact]
    public void Assign_ReassignsWindowAndPreservesTilingOrderPerMonitor()
    {
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);
        _ = ownership.Assign((nint)2, (nint)200);
        _ = ownership.Assign((nint)3, (nint)100);

        var firstMonitorWindows = ownership.GetWindows((nint)100, [(nint)3, (nint)1, (nint)2]);

        Assert.Equal([(nint)3, (nint)1], firstMonitorWindows);
        Assert.True(ownership.Assign((nint)1, (nint)200));
        Assert.Equal([(nint)3], ownership.GetWindows((nint)100, [(nint)3, (nint)1, (nint)2]));
    }

    [Fact]
    public void Assign_InvalidHandle_ReturnsFalse()
    {
        var ownership = new MonitorOwnership();

        Assert.False(ownership.Assign(nint.Zero, (nint)100));
        Assert.False(ownership.Assign((nint)1, nint.Zero));
    }

    [Fact]
    public void MoveToAdjacentMonitor_ReassignsWindowAndWraps()
    {
        var ownership = new MonitorOwnership();
        _ = ownership.Assign((nint)1, (nint)100);

        var moved = ownership.MoveToAdjacentMonitor((nint)1, -1, [(nint)100, (nint)200], out var destination);

        Assert.True(moved);
        Assert.Equal((nint)200, destination);
        Assert.Equal([(nint)1], ownership.GetWindows((nint)200, [(nint)1]));
    }
}
