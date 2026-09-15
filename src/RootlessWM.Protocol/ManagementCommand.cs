using System.Text.Json.Serialization;

namespace RootlessWM.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "commandType")]
[JsonDerivedType(typeof(ApplyManagementOptionsCommand), nameof(ManagementCommandType.ApplyManagementOptions))]
[JsonDerivedType(typeof(ExecuteTilingCommandCommand), nameof(ManagementCommandType.ExecuteTilingCommand))]
[JsonDerivedType(typeof(ToggleExplorerVisibilityCommand), nameof(ManagementCommandType.ToggleExplorerVisibility))]
[JsonDerivedType(typeof(SetMouseFocusSuspendedCommand), nameof(ManagementCommandType.SetMouseFocusSuspended))]
[JsonDerivedType(typeof(SetSessionLockedCommand), nameof(ManagementCommandType.SetSessionLocked))]
[JsonDerivedType(typeof(ResyncCommand), nameof(ManagementCommandType.Resync))]
[JsonDerivedType(typeof(RequestSnapshotCommand), nameof(ManagementCommandType.RequestSnapshot))]
[JsonDerivedType(typeof(ShutdownCommand), nameof(ManagementCommandType.Shutdown))]
public abstract record ManagementCommand(RequestId RequestId, [property: JsonIgnore] ManagementCommandType CommandType);

public sealed record ApplyManagementOptionsCommand(RequestId RequestId, ManagementOptionsPayload Options)
    : ManagementCommand(RequestId, ManagementCommandType.ApplyManagementOptions);

public sealed record ExecuteTilingCommandCommand(RequestId RequestId, ManagementTilingCommand Command, long FocusedWindowHandle)
    : ManagementCommand(RequestId, ManagementCommandType.ExecuteTilingCommand);

public sealed record ToggleExplorerVisibilityCommand(RequestId RequestId)
    : ManagementCommand(RequestId, ManagementCommandType.ToggleExplorerVisibility);

public sealed record SetMouseFocusSuspendedCommand(RequestId RequestId, bool Suspended)
    : ManagementCommand(RequestId, ManagementCommandType.SetMouseFocusSuspended);

public sealed record SetSessionLockedCommand(RequestId RequestId, bool Locked)
    : ManagementCommand(RequestId, ManagementCommandType.SetSessionLocked);

public sealed record ResyncCommand(RequestId RequestId)
    : ManagementCommand(RequestId, ManagementCommandType.Resync);

public sealed record RequestSnapshotCommand(RequestId RequestId)
    : ManagementCommand(RequestId, ManagementCommandType.RequestSnapshot);

public sealed record ShutdownCommand(RequestId RequestId)
    : ManagementCommand(RequestId, ManagementCommandType.Shutdown);
