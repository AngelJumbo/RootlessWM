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
