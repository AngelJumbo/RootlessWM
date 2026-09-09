namespace RootlessWM.App;

/// <summary>Produces the text for one status-bar widget kind.</summary>
internal interface IWidgetProvider
{
    /// <summary>Stable string key used in config (e.g. "cpu", "clock"). Case-insensitive.</summary>
    string Key { get; }

    /// <summary>Renders the current value. Called on the UI thread once per timer tick.</summary>
    string GetText();

    IReadOnlyDictionary<string, string> GetValues()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["output"] = GetText() };
}
