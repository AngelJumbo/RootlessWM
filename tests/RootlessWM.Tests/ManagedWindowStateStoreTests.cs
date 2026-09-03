using RootlessWM.App;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagedWindowStateStoreTests
{
    [Fact]
    public void Load_InvalidJson_ReportsFailureToCaller()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"RootlessWM-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "managed-windows.json");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{ invalid json");

        try
        {
            var store = new JsonManagedWindowStateStore(path);

            Assert.Throws<System.Text.Json.JsonException>(() => store.Load());
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

}
