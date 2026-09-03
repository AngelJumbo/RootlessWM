namespace RootlessWM.Domain;

public sealed class WindowEligibilityClassifier(WindowEligibilityOptions? options = null)
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExNoActivate = 0x08000000;

    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "WorkerW",
        "#32768",
        "Microsoft.UI.Content.PopupWindowSiteBridge"
    };

    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "TextInputHost",
        "SearchHost",
        "StartMenuExperienceHost"
    };

    private readonly WindowEligibilityOptions _options = options ?? new WindowEligibilityOptions();

    public WindowEligibility Classify(WindowCandidate window)
    {
        if (!window.IsVisible)
        {
            return WindowEligibility.NotVisible;
        }

        if (window.IsCloaked)
        {
            return WindowEligibility.Cloaked;
        }

        if (!window.Bounds.IsUsable)
        {
            return WindowEligibility.InvalidBounds;
        }

        if (window.IsChildWindow)
        {
            return WindowEligibility.ChildWindow;
        }

        if (window.IsToolWindow)
        {
            return WindowEligibility.ToolWindow;
        }

        if (window.ClassName.Equals("tooltips_class32", StringComparison.OrdinalIgnoreCase)
            || (window.Style & WsPopup) != 0 && (window.ExtendedStyle & WsExNoActivate) != 0)
        {
            return WindowEligibility.Tooltip;
        }

        // Owned WS_POPUP windows are transient UI (context menus, dropdowns, autocomplete flyouts),
        // not top-level app windows, which are unowned.
        if (window.HasOwner && (window.Style & WsPopup) != 0)
        {
            return WindowEligibility.Popup;
        }

        if (window.IsMinimized)
        {
            return WindowEligibility.Minimized;
        }

        if (ShellWindowClasses.Contains(window.ClassName))
        {
            return WindowEligibility.ShellWindow;
        }

        if (window.ProcessName is not null && SystemProcessNames.Contains(window.ProcessName))
        {
            return WindowEligibility.SystemWindow;
        }

        if (_options.ExcludedWindowClasses.Contains(window.ClassName))
        {
            return WindowEligibility.ExcludedWindowClass;
        }

        if (window.ProcessName is not null && _options.ExcludedExecutableNames.Contains(window.ProcessName))
        {
            return WindowEligibility.ExcludedExecutable;
        }

        return WindowEligibility.Managed;
    }
}
