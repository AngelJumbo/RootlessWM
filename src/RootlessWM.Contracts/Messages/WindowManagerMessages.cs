using RootlessWM.Domain;

namespace RootlessWM.Contracts.Messages;

public enum WindowManagerCommand
{
    ExecuteCommand,
    SetMouseFocusSuspended,
    GetStatus,
    ReloadSettings,
    Shutdown
}

public sealed record WindowManagerRequest(
    WindowManagerCommand Command,
    TilingCommand? TilingCommand = null,
    bool? Suspended = null);

public sealed record WindowManagerResponse(
    bool Success,
    string? Error = null,
    WindowManagerStatus? Status = null);

public sealed record WindowManagerStatus(
    bool ManagementEnabled,
    int CurrentWorkspace,
    int WorkspaceCount,
    double MasterRatio,
    int OuterGap,
    int InnerGap,
    int ManagedWindowCount,
    int MasterCount,
    IReadOnlyList<MonitorBarState>? Monitors = null);

public sealed record MonitorBarState(long MonitorHandle, int CurrentWorkspace, string LayoutMode);
