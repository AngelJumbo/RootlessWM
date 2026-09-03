using System.Text.Json;
using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagedWindowStateSerializationTests
{
    [Fact]
    public void Serialize_RoundTripsPersistedFieldsWithoutNativeHandle()
    {
        var original = new ManagedWindowState(1234, new WindowBounds(10, 20, 300, 400));

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<ManagedWindowState>(json);

        Assert.DoesNotContain("\"Handle\":", json, StringComparison.Ordinal);
        var restoredState = Assert.IsType<ManagedWindowState>(restored);
        Assert.Equal(original, restoredState);
        Assert.Equal((nint)1234, restoredState.Handle);
    }
}
