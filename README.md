# RootlessWM

RootlessWM is a native Windows tiling window manager inspired by DWM, focused on keyboard-driven window management and predictable layouts.

## Capabilities

### Window Management
- Automatic tiling of eligible top-level Windows applications.
- Master-and-stack window management.
- Automatic handling of newly opened and closed windows.
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

`--manage` starts a persistent multi-monitor session, tiles windows, and restores their saved bounds when it exits. Press `Ctrl+C` to exit. `Alt+J/K` also centers the cursor in the newly focused window. Moving the pointer over a visible managed window focuses it. Default global hotkeys are `Alt+Shift+M` promote master, `Alt+J` focus next, `Alt+K` focus previous, `Alt+L/H` increase/decrease master ratio, `Alt+I/D` increase/decrease master count, `Alt+Shift+O/U` increase/decrease outer gap, `Alt+Shift+I/D` increase/decrease inner gap, `Alt+Shift+L/H` focus next/previous monitor, `Alt+Ctrl+L/H` move to next/previous monitor, `Alt+Shift+J/K` swap with next/previous, `Alt+Shift+N/P` next/previous workspace, `Alt+Ctrl+N/P` move to next/previous workspace, `Alt+1..9` select workspace 1 through 9, `Alt+Shift+1..9` move the active window to workspace 1 through 9, `Alt+T` cycle layout, `Alt+M` toggle fullscreen for the active window, `Alt+F` toggle floating, `Alt+Shift+Q` request close, and `Alt+Space` open the application runner. `Alt+Shift+Esc` disables management and immediately restores saved bounds; `Alt+Shift+R` enables and tiles again. A shortcut that another application has registered is skipped and reported in `management_started`; it does not stop the session. `--no-logs` disables file logging; it can be combined with any mode.

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

Settings live at `%LOCALAPPDATA%\RootlessWM\settings.toml` you can find an example `settings.example.toml` :

```toml
MasterRatio = 0.6
OuterGap = 8
InnerGap = 8
Layout = "MasterLeft"
MasterCount = 1

[StatusBar]
Visible = true
Height = 24
Background = "#101010"

[StatusBar.Workspaces]
Background = "#101010"
Foreground = "#D0D0D0"
CurrentBackground = "#FFFFFF"
CurrentForeground = "#101010"
Symbols = ["1", "2", "3", "4", "5", "6", "7", "8", "9"]

[StatusBar.Layout]
Background = "#101010"
Foreground = "#D0D0D0"

[StatusBar.Layout.Symbols]
MasterLeft = "ML"
MasterTop = "MT"
Monocle = "MON"
Floating = "FLT"

[StatusBar.Title]
Background = "#101010"
CurrentBackground = "#101010"
CurrentForeground = "#FFFFFF"

[StatusBar.Widgets]
Order = ["cpu", "memory", "clock"]

[StatusBar.Widgets.Cpu]
Enabled = true
Symbol = "CPU"
SymbolBackground = "#101010"
SymbolForeground = "#D0D0D0"
ResultBackground = "#101010"
ResultForeground = "#D0D0D0"

[StatusBar.Widgets.Memory]
Enabled = true
Symbol = "MEM"
SymbolBackground = "#101010"
SymbolForeground = "#D0D0D0"
ResultBackground = "#101010"
ResultForeground = "#D0D0D0"

[StatusBar.Widgets.Clock]
Enabled = true
Symbol = ""
SymbolBackground = "#101010"
SymbolForeground = "#D0D0D0"
ResultBackground = "#101010"
ResultForeground = "#D0D0D0"

[Hotkeys]
OpenRunner = "Alt+Space"
PromoteToMaster = "Alt+Shift+M"
FocusNext = "Alt+J"
FocusPrevious = "Alt+K"
SelectWorkspace1 = "Alt+1"
SelectWorkspace2 = "Alt+2"
SelectWorkspace3 = "Alt+3"
SelectWorkspace4 = "Alt+4"
SelectWorkspace5 = "Alt+5"
SelectWorkspace6 = "Alt+6"
SelectWorkspace7 = "Alt+7"
SelectWorkspace8 = "Alt+8"
SelectWorkspace9 = "Alt+9"
MoveToWorkspace1 = "Alt+Shift+1"
MoveToWorkspace2 = "Alt+Shift+2"
MoveToWorkspace3 = "Alt+Shift+3"
MoveToWorkspace4 = "Alt+Shift+4"
MoveToWorkspace5 = "Alt+Shift+5"
MoveToWorkspace6 = "Alt+Shift+6"
MoveToWorkspace7 = "Alt+Shift+7"
MoveToWorkspace8 = "Alt+Shift+8"
MoveToWorkspace9 = "Alt+Shift+9"
IncreaseMasterRatio = "Alt+L"
DecreaseMasterRatio = "Alt+H"
IncreaseMasterCount = "Alt+I"
DecreaseMasterCount = "Alt+D"
IncreaseOuterGap = "Alt+Shift+O"
DecreaseOuterGap = "Alt+Shift+U"
IncreaseInnerGap = "Alt+Shift+I"
DecreaseInnerGap = "Alt+Shift+D"
FocusNextMonitor = "Alt+Shift+L"
FocusPreviousMonitor = "Alt+Shift+H"
MoveToNextMonitor = "Alt+Ctrl+L"
MoveToPreviousMonitor = "Alt+Ctrl+H"
CycleLayout = "Alt+T"
MaximizeWindow = "Alt+M"
ToggleFloating = "Alt+F"
SwapWithNext = "Alt+Shift+J"
SwapWithPrevious = "Alt+Shift+K"
Close = "Alt+Shift+Q"
```

`MasterRatio` must be from `0.05` through `0.95`; `OuterGap` and `InnerGap` must be zero or greater. `OuterGap` is the space between windows and the screen border; `InnerGap` is the space between adjacent windows. `MasterCount` must be from `1` through `9` and sets the initial number of windows in the master area. `Hotkeys` maps a command name to an `Alt`, `Ctrl`, and/or `Shift` binding with a letter, digit, `Space`, or `Escape` key. Invalid settings or individual bindings fall back to defaults. During `--manage`, the tray provides the same core window commands as hotkeys, plus enable, restore, reload-settings, and exit controls.


`StatusBar` configures the optional top bar shown on every monitor. It is visible by default; set `Visible` to `false` to hide it. When visible, `Height` (from `16` through `64`) is reserved from the top of each monitor's tiling work area so managed windows do not overlap it. `Background` sets the overall bar color. The `Workspaces` section colors the workspace-number area (`Background`, `Foreground`) and the active workspace (`CurrentBackground`, `CurrentForeground`), and `Symbols` lists one symbol per workspace (falling back to the number when the list is shorter). The `Layout` section colors the layout indicator and maps each layout mode (`MasterLeft`, `MasterTop`, `Monocle`, `Floating`) to a symbol. The `Title` section colors the centered focused-window title, which is shown only on the focused monitor. The `Widgets` section selects and orders widgets via `Order` (a list of `cpu`, `memory`, `clock`, `date`, `uptime`, `battery`, `disk`, `network`, `text`); each widget has `Enabled`, a `Symbol`, and independent `SymbolBackground`/`SymbolForeground` and `ResultBackground`/`ResultForeground` colors. The `text` widget also accepts a `Text` value to display a static string. Colors accept `#RRGGBB` (opaque) or `#RRGGBBAA`; the final two hex digits are alpha, from `00` (transparent) to `FF` (opaque). Alpha applies independently to bar, section, workspace, and widget colors, allowing windows behind the bar to show through. The RootlessWM tray tooltip always reports management state, current workspace, tiled-window count, master ratio, master count, and inner/outer gap sizes.

`runner` configures the built-in application runner. `enabled` controls whether the runner can open, `max-results` limits visible matches, and `sources` selects any combination of `start-menu`, `app-paths`, and `path`. `window.position` accepts `top-left`, `top-center`, `top-right`, or `center`; `offset-x`, `offset-y`, `width`, and `max-height` control placement and size. `style`, `input`, and `results` control the visual appearance, including font, border, selected-result colors, spacing, and row height. `icons.visible`, `icons.size`, and `icons.padding` control associated Windows icons. Runner colors accept `#RRGGBB` or `#RRGGBBAA`, using the final two digits for alpha. See `settings.example.toml` for a complete runner configuration.

`Layout` accepts `MasterLeft`, `MasterTop`, `Monocle`, or `Floating`. `CycleLayout` advances through these modes for the active workspace and monitor.

`MaximizeWindow` toggles fullscreen for the active tiled window. The window covers the whole monitor with no gaps and no reserved bar space, and the status bar on that monitor is hidden while it is fullscreen. Fullscreen is tracked per monitor and workspace, and is cleared when the window stops being tiled or management is disabled.

Managed workspace assignments are persisted atomically at `%LOCALAPPDATA%\RootlessWM\workspaces.json`. Each monitor starts on workspace 1 for a new managed session because native monitor handles are session-local; only assignments for currently discovered windows are restored, and stale HWNDs are discarded.

## Known Issues

- Some context menus and windows components may be tracked by the window manager and may break the layouts.



