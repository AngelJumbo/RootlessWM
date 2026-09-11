using System.Runtime.InteropServices;

namespace RootlessWM.Platform.Win32;

internal sealed class WindowDecorationController
{
    public bool Disable(nint windowHandle)
    {
        var style = unchecked((uint)NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlStyle).ToInt64());
        var undecoratedStyle = style & ~(NativeMethods.WsCaption | NativeMethods.WsThickFrame);
        if (undecoratedStyle == style)
        {
            return true;
        }

        var previousStyle = NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeMethods.GwlStyle,
            new nint(unchecked((long)undecoratedStyle)));
        if (previousStyle == nint.Zero && Marshal.GetLastWin32Error() != 0)
        {
            return false;
        }

        return NativeMethods.SetWindowPos(
            windowHandle,
            nint.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder |
            NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
    }
}