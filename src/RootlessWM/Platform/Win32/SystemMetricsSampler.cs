using System.Runtime.InteropServices;

namespace RootlessWM.Platform.Win32;

internal sealed class SystemMetricsSampler
{
    private ulong _previousIdle;
    private ulong _previousTotal;
    private bool _hasPreviousSample;

    public double SampleCpuUsagePercent()
    {
        if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return 0;
        }

        var idle = ToUInt64(idleTime);
        // Kernel time already includes idle time, per GetSystemTimes documentation.
        var total = ToUInt64(kernelTime) + ToUInt64(userTime);

        if (!_hasPreviousSample)
        {
            _previousIdle = idle;
            _previousTotal = total;
            _hasPreviousSample = true;
            return 0;
        }

        var idleDelta = idle - _previousIdle;
        var totalDelta = total - _previousTotal;
        _previousIdle = idle;
        _previousTotal = total;

        return totalDelta == 0 ? 0 : Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100);
    }

    public double SampleMemoryUsagePercent()
    {
        var status = new NativeMethods.MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>()
        };

        return NativeMethods.GlobalMemoryStatusEx(ref status) ? status.MemoryLoad : 0;
    }

    private static ulong ToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME fileTime)
    {
        return ((ulong)(uint)fileTime.dwHighDateTime << 32) | (uint)fileTime.dwLowDateTime;
    }
}
