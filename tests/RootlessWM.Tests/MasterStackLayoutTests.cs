using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class MasterStackLayoutTests
{
    private readonly MasterStackLayout _layout = new();
    private readonly WindowBounds _workArea = new(100, 200, 1000, 800);

    [Fact]
    public void Calculate_NoWindows_ReturnsNoPlacements()
    {
        var placements = _layout.Calculate(_workArea, []);

        Assert.Empty(placements);
    }

    [Fact]
    public void Calculate_OneWindow_UsesTheEntireWorkArea()
    {
        var placements = _layout.Calculate(_workArea, [(nint)1]);

        Assert.Equal(new WindowPlacement((nint)1, _workArea), Assert.Single(placements));
    }

    [Fact]
    public void Calculate_FloatingLayout_ReturnsNoPlacements()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.5, 8, 8, MasterStackLayoutMode.Floating));

        Assert.Empty(placements);
    }

    [Fact]
    public void Calculate_MonocleLayout_OverlapsEveryWindowInTheWorkArea()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2, (nint)3],
            new MasterStackLayoutOptions(0.5, 10, 10, MasterStackLayoutMode.Monocle));

        Assert.Equal(3, placements.Count);
        Assert.All(placements, placement => Assert.Equal(new WindowBounds(110, 210, 980, 780), placement.Bounds));
    }

    [Fact]
    public void Calculate_TwoWindows_UsesMasterRatioAndSharedHeight()
    {
        var placements = _layout.Calculate(_workArea, [(nint)1, (nint)2]);

        Assert.Equal(new WindowBounds(100, 200, 550, 800), placements[0].Bounds);
        Assert.Equal(new WindowBounds(650, 200, 450, 800), placements[1].Bounds);
    }

    [Fact]
    public void Calculate_MasterTop_PlacesMasterAboveHorizontalStack()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2, (nint)3],
            new MasterStackLayoutOptions(0.5, 10, 10, MasterStackLayoutMode.MasterTop));

        Assert.Equal(new WindowBounds(110, 210, 980, 385), placements[0].Bounds);
        Assert.Equal(new WindowBounds(110, 605, 485, 385), placements[1].Bounds);
        Assert.Equal(new WindowBounds(605, 605, 485, 385), placements[2].Bounds);
    }

    [Fact]
    public void Calculate_MasterTop_WithTwoWindowsUsesFullStackWidth()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.5, 10, 10, MasterStackLayoutMode.MasterTop));

        Assert.Equal(new WindowBounds(110, 210, 980, 385), placements[0].Bounds);
        Assert.Equal(new WindowBounds(110, 605, 980, 385), placements[1].Bounds);
    }

    [Fact]
    public void Calculate_ManyWindows_DistributesOddStackPixelsWithoutOverflow()
    {
        var workArea = new WindowBounds(0, 0, 101, 101);
        var placements = _layout.Calculate(workArea, [(nint)1, (nint)2, (nint)3, (nint)4]);

        Assert.Equal(new WindowBounds(0, 0, 56, 101), placements[0].Bounds);
        Assert.Equal(new WindowBounds(56, 0, 45, 34), placements[1].Bounds);
        Assert.Equal(new WindowBounds(56, 34, 45, 34), placements[2].Bounds);
        Assert.Equal(new WindowBounds(56, 68, 45, 33), placements[3].Bounds);
        Assert.All(placements, placement => AssertContained(workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_Gaps_KeepEveryPlacementInsideTheWorkArea()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2, (nint)3],
            new MasterStackLayoutOptions(0.5, 10, 10));

        Assert.Equal(new WindowBounds(110, 210, 485, 780), placements[0].Bounds);
        Assert.Equal(new WindowBounds(605, 210, 485, 385), placements[1].Bounds);
        Assert.Equal(new WindowBounds(605, 605, 485, 385), placements[2].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_OuterGapOnly_InsetsFromBordersWithoutInnerSpacing()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.5, 10, 0));

        Assert.Equal(new WindowBounds(110, 210, 490, 780), placements[0].Bounds);
        Assert.Equal(new WindowBounds(600, 210, 490, 780), placements[1].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_InnerGapOnly_SpacesWindowsWithoutBorderInset()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.5, 0, 10));

        Assert.Equal(new WindowBounds(100, 200, 495, 800), placements[0].Bounds);
        Assert.Equal(new WindowBounds(605, 200, 495, 800), placements[1].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Theory]
    [InlineData(0.04)]
    [InlineData(0.96)]
    public void Calculate_UnsupportedRatio_Throws(double masterRatio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _layout.Calculate(
            _workArea,
            [(nint)1],
            new MasterStackLayoutOptions(masterRatio, 0, 0)));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.95)]
    public void Calculate_SupportedRatioBoundary_ReturnsContainedPlacements(double masterRatio)
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(masterRatio, 0, 0));

        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_MasterLeft_TwoMasters_SplitsMasterColumn()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2, (nint)3],
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.MasterLeft, MasterCount: 2));

        Assert.Equal(new WindowBounds(100, 200, 500, 400), placements[0].Bounds);
        Assert.Equal(new WindowBounds(100, 600, 500, 400), placements[1].Bounds);
        Assert.Equal(new WindowBounds(600, 200, 500, 800), placements[2].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_MasterTop_TwoMasters_SplitsMasterRow()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2, (nint)3],
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.MasterTop, MasterCount: 2));

        Assert.Equal(new WindowBounds(100, 200, 500, 400), placements[0].Bounds);
        Assert.Equal(new WindowBounds(600, 200, 500, 400), placements[1].Bounds);
        Assert.Equal(new WindowBounds(100, 600, 1000, 400), placements[2].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_MasterCountAtLeastWindowCount_AllWindowsAreMasters()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2, (nint)3],
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.MasterLeft, MasterCount: 3));

        Assert.Equal(3, placements.Count);
        Assert.Equal(new WindowBounds(100, 200, 1000, 267), placements[0].Bounds);
        Assert.Equal(new WindowBounds(100, 467, 1000, 267), placements[1].Bounds);
        Assert.Equal(new WindowBounds(100, 734, 1000, 266), placements[2].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_MasterLeft_TwoMastersNoStack_ExtendsAcrossFullWidth()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.MasterLeft, MasterCount: 2));

        Assert.Equal(new WindowBounds(100, 200, 1000, 400), placements[0].Bounds);
        Assert.Equal(new WindowBounds(100, 600, 1000, 400), placements[1].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_MasterTop_TwoMastersNoStack_ExtendsAcrossFullHeight()
    {
        var placements = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.MasterTop, MasterCount: 2));

        Assert.Equal(new WindowBounds(100, 200, 500, 800), placements[0].Bounds);
        Assert.Equal(new WindowBounds(600, 200, 500, 800), placements[1].Bounds);
        Assert.All(placements, placement => AssertContained(_workArea, placement.Bounds));
    }

    [Fact]
    public void Calculate_MasterCountOne_MatchesSingleMasterLayout()
    {
        var single = _layout.Calculate(_workArea, [(nint)1, (nint)2]);
        var configured = _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.55, 0, 0, MasterStackLayoutMode.MasterLeft, MasterCount: 1));

        Assert.Equal(single, configured);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Calculate_UnsupportedMasterCount_Throws(int masterCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _layout.Calculate(
            _workArea,
            [(nint)1, (nint)2],
            new MasterStackLayoutOptions(0.5, 0, 0, MasterCount: masterCount)));
    }

    private static void AssertContained(WindowBounds outer, WindowBounds inner)
    {
        Assert.True(inner.Left >= outer.Left);
        Assert.True(inner.Top >= outer.Top);
        Assert.True(inner.Left + inner.Width <= outer.Left + outer.Width);
        Assert.True(inner.Top + inner.Height <= outer.Top + outer.Height);
    }
}
