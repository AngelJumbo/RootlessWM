# Configuration

RootlessWM configuration files are located at `%LOCALAPPDATA%/RootlessWM/settings.toml`. A complete reference template is available in `settings.example.toml`. All keys are optional; omitted values fall back to their system defaults.

> This project is still work in progress, and updates may change configuration behavior.

## General Window Manager Options

| Key | Type / Format | Default | Description |
| :--- | :--- | :--- | :--- |
| `outer-gap` | Integer (≥ 0) | `8` | Margin in pixels between windows and screen edges. |
| `inner-gap` | Integer (≥ 0) | `8` | Margin in pixels between adjacent tiled windows. |
| `toggle-explorer-behaviour` | String | `"TaskbarOnly"` | Explorer elements hidden by `ToggleExplorer`: `TaskbarOnly`, `TaskbarAndDesktopIcons`, or `TaskbarWallpaperAndDesktopIcons`. |
| `hide-explorer-on-start` | Boolean | `false` | Hides the selected Explorer components immediately on startup. |
| `excluded-executables` | Array of strings | `["Taskmgr"]` | Process names without `.exe` that remain floating. |
| `disable-window-shadows` | Boolean | `false` | Disables the default Windows drop shadow on managed windows; restored when disabled or on reload. |

## Layouts

All layout settings live under `[Layouts]`.

| Key | Type / Format | Default | Description |
| :--- | :--- | :--- | :--- |
| `default` | String | `"MasterLeft"` | Starting layout: `MasterLeft`, `MasterTop`, `Monocle`, `Floating`, `Grid`, `Fibonacci`, `Dwindle`, or `CenteredMaster`. |
| `master-ratio` | Float (`0.05`–`0.95`) | `0.6` | Proportion of screen width assigned to the master area (`MasterLeft`/`MasterTop` only). |
| `master-count` | Integer (`1`–`9`) | `1` | Initial number of windows allocated to the master area (`MasterLeft`/`MasterTop` only). |
| `enabled` | Array of strings | built-in default order | Single source of truth for `CycleLayout` / `CycleLayoutPrevious`: its order determines the cycle order, and any layout left out of the list cannot be reached by cycling (it can still be selected with a direct hotkey; see [Keybindings](keybindings.md)). |

```toml
[Layouts]
default = "MasterLeft"
master-ratio = 0.6
master-count = 1
enabled = [
    "MasterLeft",
    "MasterTop",
    "Grid",
    "Dwindle",
    "CenteredMaster",
]
```

- Valid values are the same set as `default` above: `MasterLeft`, `MasterTop`, `Monocle`,
  `Floating`, `Grid`, `Fibonacci`, `Dwindle`, `CenteredMaster`.
- An unknown layout name or a duplicate entry in `enabled` is a configuration error.
- Omitting `[Layouts]` (or `enabled`) cycles through the built-in default order: `MasterLeft`,
  `MasterTop`, `Monocle`, `Floating`.

## Related Configuration

- [Application Runner](runner.md)
- [Status Bar](statusbar.md)
- [Keybindings](keybindings.md)

## Persistent State

Managed workspace assignments are persisted atomically at `%LOCALAPPDATA%/RootlessWM/workspaces.json`. Each monitor starts on workspace 1 for a new managed session because native monitor handles are session-local. Only assignments for currently discovered windows are restored; stale window handles are discarded.
