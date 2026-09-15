namespace RootlessWM.Protocol;

public enum ManagementCommandType
{
    ApplyManagementOptions,
    ExecuteTilingCommand,
    ToggleExplorerVisibility,
    SetMouseFocusSuspended,
    SetSessionLocked,
    Resync,
    RequestSnapshot,
    Shutdown
}
