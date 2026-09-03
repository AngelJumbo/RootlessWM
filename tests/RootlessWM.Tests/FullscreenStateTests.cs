using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class FullscreenStateTests
{
    [Fact]
    public void Toggle_SetsWindowForMonitorAndWorkspace()
    {
        var state = new FullscreenState();

        var toggled = state.Toggle((nint)10, 2, (nint)1);

        Assert.True(toggled);
        Assert.True(state.TryGetWindow((nint)10, 2, out var handle));
        Assert.Equal((nint)1, handle);
        Assert.True(state.IsFullscreen((nint)1));
    }

    [Fact]
    public void Toggle_SameWindowTwiceClearsScope()
    {
        var state = new FullscreenState();
        _ = state.Toggle((nint)10, 2, (nint)1);

        _ = state.Toggle((nint)10, 2, (nint)1);

        Assert.False(state.TryGetWindow((nint)10, 2, out _));
        Assert.False(state.IsFullscreen((nint)1));
    }

    [Fact]
    public void Toggle_ReplacesPreviousWindowInSameScope()
    {
        var state = new FullscreenState();
        _ = state.Toggle((nint)10, 2, (nint)1);

        _ = state.Toggle((nint)10, 2, (nint)2);

        Assert.True(state.TryGetWindow((nint)10, 2, out var handle));
        Assert.Equal((nint)2, handle);
        Assert.False(state.IsFullscreen((nint)1));
    }

    [Fact]
    public void Toggle_IsScopedPerMonitorAndWorkspace()
    {
        var state = new FullscreenState();

        _ = state.Toggle((nint)10, 0, (nint)1);
        _ = state.Toggle((nint)11, 0, (nint)2);
        _ = state.Toggle((nint)10, 1, (nint)3);

        Assert.True(state.TryGetWindow((nint)10, 0, out var first));
        Assert.True(state.TryGetWindow((nint)11, 0, out var second));
        Assert.True(state.TryGetWindow((nint)10, 1, out var third));
        Assert.Equal((nint)1, first);
        Assert.Equal((nint)2, second);
        Assert.Equal((nint)3, third);
    }

    [Fact]
    public void Toggle_IgnoresMissingMonitorOrWindow()
    {
        var state = new FullscreenState();

        Assert.False(state.Toggle(nint.Zero, 0, (nint)1));
        Assert.False(state.Toggle((nint)10, 0, nint.Zero));
    }

    [Fact]
    public void Synchronize_DropsWindowsThatAreNoLongerManaged()
    {
        var state = new FullscreenState();
        _ = state.Toggle((nint)10, 0, (nint)1);
        _ = state.Toggle((nint)11, 0, (nint)2);

        state.Synchronize([(nint)2]);

        Assert.False(state.TryGetWindow((nint)10, 0, out _));
        Assert.True(state.TryGetWindow((nint)11, 0, out _));
    }
}
