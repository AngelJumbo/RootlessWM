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
dotnet run --project src/RootlessWM -- --manage
dotnet run --project src/RootlessWM -- --manage --no-logs
dotnet format --verify-no-changes
```

### `--diagnostics`

Enumerates top-level windows, classifications, and discovered monitor work areas. It does not change any window state.

### `--watch`

Listens for top-level window lifecycle changes and writes structured event records to standard output. Press `Ctrl+C` to stop it. This mode tracks and classifies windows only; it does not move, resize, focus, or close any window.

### `--manage`

Starts a persistent multi-monitor session, tiles windows, and restores their saved bounds when it exits. Press `Ctrl+C` to exit.

`dotnet run --project src/RootlessWM -- --manage` builds both the interface and its window-manager helper. Exit any running or installed RootlessWM instance first: the interface can otherwise connect to that instance's already-running helper instead of starting the newly built one.

Original window bounds and workspace assignments live only in the window-manager process's memory. No `managed-windows.json` or `workspaces.json` files are read or written. Each new process starts with fresh workspace state; crashes, forced termination, and reboots lose restore information. The standalone `--tile` and `--untile` commands have been removed; use `--manage` instead.

After exiting an older version cleanly, existing state files in `%LOCALAPPDATA%/RootlessWM` can be deleted; the new version ignores them.

During a managed session, moving the pointer over a visible managed window focuses it by default. Set `focus-follows-mouse = false` to disable this behavior. `Alt+Shift+Esc` disables management and immediately restores saved bounds; `Alt+Shift+R` enables management and tiles again.

Locking and unlocking the workstation while management is disabled leaves window visibility unchanged. Workspace hiding resumes only when management is enabled again.

### `--no-logs`

Disables file logging. It can be combined with any mode.

## See Also

- [Configuration](configuration.md)
