using System.Diagnostics;
using System.Text;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class WindowInspector
{
    public WindowCandidate Inspect(nint windowHandle)
    {
        if (!TryInspect(windowHandle, out var window))
        {
            throw new InvalidOperationException($"Window handle 0x{windowHandle.ToInt64():X} is no longer valid.");
        }

        return window;
    }

    public bool TryInspect(nint windowHandle, out WindowCandidate window)
    {
        window = default!;
        if (!NativeMethods.IsWindow(windowHandle))
        {
            return false;
        }

        var style = unchecked((uint)NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlStyle).ToInt64());
        var extendedStyle = unchecked((uint)NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle).ToInt64());
        var processId = GetProcessId(windowHandle);

        window = new WindowCandidate(
            windowHandle,
            NativeMethods.IsWindowVisible(windowHandle),
            (style & NativeMethods.WsChild) != 0,
            (extendedStyle & NativeMethods.WsExToolWindow) != 0,
            NativeMethods.IsIconic(windowHandle),
            GetClassName(windowHandle),
            GetProcessName(processId),
            GetBounds(windowHandle),
            style,
            extendedStyle,
            IsCloaked(windowHandle),
            NativeMethods.GetWindow(windowHandle, NativeMethods.GwOwner) != nint.Zero);
        return true;
    }

    private static bool IsCloaked(nint windowHandle)
    {
        var result = NativeMethods.DwmGetWindowAttribute(
            windowHandle,
            NativeMethods.DwmwaCloaked,
            out var cloaked,
            sizeof(uint));
        return result == 0 && cloaked != 0;
    }

    private static string GetClassName(nint windowHandle)
    {
        var className = new StringBuilder(256);
        _ = NativeMethods.GetClassName(windowHandle, className, className.Capacity);
        return className.ToString();
    }

    private static uint GetProcessId(nint windowHandle)
    {
        _ = NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        return processId;
    }

    private static WindowBounds GetBounds(nint windowHandle)
    {
        if (!NativeMethods.GetWindowRect(windowHandle, out var rectangle))
        {
            return default;
        }

        return new WindowBounds(
            rectangle.Left,
            rectangle.Top,
            rectangle.Right - rectangle.Left,
            rectangle.Bottom - rectangle.Top);
    }

    private static string? GetProcessName(uint processId)
    {
        if (processId == 0 || processId > int.MaxValue)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
