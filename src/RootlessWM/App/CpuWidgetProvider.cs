using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class CpuWidgetProvider : IWidgetProvider
{
    private readonly SystemMetricsSampler _metricsSampler;

    public CpuWidgetProvider(SystemMetricsSampler metricsSampler)
    {
        _metricsSampler = metricsSampler;
    }

    public string Key => "cpu";

    public string GetText() => $" {_metricsSampler.SampleCpuUsagePercent():0}%";

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var percent = _metricsSampler.SampleCpuUsagePercent();
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            ["output"] = $" {percent:0}%"
        };
    }
}
