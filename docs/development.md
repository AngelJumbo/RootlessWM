# Development

## Prerequisites

- Windows 11
- .NET SDK 9.0.305 or a later 9.0 feature-band SDK

## Commands

```powershell
dotnet build
dotnet test
dotnet run --project src/RootlessWM -- --diagnostics
dotnet run --project src/RootlessWM -- --watch
dotnet run --project src/RootlessWM -- --tile
dotnet run --project src/RootlessWM -- --untile
dotnet run --project src/RootlessWM -- --manage
dotnet run --project src/RootlessWM -- --manage --no-logs
dotnet format --verify-no-changes
```

### `--diagnostics`

Enumerates top-level windows, classifications, and discovered monitor work areas. It does not change any window state.

### `--watch`

Listens for top-level window lifecycle changes and writes structured event records to standard output. Press `Ctrl+C` to stop it. This mode tracks and classifies windows only; it does not move, resize, focus, or close any window.

### `--tile`

Applies independent master-and-stack layouts to currently eligible windows on every detected monitor. On a new session, the largest eligible window on each monitor becomes master. Maximized managed windows are restored before placement, and window handles are revalidated immediately before placement. This command changes window bounds.

### `--untile`

Restores the original bounds saved by `--tile`. State is stored at `%LOCALAPPDATA%/RootlessWM/managed-windows.json` before placement occurs. A pending restore blocks another `--tile` command until recovery completes.

### `--manage`

Starts a persistent multi-monitor session, tiles windows, and restores their saved bounds when it exits. Press `Ctrl+C` to exit.

During a managed session, moving the pointer over a visible managed window focuses it by default. Set `focus-follows-mouse = false` to disable this behavior. `Alt+Shift+Esc` disables management and immediately restores saved bounds; `Alt+Shift+R` enables management and tiles again.

### `--no-logs`

Disables file logging. It can be combined with any mode.

## See Also

- [Configuration](configuration.md)
