using System.Net.NetworkInformation;

namespace RootlessWM.Platform.Win32;

internal sealed class NetworkMetricsSampler
{
    private ulong _previousIn;
    private ulong _previousOut;
    private DateTime _previousTime;
    private bool _hasPreviousSample;

    public NetworkRate? SampleRates()
    {
        ulong inBytes = 0;
        ulong outBytes = 0;
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var stats = networkInterface.GetIPv4Statistics();
            inBytes += (ulong)stats.BytesReceived;
            outBytes += (ulong)stats.BytesSent;
        }

        var now = DateTime.UtcNow;
        if (!_hasPreviousSample)
        {
            _previousIn = inBytes;
            _previousOut = outBytes;
            _previousTime = now;
            _hasPreviousSample = true;
            return new NetworkRate(0, 0);
        }

        var elapsed = (now - _previousTime).TotalSeconds;
        var inDelta = inBytes >= _previousIn ? inBytes - _previousIn : 0;
        var outDelta = outBytes >= _previousOut ? outBytes - _previousOut : 0;
        _previousIn = inBytes;
        _previousOut = outBytes;
        _previousTime = now;

        if (elapsed <= 0)
        {
            return new NetworkRate(0, 0);
        }

        return new NetworkRate(inDelta / elapsed, outDelta / elapsed);
    }
}

internal readonly record struct NetworkRate(double DownloadBytesPerSecond, double UploadBytesPerSecond);
