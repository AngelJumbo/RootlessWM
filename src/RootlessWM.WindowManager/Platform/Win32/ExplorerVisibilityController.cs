using System.Text;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

/// <summary>
/// Shows/hides selected Windows shell chrome (taskbar, desktop icons, or wallpaper)
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

    /// <summary>Flips configured shell chrome visibility. Returns true when the chrome is now visible.</summary>
    public bool Toggle(ToggleExplorerBehaviour behaviour)
    {
        var handles = GetShellWindowHandles(behaviour);
        var currentlyVisible = handles.Any(NativeMethods.IsWindowVisible);
        SetVisibility(handles, !currentlyVisible);
        return !currentlyVisible;
    }

    /// <summary>Forces the shell chrome back on. Used on management disable and process exit.</summary>
    public void EnsureVisible()
    {
        SetVisibility(GetShellWindowHandles(ToggleExplorerBehaviour.TaskbarWallpaperAndDesktopIcons), visible: true);
    }

    private static List<nint> GetShellWindowHandles(ToggleExplorerBehaviour behaviour)
    {
        var handles = new List<nint>();
        handles.AddRange(GetTaskbarWindowHandles());

        if (behaviour == ToggleExplorerBehaviour.TaskbarAndDesktopIcons)
        {
            handles.AddRange(GetDesktopIconWindowHandles());
        }

        if (behaviour == ToggleExplorerBehaviour.TaskbarWallpaperAndDesktopIcons)
        {
            var progman = NativeMethods.FindWindow("Progman", "Program Manager");
            if (progman != nint.Zero)
            {
                handles.Add(progman);
            }
        }

        return handles.Distinct().ToList();
    }

    private static List<nint> GetTaskbarWindowHandles()
    {
        var handles = new List<nint>();
        // One Shell_SecondaryTrayWnd exists per secondary monitor, so enumerate rather than FindWindow.
        _ = NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (TaskbarWindowClasses.Contains(GetClassName(windowHandle)))
            {
                handles.Add(windowHandle);
            }

            return true;
        }, nint.Zero);
        return handles;
    }

    private static List<nint> GetDesktopIconWindowHandles()
    {
        var handles = new List<nint>();
        var progman = NativeMethods.FindWindow("Progman", "Program Manager");
        AddDesktopIconWindow(progman, handles);

        _ = NativeMethods.EnumWindows((windowHandle, _) =>
        {
            if (string.Equals(GetClassName(windowHandle), "WorkerW", StringComparison.OrdinalIgnoreCase))
            {
                AddDesktopIconWindow(windowHandle, handles);
            }

            return true;
        }, nint.Zero);
        return handles;
    }

    private static void AddDesktopIconWindow(nint parentHandle, ICollection<nint> handles)
    {
        if (parentHandle == nint.Zero)
        {
            return;
        }

        var shellView = NativeMethods.FindWindowEx(parentHandle, nint.Zero, "SHELLDLL_DefView", null);
        if (shellView == nint.Zero)
        {
            return;
        }

        var iconList = NativeMethods.FindWindowEx(shellView, nint.Zero, "SysListView32", "FolderView");
        handles.Add(iconList == nint.Zero ? shellView : iconList);
    }

    private static string GetClassName(nint windowHandle)
    {
        var className = new StringBuilder(64);
        _ = NativeMethods.GetClassName(windowHandle, className, className.Capacity);
        return className.ToString();
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
