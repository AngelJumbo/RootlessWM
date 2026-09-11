# RootlessWM

RootlessWM is a native Windows tiling window manager inspired by DWM, focused on keyboard-driven window management and predictable layouts.

## Capabilities

### Window Management
- Automatic tiling of eligible top-level Windows applications.
- Master-and-stack window management.
- Automatic handling of newly opened and closed windows.
- Optional borderless tiled windows by disabling native window decorations.
- Fullscreen mode for the active window.
- Mouse movement over a managed window can automatically focus it.
### Layouts

| Layout	| Description |
|-----------|-------------|
| MasterLeft	| Master windows occupy the left side of the monitor, with the remaining windows stacked on the right. |
| MasterTop	 | Master windows occupy the top of the monitor, with the remaining windows stacked below. |
| Monocle	| A single managed window occupies the entire available tiling area.|
| Floating	| Managed windows are left in their existing positions and sizes. |

### Workspaces
- 9 independent workspaces.
- Workspace selection via keyboard shortcuts.
- Move windows between workspaces.
- Each monitor maintains its own active workspace.
### Multi-Monitor
- Independent window layouts on every detected monitor.
- Automatic discovery of monitor work areas.
- Independent workspace state per monitor.

### Status Bar

RootlessWM includes an optional status bar displayed at the top of every monitor.

The bar supports:

- Workspace indicators.
- Active workspace highlighting.
- Current layout indicator.
- Focused-window title.
- CPU usage.
- Memory usage.
- Clock.
- Date.
- System uptime.
- Battery status.
- Disk usage.
- Network information.
- Static text widgets.
- User-defined command widget.
- Configurable widget ordering.
- Transparent backgrounds that allow windows behind the bar to remain visible.

### Built-in Runner

RootlessWM includes a rofi-style application runner. Press `Alt+Space` during a
`--manage` session to open it on the monitor containing the pointer.

The runner:

- Searches Start Menu shortcuts, Windows App Paths, and executable files on `PATH`.
- Filters results as you type.
- Shows the associated Windows application icon when enabled.
- Launches the selected result with `Enter`.
- Supports `Up`/`Down` selection and `Escape` to close.
- Accepts a typed executable, command, or URL when no discovered result is selected.
- Supports configurable position, dimensions, fonts, borders, colors, transparency, row height, and icons.

The runner does not use configured application entries. Its hotkey is a normal
command in the `[Hotkeys]` section, so it can be changed like any other binding.

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

`--diagnostics` enumerates top-level windows, classifications, and discovered monitor work areas. It does not change any window state.

`--watch` listens for top-level window lifecycle changes and writes structured event records to standard output. Press `Ctrl+C` to stop it. This mode tracks and classifies windows only; it does not move, resize, focus, or close any window.

`--tile` applies independent master-and-stack layouts to currently eligible windows on every detected monitor. On a new session, the largest eligible window on each monitor becomes master; the resulting order remains stable while the manager is running. Maximized managed windows are restored before placement; z-order is preserved and every HWND is revalidated immediately before placement. This command changes window bounds.

`--untile` restores the original bounds saved by `--tile`. State is stored at `%LOCALAPPDATA%\RootlessWM\managed-windows.json` before any placement occurs. A pending restore blocks another `--tile` command until recovery completes.

`--manage` starts a persistent multi-monitor session, tiles windows, and restores their saved bounds when it exits. Press `Ctrl+C` to exit. `Alt+J/K` also centers the cursor in the newly focused window. Moving the pointer over a visible managed window focuses it. Default global hotkeys are `Alt+M` promote master, `Alt+J` focus next, `Alt+K` focus previous, `Alt+L/H` increase/decrease master ratio, `Alt+I/D` increase/decrease master count, `Alt+O/U` increase/decrease outer gap, `Alt+Shift+O/U` increase/decrease inner gap, `Alt+Shift+L/H` focus next/previous monitor, `Alt+Ctrl+L/H` move to next/previous monitor, `Alt+Shift+J/K` swap with next/previous, `Alt+Shift+N/P` next/previous workspace, `Alt+Ctrl+N/P` move to next/previous workspace, `Alt+1..9` select workspace 1 through 9, `Alt+Shift+1..9` move the active window to workspace 1 through 9, `Alt+Space` cycle layout, `Alt+Shift+F` maximize the active window, `Alt+F` toggle floating, `Alt+Q` request close, `Alt+E` toggle Explorer, and `Alt+P` open the application runner. `Alt+Shift+Esc` disables management and immediately restores saved bounds; `Alt+Shift+R` enables and tiles again. A shortcut that another application has registered is skipped and reported in `management_started`; it does not stop the session. `--no-logs` disables file logging; it can be combined with any mode.

## Local Install

For a personal daily-driver installation, run this from the repository root:

```powershell
.\scripts\Install-Local.ps1
```

The script publishes a self-contained `win-x64` executable to `artifacts\publish`, installs it in `%LOCALAPPDATA%\RootlessWM\app`, creates a startup shortcut that runs `RootlessWM.exe --manage --no-logs` at sign-in, and launches it immediately. Your settings, workspace state, and saved restore state remain in `%LOCALAPPDATA%\RootlessWM` across updates.

Exit RootlessWM from the tray before updating, then run the same installer command again. To install without startup registration or without launching immediately, pass `-NoStartup` or `-NoLaunch`.

To remove the installed binaries and startup shortcut while preserving settings/state:

```powershell
.\scripts\Uninstall-Local.ps1
```

Pass `-RemoveData` to also delete `%LOCALAPPDATA%\RootlessWM`, including settings and pending restore data.

## Settings

This project is still work in progress, some updates may change.

Configuration files are located at `%LOCALAPPDATA%\RootlessWM\settings.toml`. A complete reference template is available in `settings.example.toml`. All keys are optional; omitted values automatically fall back to their system defaults.

#### General Window Manager Options

| Key | Type / Format | Default | Description |
| :--- | :--- | :--- | :--- |
| `master-ratio` | Float (`0.05` \u2013 `0.95`) | `0.6` | Proportion of screen width assigned to the master area. |
| `outer-gap` | Integer ($\\ge 0$) | `8` | Margin (in pixels) between windows and the screen edges. |
| `inner-gap` | Integer ($\\ge 0$) | `8` | Margin (in pixels) between adjacent tiled windows. |
| `layout` | String | `"MasterLeft"` | Starting layout mode (`"MasterLeft"`, `"MasterTop"`, `"Monocle"`, `"Floating"`). |
| `master-count` | Integer (`1` \u2013 `9`) | `1` | Initial number of windows allocated to the master area. |
| `toggle-explorer-behaviour` | String | `"TaskbarOnly"` | Scope of elements hidden by `ToggleExplorer` (`"TaskbarOnly"`, `"TaskbarAndDesktopIcons"`, `"TaskbarWallpaperAndDesktopIcons"`). |
| `hide-explorer-on-start` | Boolean | `false` | When true, hides the selected Explorer components immediately on startup. |
| `excluded-executables` | Array of strings | `["Taskmgr"]` | Process names (without `.exe`) kept floating to avoid failed retile attempts. |

#### Status Bar Configuration (`[statusbar]`)

The status bar accepts colors in `#RRGGBB` (opaque) or `#RRGGBBAA` (with alpha channel, `00` being transparent and `FF` opaque).

| Key | Type / Format | Description |
| :--- | :--- | :--- |
| `visible` | Boolean | Enables or disables the bar across all monitors. |
| `height` | Integer (`16` \u2013 `64`) | Reserved height from the top of the monitor's tiling area. |
| `background` | Hex Color | Base bar background color. |
| `border` | Table (`width`, `color`) | Border line thickness and color. |
| `radius` | Integer | Corner rounding radius in pixels. |
| `padding` | Table (`top`, `bottom`, `left`, `right`) | Outer padding around the bar content. |
| `spacing` | Integer | Pixel spacing between modules. |
| `font` | Table (`family`, `size`, `weight`) | Global font properties for the bar text. |

#### Status Bar Modules

The sections/widgets syntax was replaced by ordered module lists:

```toml
modules-left = ["workspaces", "layout"]
modules-center = ["window-title"]
modules-right = ["cpu", "memory", "clock"]

[module.cpu]
type = "cpu"
monitor = "primary"
format = "CPU {percent}%"
```

Module tables are top-level `[module.<id>]` tables. Supported module types are `workspaces`, `layout`, `window-title`, `cpu`, `memory`, `clock`, `date`, `uptime`, `battery`, `disk`, `network`, `text`, and `command`.

`monitor` defaults to `all`; `primary` shows only on the primary monitor and `focused` only on the focused monitor. Only `color` and `font` inherit from `[statusbar]`. Module `background`, `padding`, `margin`, `radius`, and `border` default independently to transparent, zero, zero, zero, and no border.

`format` is supported by data modules and `command`. For the `battery` module, `symbols` configures state glyphs and `charging` configures the charging glyph; the default charging glyph is `⚡`.

#### Built-in Module Values and Formats

| Module | Format behavior | Default output / format | Supported values |
| :--- | :--- | :--- | :--- |
| `workspaces` | No `format` | Labels from `labels`, or `1` through `9` | Active state uses `active-bg` and `active-fg`. |
| `layout` | No `format` | Configured `symbols` value, or built-in abbreviation | `MasterLeft`, `MasterTop`, `monocle`, `floating`. |
| `window-title` | No `format` | Focused window title | `max-width` limits the title. Empty when unfocused. |
| `cpu` | Yes | `{percent}%` | `{percent}` |
| `memory` | Yes | `{used_percent}%` | `{used_percent}` |
| `clock` | Yes | `{:%H:%M:%S}` | `{:%...}` date/time syntax. |
| `date` | Yes | `{:%Y-%m-%d}` | `{:%...}` date/time syntax. |
| `uptime` | Yes | `{days}d {hours}:{minutes}` | `{days}`, `{hours}`, `{minutes}`, `{seconds}`, `{total_seconds}` |
| `battery` | Yes | `{percent}%{charging}` | `{percent}`, `{battery_symbol}`, `{charging}`, `{ac_status}`. `battery_symbol` selects `batteryEmpty`, `batteryQuarter`, `batteryHalf`, `batteryThreeQuarters`, or `batteryFull`. Empty when no battery exists. |
| `disk` | Yes | `{output}` | `{root}`, `{used_percent}`, `{used_bytes}`, `{free_bytes}`, `{total_bytes}`, `{output}` |
| `network` | Yes | `{output}` | `{download_bps}`, `{upload_bps}`, `{download_rate}`, `{upload_rate}`, `{output}` |
| `text` | No `format` | Literal `text` value | Configured `text`, rendered unchanged. |
| `command` | Yes | `{output}` | `{output}`, containing trimmed command output. |

Date and clock formats use `{:%...}` with the supported date/time tokens. Numeric values use invariant formatting. Unknown placeholders remain unchanged.

This is a breaking configuration change. Existing `[[statusbar.sections]]` and `[[statusbar.widgets]]` settings are deprecated; migrate them using `settings.example.toml`.

#### Application Runner (`[runner]`)

The built-in launcher provides quick access to installed executables.

| Sub-table | Key | Default / Format | Description |
| :--- | :--- | :--- | :--- |
| `[runner]` | `enabled` | `true` | Enables or disables the quick-launch runner. |
| | `max-results` | `8` | Maximum matching entries rendered. |
| | `matching` | `"fuzzy"` | Search matching algorithm. |
| | `sources` | Array | Index sources (`"start-menu"`, `"app-paths"`, `"path"`). |
| `[runner.window]` | `position` | `"top-center"` | Placement (`top-left`, `top-center`, `top-right`, `center`). |
| | `width`, `max-height` | `720`, `560` | Overall launcher window dimensions in pixels. |
| | `offset-x`, `offset-y` | `0`, `96` | Pixel offsets relative to the target screen anchor. |
| `[runner.style]` | `background`, `color`, `border-color`, `radius`, `font-*` | Style settings | Visual container styling; supports `#RRGGBBAA` alpha transparency. |
| `[runner.input]` | `prompt`, `height`, `placeholder-color` | Input settings | Input bar layout, placeholder text, and field height. |
| `[runner.results]` | `row-height`, `spacing`, `selected-background` | Results list | Spacing and highlight colors for hovered/active entries. |
| `[runner.icons]` | `visible`, `size`, `padding` | Icon settings | Toggles Windows executable icons alongside result text. |

#### Default Hotkey Bindings (`[Hotkeys]`)

Hotkeys support combinations of modifier keys (`Alt`, `Ctrl`, `Shift`, `Win`/`Super`) alongside letters, digits, `Space`, `Escape` or `Enter`/`Return`.

| Action | Shortcut | Description |
| :--- | :--- | :--- |
| **Window Navigation** | | |
| `FocusNext` | `Alt+J` | Move focus to next window in stack. |
| `FocusPrevious` | `Alt+K` | Move focus to previous window in stack. |
| `SwapWithNext` | `Alt+Shift+J` | Swap active window with the next window. |
| `SwapWithPrevious` | `Alt+Shift+K` | Swap active window with the previous window. |
| `PromoteToMaster` | `Alt+M` | Move the focused window into the master position. |
| `Close` | `Alt+Q` | Close active window. |
| **Layout & Windows** | | |
| `CycleLayout` | `Alt+Space` | Cycle through master, monocle, and floating modes. |
| `MaximizeWindow` | `Alt+Shift+F` | Toggle fullscreen mode (removes gaps and bar space). |
| `ToggleFloating` | `Alt+F` | Toggle tiling on active window. |
| `IncreaseMasterRatio` / `DecreaseMasterRatio` | `Alt+L` / `Alt+H` | Expand or shrink master window width ratio. |
| `IncreaseMasterCount` / `DecreaseMasterCount` | `Alt+I` / `Alt+D` | Adjust number of windows in the master region. |
| `IncreaseOuterGap` / `DecreaseOuterGap` | `Alt+O` / `Alt+U` | Adjust screen border gaps. |
| `IncreaseInnerGap` / `DecreaseInnerGap` | `Alt+Shift+O` / `Alt+Shift+U` | Adjust spacing between tiled windows. |
| **Workspaces & Monitors** | | |
| `SelectWorkspace1` \u2013 `9` | `Alt+1` through `Alt+9` | Switch to workspace 1\u20139. |
| `MoveToWorkspace1` \u2013 `9` | `Alt+Shift+1` through `Alt+Shift+9` | Move active window to workspace 1\u20139. |
| `FocusNextMonitor` / `FocusPreviousMonitor` | `Alt+Shift+L` / `Alt+Shift+H` | Shift focus across displays. |
| `MoveToNextMonitor` / `MoveToPreviousMonitor` | `Alt+Ctrl+L` / `Alt+Ctrl+H` | Move window to next or previous display. |
| **System & Shell** | | |
| `OpenRunner` | `Alt+P` | Launch the application search prompt. |
| `ToggleExplorer` | `Alt+E` | Toggle visibility of Windows Explorer chrome / taskbar. |



Managed workspace assignments are persisted atomically at `%LOCALAPPDATA%\RootlessWM\workspaces.json`. Each monitor starts on workspace 1 for a new managed session because native monitor handles are session-local; only assignments for currently discovered windows are restored, and stale HWNDs are discarded.

## Known Issues

- Some context menus and windows components may be tracked by the window manager and may break the layouts.



