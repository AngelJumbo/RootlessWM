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
}
