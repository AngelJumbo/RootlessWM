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
| `CycleLayout` | `Alt+Space` | Cycle through layout modes. |
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

## See Also

- [Configuration](configuration.md)
- [Application Runner](runner.md)
