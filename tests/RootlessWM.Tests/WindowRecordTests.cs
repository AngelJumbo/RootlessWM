using Xunit;
using RootlessWM.Domain;

namespace RootlessWM.Tests;

public sealed class WindowRecordTests
{
    [Fact]
    public void Constructor_PreservesOriginalBoundsForRestoration()
    {
        var originalBounds = new WindowBounds(12, 34, 800, 600);

        var record = new WindowRecord((nint)42, originalBounds);

        Assert.Equal((nint)42, record.Handle);
        Assert.Equal(originalBounds, record.OriginalBounds);
    }
}
