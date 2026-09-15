using RootlessWM.Protocol;
using Xunit;

namespace RootlessWM.Tests;

public sealed class StateRevisionTests
{
    [Fact]
    public void None_IsZero()
    {
        Assert.Equal(0, StateRevision.None.Value);
    }

    [Fact]
    public void Next_IncrementsByOneWithoutMutatingOriginal()
    {
        var first = StateRevision.None;
        var second = first.Next();

        Assert.Equal(0, first.Value);
        Assert.Equal(1, second.Value);
    }

    [Fact]
    public void ComparisonOperators_AgreeWithCompareTo()
    {
        var first = StateRevision.None;
        var second = first.Next();
        var third = second.Next();

        Assert.True(first < second);
        Assert.True(second > first);
        Assert.True(second <= third);
        Assert.True(third >= second);
        Assert.True(first.CompareTo(second) < 0);
        Assert.True(third.CompareTo(second) > 0);
    }
}
