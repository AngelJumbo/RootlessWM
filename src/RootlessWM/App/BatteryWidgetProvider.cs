using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class BatteryWidgetProvider : IWidgetProvider
{
    private readonly BatteryMetricsSampler _batterySampler;
    private readonly string _chargingGlyph;
    private readonly IReadOnlyDictionary<string, string> _batterySymbols;

    public BatteryWidgetProvider(
        BatteryMetricsSampler batterySampler,
        string? chargingGlyph = null,
        IReadOnlyDictionary<string, string>? batterySymbols = null)
    {
        _batterySampler = batterySampler;
        _batterySymbols = batterySymbols ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _chargingGlyph = _batterySymbols.TryGetValue("charging", out var configuredChargingGlyph)
            ? configuredChargingGlyph
            : string.IsNullOrEmpty(chargingGlyph) ? "⚡" : chargingGlyph;
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

        var charging = GetChargingText(status.Value.ACLineStatus);
        return $" {percent}%{FormatChargingSuffix(charging)}";
    }

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var status = _batterySampler.Sample();
        if (status is null || status.Value.BatteryLifePercent == 255)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var percent = status.Value.BatteryLifePercent.ToString();
        var batterySymbol = GetBatterySymbol(status.Value.BatteryLifePercent);
        var charging = GetChargingText(status.Value.ACLineStatus);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = percent,
            ["battery_symbol"] = batterySymbol,
            ["charging"] = charging,
            ["ac_status"] = status.Value.ACLineStatus == 1 ? "online" : "offline",
            ["output"] = $" {percent}%{FormatChargingSuffix(charging)}"
        };
    }

    private string GetBatterySymbol(byte percent)
    {
        var state = percent switch
        {
            0 => "batteryEmpty",
            <= 25 => "batteryQuarter",
            <= 50 => "batteryHalf",
            <= 75 => "batteryThreeQuarters",
            _ => "batteryFull"
        };

        return _batterySymbols.TryGetValue(state, out var symbol) ? symbol : string.Empty;
    }

    private string GetChargingText(byte acLineStatus)
        => acLineStatus == 1 ? _chargingGlyph : string.Empty;

    private static string FormatChargingSuffix(string charging)
        => string.IsNullOrEmpty(charging) ? string.Empty : $" {charging}";
}
