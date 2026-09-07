using System.Text;

namespace RootlessWM.Platform.Win32;

/// <summary>
/// Shows/hides the Windows shell chrome (desktop icons via Progman, and all taskbars)
/// without stopping Explorer, so tray icons keep working while the chrome is hidden.
/// Inspired by dwm-win32's MOD+E toggle.
/// </summary>
internal sealed class ExplorerVisibilityController
{
    private static readonly HashSet<string> TaskbarWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd"
    };

    /// <summary>Flips shell chrome visibility. Returns true when the chrome is now visible.</summary>
    public bool Toggle()
    {
        var handles = GetShellWindowHandles();
        var currentlyVisible = handles.Any(NativeMethods.IsWindowVisible);
        SetVisibility(handles, !currentlyVisible);
        return !currentlyVisible;
    }

    /// <summary>Forces the shell chrome back on. Used on management disable and process exit.</summary>
    public void EnsureVisible()
    {
        SetVisibility(GetShellWindowHandles(), visible: true);
    }

    private static List<nint> GetShellWindowHandles()
    {
        var handles = new List<nint>();
        var progman = NativeMethods.FindWindow("Progman", "Program Manager");
        if (progman != nint.Zero)
        {
            handles.Add(progman);
        }

        // One Shell_SecondaryTrayWnd exists per secondary monitor, so enumerate rather than FindWindow.
        _ = NativeMethods.EnumWindows((windowHandle, _) =>
        {
            var className = new StringBuilder(64);
            _ = NativeMethods.GetClassName(windowHandle, className, className.Capacity);
            if (TaskbarWindowClasses.Contains(className.ToString()))
            {
                handles.Add(windowHandle);
            }

            return true;
        }, nint.Zero);
        return handles;
    }

    private static void SetVisibility(IReadOnlyList<nint> handles, bool visible)
    {
        foreach (var handle in handles)
        {
            _ = NativeMethods.SetWindowPos(
                handle,
                nint.Zero,
                0,
                0,
                0,
                0,
                (visible ? NativeMethods.SwpShowWindow : NativeMethods.SwpHideWindow)
                    | NativeMethods.SwpNoActivate
                    | NativeMethods.SwpNoMove
                    | NativeMethods.SwpNoSize
                    | NativeMethods.SwpNoZOrder);
        }
    }
}
