using System.Globalization;

namespace RootlessWM.App;

internal sealed class DateTimeWidgetProvider : IWidgetProvider
{
    public string Key => "datetime";

    public string GetText() => $" {DateTime.Now:HH:mm:ss}";

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var now = DateTime.Now;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["output"] = GetText(),
            ["year"] = now.ToString("yyyy", CultureInfo.InvariantCulture),
            ["short_year"] = now.ToString("yy", CultureInfo.InvariantCulture),
            ["month"] = now.ToString("MM", CultureInfo.InvariantCulture),
            ["day"] = now.ToString("dd", CultureInfo.InvariantCulture),
            ["hours"] = now.ToString("HH", CultureInfo.InvariantCulture),
            ["minutes"] = now.ToString("mm", CultureInfo.InvariantCulture),
            ["seconds"] = now.ToString("ss", CultureInfo.InvariantCulture),
        };
    }
}
