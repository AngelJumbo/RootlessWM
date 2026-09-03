namespace RootlessWM.App;

internal sealed class UptimeWidgetProvider : IWidgetProvider
{
    public string Key => "uptime";

    public string GetText()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $" {uptime.Days}d {uptime.Hours:00}:{uptime.Minutes:00}";
    }
}
