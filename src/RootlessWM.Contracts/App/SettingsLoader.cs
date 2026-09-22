namespace RootlessWM.App;

/// <summary>
/// Wraps a <see cref="TomlSettingsProvider"/> and adds last-known-good caching so that an
/// invalid settings.toml never destabilizes an already-running instance: a failed load/reload
/// falls back to the most recently successful settings (or safe defaults if none exist yet).
/// </summary>
internal sealed class SettingsLoader
{
    private readonly TomlSettingsProvider _provider;
    private RootlessWMSettings? _lastGood;

    public SettingsLoader(TomlSettingsProvider provider)
    {
        _provider = provider;
    }

    public string FilePath => _provider.FilePath;

    public SettingsLoadResult Load()
    {
        try
        {
            var settings = _provider.Load();
            _lastGood = settings;
            return SettingsLoadResult.Ok(settings, _provider.FilePath);
        }
        catch (Exception exception) when (exception is IOException
            or System.Text.Json.JsonException
            or InvalidDataException
            or ArgumentOutOfRangeException
            or FormatException
            or Tomlyn.TomlException)
        {
            var usedLastKnownGood = _lastGood is not null;
            var fallback = _lastGood ?? RootlessWMSettings.Default;
            return SettingsLoadResult.Failed(fallback, _provider.FilePath, BuildErrorMessage(exception), usedLastKnownGood);
        }
    }

    private string BuildErrorMessage(Exception exception)
        => exception switch
        {
            Tomlyn.TomlException tomlException =>
                $"TOML syntax error in '{_provider.FilePath}': {tomlException.Message}",
            ArgumentOutOfRangeException argException =>
                $"Invalid value for '{argException.ParamName}' ({argException.ActualValue}) in '{_provider.FilePath}': {argException.Message}",
            _ => $"Failed to load settings from '{_provider.FilePath}': {exception.Message}"
        };
}
