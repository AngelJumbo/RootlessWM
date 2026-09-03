using System.Text.Json;

namespace RootlessWM.App;

internal sealed class JsonSettingsProvider
{
    private readonly string _filePath;

    public JsonSettingsProvider()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RootlessWM",
            "settings.json"))
    {
    }

    internal JsonSettingsProvider(string filePath)
    {
        _filePath = filePath;
    }

    public RootlessWMSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return RootlessWMSettings.Default;
        }

        using var stream = File.OpenRead(_filePath);
        var settings = JsonSerializer.Deserialize<RootlessWMSettings>(stream)
            ?? throw new InvalidDataException("The settings file is empty.");
        _ = settings.ToLayoutOptions();
        _ = settings.ToWorkspaceBarOptions();
        return settings;
    }
}
