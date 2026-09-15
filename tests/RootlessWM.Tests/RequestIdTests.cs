using RootlessWM.Protocol;
using Xunit;

namespace RootlessWM.Tests;

public sealed class RequestIdTests
{
    [Fact]
    public void New_ReturnsNonDefaultValue()
    {
        var id = RequestId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void New_ReturnsDistinctValues()
    {
        var first = RequestId.New();
        var second = RequestId.New();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ToString_ReturnsThirtyTwoCharacterHex()
    {
        var id = RequestId.New();

        var text = id.ToString();

        Assert.Equal(32, text.Length);
        Assert.DoesNotContain('-', text);
    }
}
