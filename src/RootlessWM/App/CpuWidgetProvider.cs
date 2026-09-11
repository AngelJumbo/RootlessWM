using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class CpuWidgetProvider : IWidgetProvider
{
    private readonly SystemMetricsSampler _metricsSampler;
    private DateTimeOffset _lastSampleTime;
    private double _lastPercent;

    public CpuWidgetProvider(SystemMetricsSampler metricsSampler)
    {
        _metricsSampler = metricsSampler;
    }

    public string Key => "cpu";

    public string GetText() => FormatOutput(GetPercent());

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var percent = GetPercent();
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["percent"] = percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            ["output"] = FormatOutput(percent)
        };
    }

    private double GetPercent()
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastSampleTime).TotalMilliseconds < 750)
        {
            return _lastPercent;
        }

        _lastPercent = _metricsSampler.SampleCpuUsagePercent();
        _lastSampleTime = now;
        return _lastPercent;
    }

    private static string FormatOutput(double percent)
        => $"{percent,3:0}%";
}
