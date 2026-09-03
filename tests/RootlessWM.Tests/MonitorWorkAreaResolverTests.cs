using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class MonitorWorkAreaResolverTests
{
    private static readonly WindowBounds MonitorBounds = new(0, 0, 1920, 1080);
    private static readonly WindowBounds ReservedWorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void Resolve_VisibleTaskbar_UsesReservedWorkArea()
    {
        var result = MonitorWorkAreaResolver.Resolve(MonitorBounds, ReservedWorkArea, true);

        Assert.Equal(ReservedWorkArea, result);
    }

    [Fact]
    public void Resolve_HiddenTaskbar_UsesFullMonitorBounds()
    {
        var result = MonitorWorkAreaResolver.Resolve(MonitorBounds, ReservedWorkArea, false);

        Assert.Equal(MonitorBounds, result);
    }
}
