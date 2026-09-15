namespace RootlessWM.Protocol;

public sealed record ManagementOptionsPayload(
    double MasterRatio,
    int OuterGap,
    int InnerGap,
    string LayoutMode,
    int MasterCount,
    IReadOnlyList<string> ExcludedExecutables,
    IReadOnlyList<string> ExcludedWindowClasses,
    bool FocusFollowsMouse,
    string ToggleExplorerBehaviour);
