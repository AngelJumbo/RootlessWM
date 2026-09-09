using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class BatteryWidgetProvider : IWidgetProvider
{
    private readonly BatteryMetricsSampler _batterySampler;

    public BatteryWidgetProvider(BatteryMetricsSampler batterySampler)
    {
        _batterySampler = batterySampler;
    }

    public string Key => "battery";

    public string GetText()
    {
        var status = _batterySampler.Sample();
        if (status is null)
        {
            return string.Empty;
        }

        var percent = status.Value.BatteryLifePercent;
        if (percent == 255)
        {
            return string.Empty;
        }

        var charging = status.Value.ACLineStatus == 1 ? " ⚡" : string.Empty;
        return $" {percent}%{charging}";
    }

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var status = _batterySampler.Sample();
        if (status is null || status.Value.BatteryLifePercent == 255)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var percent = status.Value.BatteryLifePercent.ToString();
        var charging = status.Value.ACLineStatus == 1 ? " ⚡" : string.Empty;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = percent,
            ["charging"] = charging,
            ["ac_status"] = status.Value.ACLineStatus == 1 ? "online" : "offline",
            ["output"] = $" {percent}%{charging}"
        };
    }
}
