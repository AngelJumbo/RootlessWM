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
}
