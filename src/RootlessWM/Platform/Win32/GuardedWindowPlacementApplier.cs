using System.ComponentModel;
using System.Runtime.InteropServices;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class GuardedWindowPlacementApplier(
    WindowInspector windowInspector,
    WindowEligibilityClassifier eligibilityClassifier,
    WindowDecorationController? decorationController = null,
    Func<bool>? decorationsEnabled = null) : IWindowPlacementApplier
{
    public PlacementResult Apply(WindowPlacement placement)
    {
        if (!placement.Bounds.IsUsable)
        {
            return PlacementResult.Skipped(placement.Handle, "invalid_bounds");
        }

        if (!windowInspector.TryInspect(placement.Handle, out var candidate))
        {
            return PlacementResult.Skipped(placement.Handle, "invalid_window");
        }

        var eligibility = eligibilityClassifier.Classify(candidate);
        if (eligibility != WindowEligibility.Managed)
        {
            return PlacementResult.Skipped(placement.Handle, $"ineligible_{eligibility}");
        }

        if (decorationsEnabled is not null && !decorationsEnabled() && decorationController is not null
            && !decorationController.Disable(placement.Handle))
        {
            return PlacementResult.Skipped(placement.Handle, "disable_decorations_failed");
        }

        if (NativeMethods.IsZoomed(placement.Handle))
        {
            _ = NativeMethods.ShowWindow(placement.Handle, NativeMethods.SwRestore);
        }

        var bounds = placement.Bounds;
        var placed = NativeMethods.SetWindowPos(
            placement.Handle,
            nint.Zero,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder);

        // Moving across monitors with different DPI makes Windows rescale the window after the
        // first SetWindowPos, so reapply once when the resulting bounds do not match.
        if (placed
            && windowInspector.TryInspect(placement.Handle, out var placedCandidate)
            && placedCandidate.Bounds != bounds)
        {
            placed = NativeMethods.SetWindowPos(
                placement.Handle,
                nint.Zero,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder);
        }

        return placed
            ? PlacementResult.Success(placement.Handle)
            : PlacementResult.Skipped(
                placement.Handle,
                $"set_window_pos_failed_{new Win32Exception(Marshal.GetLastWin32Error()).NativeErrorCode}");
    }
}
