# Application Runner

RootlessWM includes a rofi-style application runner. Press `Alt+P` during a `--manage` session to open it on the monitor containing the pointer.

## Features

- Searches Start Menu shortcuts, Windows App Paths, and executable files on `PATH`.
- Filters results as you type.
- Shows the associated Windows application icon when enabled.
- Launches the selected result with `Enter`.
- Supports `Up`/`Down` selection.
- Closes with `Escape`, an outside click, or another RootlessWM keybinding.
- Accepts a typed executable, command, or URL when no discovered result is selected.
- Supports configurable position, dimensions, fonts, borders, colors, transparency, row height, and icons.

Application discovery starts in the background when runner settings are first applied. Typing filters an in-memory catalog instead of rescanning directories and the registry on every keypress. Reopening the runner refreshes the catalog in the background while retaining existing results; changing its sources rebuilds it. During initial loading, the runner shows `Loading applications...` and preserves typed input until results arrive. Discovery failures are logged and displayed explicitly.

Associated icons are loaded asynchronously and cached in memory for the runner's lifetime. Rows appear immediately; icons fill in when ready. Closing RootlessWM releases the cached images. No runner cache files are written.

## Configuration (`[runner]`)

| Sub-table | Key | Default / Format | Description |
| :--- | :--- | :--- | :--- |
| `[runner]` | `enabled` | `true` | Enables or disables the quick-launch runner. |
| | `max-results` | `8` | Maximum matching entries rendered. |
| | `matching` | `"fuzzy"` | Search matching algorithm. |
| | `sources` | Array | Index sources: `start-menu`, `app-paths`, and `path`. |
| `[runner.window]` | `position` | `"top-center"` | Placement: `top-left`, `top-center`, `top-right`, or `center`. |
| | `width`, `max-height` | `720`, `560` | Launcher dimensions in pixels. |
| | `offset-x`, `offset-y` | `0`, `96` | Pixel offsets relative to the target-screen anchor. |
| `[runner.style]` | `background`, `color`, `border-color`, `radius`, `font-*` | Style settings | Visual container styling; supports `#RRGGBBAA` transparency. |
| `[runner.input]` | `prompt`, `height`, `placeholder-color` | Input settings | Prompt, placeholder, and input height. |
| `[runner.results]` | `row-height`, `spacing`, `selected-background` | Results settings | Result spacing and selection colors. |
| `[runner.icons]` | `visible`, `size`, `padding` | Icon settings | Controls Windows executable icons. |

## See Also

- [Configuration](configuration.md)
- [Keybindings](keybindings.md)
