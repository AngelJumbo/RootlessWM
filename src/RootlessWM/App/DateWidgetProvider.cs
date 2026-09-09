namespace RootlessWM.App;

internal sealed class DateWidgetProvider : IWidgetProvider
{
    public string Key => "date";

    public string GetText() => $" {DateTime.Now:yyyy-MM-dd}";

    public IReadOnlyDictionary<string, string> GetValues()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["output"] = GetText() };
}
