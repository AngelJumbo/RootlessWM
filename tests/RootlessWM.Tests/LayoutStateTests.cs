using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class LayoutStateTests
{
    [Fact]
    public void Cycle_IsIndependentPerWorkspaceAndMonitor()
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, 8, 8);

        _ = state.Cycle(0, (nint)100, fallback);
        _ = state.Cycle(1, (nint)200, fallback);

        Assert.Equal(MasterStackLayoutMode.MasterTop, state.Get(0, (nint)100, fallback).Mode);
        Assert.Equal(MasterStackLayoutMode.MasterTop, state.Get(1, (nint)200, fallback).Mode);
        Assert.Equal(MasterStackLayoutMode.MasterLeft, state.Get(0, (nint)200, fallback).Mode);
        Assert.Equal(MasterStackLayoutMode.MasterLeft, state.Get(1, (nint)100, fallback).Mode);
    }

    [Fact]
    public void AdjustMasterRatio_UpdatesOnlyTheActiveContext()
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, 8, 8);

        var updated = state.AdjustMasterRatio(0, (nint)100, fallback, 0.05);

        Assert.Equal(0.6, updated.MasterRatio, 10);
        Assert.Equal(0.55, state.Get(0, (nint)200, fallback).MasterRatio, 10);
        Assert.Equal(0.55, state.Get(1, (nint)100, fallback).MasterRatio, 10);
    }

    [Theory]
    [InlineData(0.95, 0.05, 0.95)]
    [InlineData(0.05, -0.05, 0.05)]
    public void AdjustMasterRatio_ClampsToSupportedRange(double initial, double delta, double expected)
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(initial, 8, 8);

        var updated = state.AdjustMasterRatio(0, (nint)100, fallback, delta);

        Assert.Equal(expected, updated.MasterRatio, 10);
    }

    [Fact]
    public void AdjustMasterCount_UpdatesOnlyTheActiveContext()
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, 8, 8);

        var updated = state.AdjustMasterCount(0, (nint)100, fallback, 1);

        Assert.Equal(2, updated.MasterCount);
        Assert.Equal(1, state.Get(0, (nint)200, fallback).MasterCount);
        Assert.Equal(1, state.Get(1, (nint)100, fallback).MasterCount);
    }

    [Theory]
    [InlineData(9, 1, 9)]
    [InlineData(1, -1, 1)]
    public void AdjustMasterCount_ClampsToSupportedRange(int initial, int delta, int expected)
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, 8, 8, MasterCount: initial);

        var updated = state.AdjustMasterCount(0, (nint)100, fallback, delta);

        Assert.Equal(expected, updated.MasterCount);
    }

    [Fact]
    public void AdjustOuterGap_UpdatesOnlyTheActiveContext()
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, 8, 8);

        var updated = state.AdjustOuterGap(0, (nint)100, fallback, 2);

        Assert.Equal(10, updated.OuterGap);
        Assert.Equal(8, updated.InnerGap);
        Assert.Equal(8, state.Get(0, (nint)200, fallback).OuterGap);
        Assert.Equal(8, state.Get(1, (nint)100, fallback).OuterGap);
    }

    [Fact]
    public void AdjustInnerGap_UpdatesOnlyTheActiveContext()
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, 8, 8);

        var updated = state.AdjustInnerGap(0, (nint)100, fallback, 2);

        Assert.Equal(8, updated.OuterGap);
        Assert.Equal(10, updated.InnerGap);
        Assert.Equal(8, state.Get(0, (nint)200, fallback).InnerGap);
        Assert.Equal(8, state.Get(1, (nint)100, fallback).InnerGap);
    }

    [Theory]
    [InlineData(100, 2, 100)]
    [InlineData(0, -2, 0)]
    public void AdjustOuterGap_ClampsToSupportedRange(int initial, int delta, int expected)
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, initial, 8);

        var updated = state.AdjustOuterGap(0, (nint)100, fallback, delta);

        Assert.Equal(expected, updated.OuterGap);
    }

    [Theory]
    [InlineData(100, 2, 100)]
    [InlineData(0, -2, 0)]
    public void AdjustInnerGap_ClampsToSupportedRange(int initial, int delta, int expected)
    {
        var state = new LayoutState();
        var fallback = new MasterStackLayoutOptions(0.55, 8, initial);

        var updated = state.AdjustInnerGap(0, (nint)100, fallback, delta);

        Assert.Equal(expected, updated.InnerGap);
    }
}
