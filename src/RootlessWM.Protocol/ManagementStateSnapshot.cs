namespace RootlessWM.Protocol;

public readonly record struct WindowBoundsSnapshot(int Left, int Top, int Width, int Height);

public sealed record ManagedWindowSnapshot(long Handle, int WorkspaceIndex, bool IsFloating, bool IsFullscreen, WindowBoundsSnapshot Bounds);

public sealed record ManagementStateSnapshot(
    StateRevision Revision,
    bool IsManagementEnabled,
    int CurrentWorkspace,
    int WorkspaceCount,
    double MasterRatio,
    int OuterGap,
    int InnerGap,
    int MasterCount,
    IReadOnlyList<ManagedWindowSnapshot> ManagedWindows);
