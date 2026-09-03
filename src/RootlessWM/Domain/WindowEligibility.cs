namespace RootlessWM.Domain;

public enum WindowEligibility
{
    Managed,
    NotVisible,
    Cloaked,
    InvalidBounds,
    ChildWindow,
    ToolWindow,
    Tooltip,
    Popup,
    Minimized,
    ShellWindow,
    SystemWindow,
    ExcludedExecutable,
    ExcludedWindowClass
}
