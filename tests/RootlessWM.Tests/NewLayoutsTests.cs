using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class NewLayoutsTests
{
    private readonly MasterStackLayout _layout = new();
    private readonly WindowBounds _workArea = new(0, 0, 1000, 800);

    private static IReadOnlyList<nint> Handles(int count)
        => Enumerable.Range(1, count).Select(i => (nint)i).ToArray();

    private static void AssertPositiveNonOverlappingBounds(IReadOnlyList<WindowPlacement> placements, WindowBounds workArea)
    {
        foreach (var placement in placements)
        {
            Assert.True(placement.Bounds.Width > 0);
            Assert.True(placement.Bounds.Height > 0);
            Assert.True(placement.Bounds.Left >= workArea.Left);
            Assert.True(placement.Bounds.Top >= workArea.Top);
            Assert.True(placement.Bounds.Left + placement.Bounds.Width <= workArea.Left + workArea.Width);
            Assert.True(placement.Bounds.Top + placement.Bounds.Height <= workArea.Top + workArea.Height);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(9)]
    public void Grid_AllCounts_ProducesValidNonOverlappingBounds(int count)
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(count),
            new MasterStackLayoutOptions(0.5, 4, 4, MasterStackLayoutMode.Grid));

        Assert.Equal(count, placements.Count);
        AssertPositiveNonOverlappingBounds(placements, _workArea);
    }

    [Fact]
    public void Grid_FourWindows_ArrangesAsTwoByTwo()
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(4),
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.Grid));

        Assert.Equal(4, placements.Count);
        Assert.Equal(new WindowBounds(0, 0, 500, 400), placements[0].Bounds);
        Assert.Equal(new WindowBounds(500, 0, 500, 400), placements[1].Bounds);
        Assert.Equal(new WindowBounds(0, 400, 500, 400), placements[2].Bounds);
        Assert.Equal(new WindowBounds(500, 400, 500, 400), placements[3].Bounds);
    }

    [Fact]
    public void Grid_ThreeWindows_LastRowStretchesFullWidth()
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(3),
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.Grid));

        Assert.Equal(3, placements.Count);
        Assert.Equal(1000, placements[2].Bounds.Width);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void Fibonacci_AllCounts_ProducesValidBounds(int count)
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(count),
            new MasterStackLayoutOptions(0.5, 4, 4, MasterStackLayoutMode.Fibonacci));

        Assert.Equal(count, placements.Count);
        AssertPositiveNonOverlappingBounds(placements, _workArea);
    }

    [Fact]
    public void Fibonacci_TwoWindows_SplitsVerticallyInHalf()
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(2),
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.Fibonacci));

        Assert.Equal(new WindowBounds(0, 0, 500, 800), placements[0].Bounds);
        Assert.Equal(new WindowBounds(500, 0, 500, 800), placements[1].Bounds);
    }

    [Fact]
    public void Fibonacci_ThreeWindows_AlternatesSplitDirection()
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(3),
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.Fibonacci));

        Assert.Equal(new WindowBounds(0, 0, 500, 800), placements[0].Bounds);
        Assert.Equal(new WindowBounds(500, 0, 500, 400), placements[1].Bounds);
        Assert.Equal(new WindowBounds(500, 400, 500, 400), placements[2].Bounds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void Dwindle_AllCounts_ProducesValidBounds(int count)
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(count),
            new MasterStackLayoutOptions(0.6, 4, 4, MasterStackLayoutMode.Dwindle));

        Assert.Equal(count, placements.Count);
        AssertPositiveNonOverlappingBounds(placements, _workArea);
    }

    [Fact]
    public void Dwindle_TwoWindows_MasterUsesConfiguredRatio()
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(2),
            new MasterStackLayoutOptions(0.6, 0, 0, MasterStackLayoutMode.Dwindle));

        Assert.Equal(new WindowBounds(0, 0, 600, 800), placements[0].Bounds);
        Assert.Equal(new WindowBounds(600, 0, 400, 800), placements[1].Bounds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CenteredMaster_AllCounts_ProducesValidBounds(int count)
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(count),
            new MasterStackLayoutOptions(0.5, 4, 4, MasterStackLayoutMode.CenteredMaster));

        Assert.Equal(count, placements.Count);
        AssertPositiveNonOverlappingBounds(placements, _workArea);
    }

    [Fact]
    public void CenteredMaster_OneSecondary_PlacesItOnTheRight()
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(2),
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.CenteredMaster));

        var master = placements[0].Bounds;
        var secondary = placements[1].Bounds;
        Assert.True(secondary.Left >= master.Left + master.Width);
        Assert.True(master.Left > 0, "Master column should be centered, leaving space on the left.");
    }

    [Fact]
    public void CenteredMaster_TwoSecondaries_PlacesOneOnEachSide()
    {
        var placements = _layout.Calculate(
            _workArea,
            Handles(3),
            new MasterStackLayoutOptions(0.5, 0, 0, MasterStackLayoutMode.CenteredMaster));

        var master = placements[0].Bounds;
        var right = placements[1].Bounds;
        var left = placements[2].Bounds;
        Assert.True(right.Left >= master.Left + master.Width);
        Assert.True(left.Left + left.Width <= master.Left);
    }

    [Fact]
    public void SwitchingLayouts_DoesNotChangeWindowSet()
    {
        var handles = Handles(4);
        foreach (var mode in new[]
                 {
                     MasterStackLayoutMode.MasterLeft,
                     MasterStackLayoutMode.MasterTop,
                     MasterStackLayoutMode.Monocle,
                     MasterStackLayoutMode.Grid,
                     MasterStackLayoutMode.Fibonacci,
                     MasterStackLayoutMode.Dwindle,
                     MasterStackLayoutMode.CenteredMaster
                 })
        {
            var placements = _layout.Calculate(_workArea, handles, new MasterStackLayoutOptions(0.5, 4, 4, mode));
            Assert.Equal(handles.ToHashSet(), placements.Select(placement => placement.Handle).ToHashSet());
        }
    }
}
