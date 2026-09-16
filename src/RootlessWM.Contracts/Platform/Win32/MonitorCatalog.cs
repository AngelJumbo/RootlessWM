using System.ComponentModel;
using System.Runtime.InteropServices;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class MonitorCatalog
{
    private static readonly HashSet<string> TaskbarWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd"
    };

    public nint GetMonitorHandle(nint windowHandle)
    {
        return NativeMethods.MonitorFromWindow(windowHandle, NativeMethods.MonitorDefaultToNull);
    }

    public nint GetNearestMonitorHandle(nint windowHandle)
    {
        return NativeMethods.MonitorFromWindow(windowHandle, NativeMethods.MonitorDefaultToNearest);
    }

    public nint GetCursorMonitorHandle()
    {
        return NativeMethods.GetCursorPos(out var cursorPosition)
            ? NativeMethods.MonitorFromPoint(cursorPosition, NativeMethods.MonitorDefaultToNearest)
            : nint.Zero;
    }

    public IReadOnlyList<MonitorWorkArea> GetWorkAreas()
    {
        var monitors = new List<MonitorWorkArea>();
        var monitorsWithVisibleTaskbars = GetMonitorsWithVisibleTaskbars();
        var enumerated = NativeMethods.EnumDisplayMonitors(
            nint.Zero,
            nint.Zero,
            (monitor, _, _, _) =>
            {
                var monitorInfo = new NativeMethods.MonitorInfo
                {
                    Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>()
                };
                if (NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var monitorBounds = ToBounds(monitorInfo.Monitor);
                    var reservedWorkArea = ToBounds(monitorInfo.Work);
                    monitors.Add(new MonitorWorkArea(
                        monitor,
                        MonitorWorkAreaResolver.Resolve(
                            monitorBounds,
                            reservedWorkArea,
                            monitorsWithVisibleTaskbars.Contains(monitor)),
                        (monitorInfo.Flags & NativeMethods.MonitorInfofPrimary) != 0));
                }

                return true;
            },
            nint.Zero);

        if (!enumerated)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumDisplayMonitors failed.");
        }

        return monitors;
    }

    private static HashSet<nint> GetMonitorsWithVisibleTaskbars()
    {
        var monitorHandles = new HashSet<nint>();
        _ = NativeMethods.EnumWindows(
            (windowHandle, _) =>
            {
                if (NativeMethods.IsWindowVisible(windowHandle)
                    && TaskbarWindowClasses.Contains(GetClassName(windowHandle)))
                {
                    var monitor = NativeMethods.MonitorFromWindow(
                        windowHandle,
                        NativeMethods.MonitorDefaultToNull);
                    if (monitor != nint.Zero)
                    {
                        monitorHandles.Add(monitor);
                    }
                }

                return true;
            },
            nint.Zero);
        return monitorHandles;
    }

    private static string GetClassName(nint windowHandle)
    {
        var className = new System.Text.StringBuilder(64);
        _ = NativeMethods.GetClassName(windowHandle, className, className.Capacity);
        return className.ToString();
    }

    private static WindowBounds ToBounds(NativeMethods.Rect rectangle)
    {
        return new WindowBounds(
            rectangle.Left,
            rectangle.Top,
            rectangle.Right - rectangle.Left,
            rectangle.Bottom - rectangle.Top);
    }
}
