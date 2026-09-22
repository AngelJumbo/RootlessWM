# Keybindings

Hotkeys support modifier combinations using `Alt`, `Ctrl`, `Shift`, and `Win`/`Super`, alongside letters, digits, `Space`, `Escape`, and `Enter`/`Return`.

## Default Bindings

| Action | Shortcut | Description |
| :--- | :--- | :--- |
| `FocusNext` | `Alt+J` | Move focus to the next window. |
| `FocusPrevious` | `Alt+K` | Move focus to the previous window. |
| `SwapWithNext` | `Alt+Shift+J` | Swap the active window with the next window. |
| `SwapWithPrevious` | `Alt+Shift+K` | Swap the active window with the previous window. |
| `PromoteToMaster` | `Alt+M` | Move the focused window into the master position. |
| `Close` | `Alt+Q` | Close the active window. |
| `CycleLayout` | `Alt+Space` | Cycle forward through `layouts.enabled`. |
| `CycleLayoutPrevious` | `Alt+Shift+Space` | Cycle backward through `layouts.enabled`. |
| `MaximizeWindow` | `Alt+Shift+F` | Toggle fullscreen mode. |
| `ToggleFloating` | `Alt+F` | Toggle tiling on the active window. |
| `IncreaseMasterRatio` / `DecreaseMasterRatio` | `Alt+L` / `Alt+H` | Adjust the master-area ratio. |
| `IncreaseMasterCount` / `DecreaseMasterCount` | `Alt+I` / `Alt+D` | Adjust the number of master windows. |
| `IncreaseOuterGap` / `DecreaseOuterGap` | `Alt+O` / `Alt+U` | Adjust screen-border gaps. |
| `IncreaseInnerGap` / `DecreaseInnerGap` | `Alt+Shift+O` / `Alt+Shift+U` | Adjust spacing between tiled windows. |
| `SelectWorkspace1`–`9` | `Alt+1` through `Alt+9` | Switch workspace. |
| `MoveToWorkspace1`–`9` | `Alt+Shift+1` through `Alt+Shift+9` | Move the active window to a workspace. |
| `FocusNextMonitor` / `FocusPreviousMonitor` | `Alt+Shift+L` / `Alt+Shift+H` | Shift focus across displays. |
| `MoveToNextMonitor` / `MoveToPreviousMonitor` | `Alt+Ctrl+L` / `Alt+Ctrl+H` | Move a window across displays. |
| `OpenRunner` | `Alt+P` | Open the application runner. |
| `ToggleExplorer` | `Alt+E` | Toggle Windows Explorer chrome/taskbar visibility. |
| Disable management | `Alt+Shift+Esc` | Disable management and restore saved bounds. |
| Enable management | `Alt+Shift+R` | Enable management and tile again. |

A shortcut registered by another application is skipped and reported in `management_started`; it does not stop the session.

## Direct Layout Hotkeys

Every layout can optionally be bound to a hotkey that switches to it immediately, without
affecting `layouts.enabled` or the cycling order. Unconfigured entries have no default binding
and register nothing.

```toml
[Hotkeys]
SelectLayoutMasterLeft = "Alt+Shift+M"
SelectLayoutGrid = "Alt+Shift+G"
SelectLayoutDwindle = "Alt+Shift+D"
SelectLayoutCenteredMaster = "Alt+Shift+C"
```

Available actions: `SelectLayoutMasterLeft`, `SelectLayoutMasterTop`, `SelectLayoutMonocle`,
`SelectLayoutFloating`, `SelectLayoutGrid`, `SelectLayoutFibonacci`, `SelectLayoutDwindle`,
`SelectLayoutCenteredMaster`. After a direct selection, `CycleLayout`/`CycleLayoutPrevious`
resume from that layout's position in `layouts.enabled` (or from the start of the list if the
selected layout isn't in it). See [Configuration](docs/configuration.md) for `layouts.enabled`.

## Program Launch Hotkeys

You can bind hotkeys to launch programs in `settings.toml` using `[[launch]]`:

```toml
[[launch]]
hotkey = "Alt+T"
command = "pwsh.exe"
args = "-NoLogo"

[[launch]]
hotkey = "Alt+Shift+T"
command = "pwsh.exe"
admin = true

[[launch]]
hotkey = "Alt+Return"
command = "wt.exe"
```

| Key | Type | Description |
| :--- | :--- | :--- |
| `hotkey` | String | Key combination (e.g. `"Alt+T"`, `"Alt+Return"`). |
| `command` | String | Executable name or file path to run. |
| `args` | String (optional) | Command-line arguments. |
| `working-directory` | String (optional) | Initial working directory. |
| `admin` | Boolean (optional) | Launch with elevated administrator privileges (UAC prompt). |

## See Also

- [Configuration](docs/configuration.md)
- [Application Runner](docs/runner.md)
