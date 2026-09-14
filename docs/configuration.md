# Configuration

RootlessWM configuration files are located at `%LOCALAPPDATA%/RootlessWM/settings.toml`. A complete reference template is available in `settings.example.toml`. All keys are optional; omitted values fall back to their system defaults.

> This project is still work in progress, and updates may change configuration behavior.

## General Window Manager Options

| Key | Type / Format | Default | Description |
| :--- | :--- | :--- | :--- |
| `master-ratio` | Float (`0.05`–`0.95`) | `0.6` | Proportion of screen width assigned to the master area. |
| `outer-gap` | Integer (≥ 0) | `8` | Margin in pixels between windows and screen edges. |
| `inner-gap` | Integer (≥ 0) | `8` | Margin in pixels between adjacent tiled windows. |
| `layout` | String | `"MasterLeft"` | Starting layout: `MasterLeft`, `MasterTop`, `Monocle`, or `Floating`. |
| `master-count` | Integer (`1`–`9`) | `1` | Initial number of windows allocated to the master area. |
| `toggle-explorer-behaviour` | String | `"TaskbarOnly"` | Explorer elements hidden by `ToggleExplorer`: `TaskbarOnly`, `TaskbarAndDesktopIcons`, or `TaskbarWallpaperAndDesktopIcons`. |
| `hide-explorer-on-start` | Boolean | `false` | Hides the selected Explorer components immediately on startup. |
| `excluded-executables` | Array of strings | `["Taskmgr"]` | Process names without `.exe` that remain floating. |

## Related Configuration

- [Application Runner](runner.md)
- [Status Bar](statusbar.md)
- [Keybindings](keybindings.md)

## Persistent State

Managed workspace assignments are persisted atomically at `%LOCALAPPDATA%/RootlessWM/workspaces.json`. Each monitor starts on workspace 1 for a new managed session because native monitor handles are session-local. Only assignments for currently discovered windows are restored; stale window handles are discarded.
