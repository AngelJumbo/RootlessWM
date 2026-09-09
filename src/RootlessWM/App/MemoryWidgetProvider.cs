using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class MemoryWidgetProvider : IWidgetProvider
{
    private readonly SystemMetricsSampler _metricsSampler;

    public MemoryWidgetProvider(SystemMetricsSampler metricsSampler)
    {
        _metricsSampler = metricsSampler;
    }

    public string Key => "memory";

    public string GetText() => $" {_metricsSampler.SampleMemoryUsagePercent():0}%";

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var percent = _metricsSampler.SampleMemoryUsagePercent();
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["used_percent"] = percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            ["output"] = $" {percent:0}%"
        };
    }
}
