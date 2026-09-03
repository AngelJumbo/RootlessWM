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
}
