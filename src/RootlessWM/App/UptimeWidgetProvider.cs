namespace RootlessWM.App;

internal sealed class UptimeWidgetProvider : IWidgetProvider
{
    public string Key => "uptime";

    public string GetText()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $" {uptime.Days}d {uptime.Hours:00}:{uptime.Minutes:00}";
    }

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["days"] = uptime.Days.ToString(),
            ["hours"] = uptime.Hours.ToString("00"),
            ["minutes"] = uptime.Minutes.ToString("00"),
            ["seconds"] = uptime.Seconds.ToString("00"),
            ["total_seconds"] = ((long)uptime.TotalSeconds).ToString(),
            ["output"] = $" {uptime.Days}d {uptime.Hours:00}:{uptime.Minutes:00}"
        };
    }
}
