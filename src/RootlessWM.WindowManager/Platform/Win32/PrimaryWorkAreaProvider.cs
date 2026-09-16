using System.ComponentModel;
using System.Runtime.InteropServices;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class PrimaryWorkAreaProvider
{
    public WindowBounds GetWorkArea()
    {
        if (!NativeMethods.SystemParametersInfo(NativeMethods.SpiGetWorkArea, 0, out var rectangle, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SystemParametersInfo(SPI_GETWORKAREA) failed.");
        }

        return new WindowBounds(
            rectangle.Left,
            rectangle.Top,
            rectangle.Right - rectangle.Left,
            rectangle.Bottom - rectangle.Top);
    }

    public bool IsOnPrimaryMonitor(nint windowHandle)
    {
        var monitor = NativeMethods.MonitorFromWindow(windowHandle, NativeMethods.MonitorDefaultToNull);
        if (monitor == nint.Zero)
        {
            return false;
        }

        var monitorInfo = new NativeMethods.MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>()
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        return (monitorInfo.Flags & NativeMethods.MonitorInfofPrimary) != 0;
    }
}
