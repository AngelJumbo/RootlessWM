using System.ComponentModel;
using System.Runtime.InteropServices;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class GuardedWindowBoundsRestorer : IWindowBoundsRestorer
{
    public PlacementResult Restore(WindowPlacement placement)
    {
        if (!placement.Bounds.IsUsable)
        {
            return PlacementResult.Skipped(placement.Handle, "invalid_bounds");
        }

        if (!NativeMethods.IsWindow(placement.Handle))
        {
            return PlacementResult.Skipped(placement.Handle, "invalid_window");
        }

        var bounds = placement.Bounds;
        var restored = NativeMethods.SetWindowPos(
            placement.Handle,
            nint.Zero,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder | NativeMethods.SwpShowWindow);

        return restored
            ? PlacementResult.Success(placement.Handle)
            : PlacementResult.Skipped(
                placement.Handle,
                $"restore_window_pos_failed_{new Win32Exception(Marshal.GetLastWin32Error()).NativeErrorCode}");
    }
}
