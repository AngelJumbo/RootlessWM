namespace RootlessWM.App;

internal sealed class ClockWidgetProvider : IWidgetProvider
{
    public string Key => "clock";

    public string GetText() => $" {DateTime.Now:HH:mm:ss}";

    public IReadOnlyDictionary<string, string> GetValues()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["output"] = GetText() };
}
