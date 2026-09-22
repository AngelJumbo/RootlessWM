using RootlessWM.App;
using Xunit;

namespace RootlessWM.Tests;

public sealed class SettingsLoaderTests
{
    [Fact]
    public void Load_ValidThenInvalid_KeepsLastKnownGood()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "MasterRatio = 0.6");
        var loader = new SettingsLoader(new TomlSettingsProvider(path));

        var first = loader.Load();
        Assert.True(first.Success);
        Assert.Equal(0.6, first.Settings.MasterRatio);

        File.WriteAllText(path, "MasterRatio = = 0.6");
        var second = loader.Load();

        Assert.False(second.Success);
        Assert.True(second.UsedLastKnownGood);
        Assert.Equal(0.6, second.Settings.MasterRatio);
        Assert.Contains(path, second.ErrorMessage);
    }

    [Fact]
    public void Load_InvalidWithNoPriorGood_FallsBackToDefaults()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "MasterRatio = = 0.6");
        var loader = new SettingsLoader(new TomlSettingsProvider(path));

        var result = loader.Load();

        Assert.False(result.Success);
        Assert.False(result.UsedLastKnownGood);
        Assert.Equal(RootlessWMSettings.Default, result.Settings);
    }

    [Fact]
    public void Load_ValidToml_Succeeds()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "MasterRatio = 0.7");
        var loader = new SettingsLoader(new TomlSettingsProvider(path));

        var result = loader.Load();

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(0.7, result.Settings.MasterRatio);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RootlessWM-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
