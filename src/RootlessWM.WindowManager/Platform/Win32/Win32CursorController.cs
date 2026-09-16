using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class Win32CursorController
{
    public bool CenterOn(nint windowHandle)
    {
        if (!NativeMethods.GetWindowRect(windowHandle, out var bounds))
        {
            return false;
        }

        var centerX = bounds.Left + ((bounds.Right - bounds.Left) / 2);
        var centerY = bounds.Top + ((bounds.Bottom - bounds.Top) / 2);
        return NativeMethods.SetCursorPos(centerX, centerY);
    }
}
