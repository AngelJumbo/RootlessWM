using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class NetworkWidgetProvider : IWidgetProvider
{
    private readonly NetworkMetricsSampler _networkSampler;

    public NetworkWidgetProvider(NetworkMetricsSampler networkSampler)
    {
        _networkSampler = networkSampler;
    }

    public string Key => "network";

    public string GetText()
    {
        var rates = _networkSampler.SampleRates();
        if (rates is null)
        {
            return string.Empty;
        }

        return $" ↓{FormatRate(rates.Value.DownloadBytesPerSecond)} ↑{FormatRate(rates.Value.UploadBytesPerSecond)}";
    }

    private static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024 * 1024)
        {
            return $"{bytesPerSecond / (1024 * 1024):0.0}MB/s";
        }

        if (bytesPerSecond >= 1024)
        {
            return $"{bytesPerSecond / 1024:0.0}KB/s";
        }

        return $"{bytesPerSecond:0}B/s";
    }
}
