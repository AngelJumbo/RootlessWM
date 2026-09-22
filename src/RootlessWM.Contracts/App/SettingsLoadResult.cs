namespace RootlessWM.App;

internal sealed record SettingsLoadResult(
    bool Success,
    RootlessWMSettings Settings,
    string? ErrorMessage,
    string FilePath,
    bool UsedLastKnownGood = false)
{
    public static SettingsLoadResult Ok(RootlessWMSettings settings, string filePath)
        => new(true, settings, null, filePath);

    public static SettingsLoadResult Failed(RootlessWMSettings fallback, string filePath, string errorMessage, bool usedLastKnownGood)
        => new(false, fallback, errorMessage, filePath, usedLastKnownGood);
}
