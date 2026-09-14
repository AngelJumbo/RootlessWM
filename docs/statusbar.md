# Status Bar

RootlessWM includes an optional status bar displayed at the top of every monitor.

## Configuration (`[statusbar]`)

Colors use `#RRGGBB` for opaque colors or `#RRGGBBAA` for colors with alpha (`00` transparent, `FF` opaque).

| Key | Type / Format | Description |
| :--- | :--- | :--- |
| `visible` | Boolean | Enables or disables the bar across all monitors. |
| `height` | Integer (`16`–`64`) | Reserved height at the top of the tiling area. |
| `background` | Hex color | Base bar background. |
| `border` | Table (`width`, `color`) | Border thickness and color. |
| `radius` | Integer | Corner radius in pixels. |
| `padding` | Table (`top`, `bottom`, `left`, `right`) | Outer content padding. |
| `spacing` | Integer | Spacing between modules. |
| `font` | Table (`family`, `size`, `weight`) | Global text font properties. |

## Modules

Use ordered module lists:

```toml
modules-left = ["workspaces", "layout"]
modules-center = ["window-title"]
modules-right = ["cpu", "memory", "clock"]

[module.cpu]
type = "cpu"
monitor = "primary"
format = "CPU {percent}%"
```

Supported module types are `workspaces`, `layout`, `window-title`, `cpu`, `memory`, `clock`, `date`, `uptime`, `battery`, `disk`, `network`, `text`, and `command`.

`monitor` defaults to `all`; `primary` shows only on the primary monitor and `focused` only on the focused monitor. Only `color` and `font` inherit from `[statusbar]`. Module `background`, `padding`, `margin`, `radius`, and `border` default independently to transparent, zero, zero, zero, and no border.

## Built-in Module Values and Formats

| Module | Format behavior | Default output / format | Supported values |
| :--- | :--- | :--- | :--- |
| `workspaces` | No `format` | Labels from `labels`, or `1` through `9` | Active state uses `active-bg` and `active-fg`. |
| `layout` | No `format` | Configured `symbols`, or built-in abbreviation | `MasterLeft`, `MasterTop`, `monocle`, `floating`. |
| `window-title` | No `format` | Focused window title | `max-width` limits the title. |
| `cpu` | Yes | `{percent}%` | `{percent}` |
| `memory` | Yes | `{used_percent}%` | `{used_percent}` |
| `clock` | Yes | `{:%H:%M:%S}` | Date/time syntax. |
| `date` | Yes | `{:%Y-%m-%d}` | Date/time syntax. |
| `uptime` | Yes | `{days}d {hours}:{minutes}` | `{days}`, `{hours}`, `{minutes}`, `{seconds}`, `{total_seconds}` |
| `battery` | Yes | `{percent}%{charging}` | `{percent}`, `{battery_symbol}`, `{charging}`, `{ac_status}`. Empty when no battery exists. |
| `disk` | Yes | `{output}` | `{root}`, `{used_percent}`, `{used_bytes}`, `{free_bytes}`, `{total_bytes}`, `{output}` |
| `network` | Yes | `{output}` | `{download_bps}`, `{upload_bps}`, `{download_rate}`, `{upload_rate}`, `{output}` |
| `text` | No `format` | Literal `text` value | Configured `text`, unchanged. |
| `command` | Yes | `{output}` | Trimmed command output. |

For `battery`, `symbols` configures state glyphs and `charging` configures the charging glyph. The default charging glyph is `⚡`. Numeric values use invariant formatting. Unknown placeholders remain unchanged.

## Inline Styling

Module `format` strings and static module text support:

- Syntax: `[c=<color> s=<size> w=<weight> f=<family>]text[/]`
- `[/]` restores the previous style.
- `[[` outputs `[`, and `]]` outputs `]`.

| Attribute | Meaning | Examples |
| :--- | :--- | :--- |
| `c` | Color | `#89b4fa`, `#fab387cc`, `red` |
| `s` | Size in points | `11`, `14` |
| `w` | Weight | `normal`, `bold`, `semibold`, `light`, `100`–`900` |
| `f` | Font family | `"Cascadia Mono"`, `"Segoe UI"` |

```toml
[module.memory]
type = "memory"
format = "[c=#89b4fa s=14 w=bold]MEM[/] {used_percent}%"

[module.cpu]
type = "cpu"
format = "[c=#f38ba8 f=\"Cascadia Code\" w=bold]CPU[/] [c=#a6e3a1 s=11]{percent}%[/]"
```


## See Also

- [Configuration](configuration.md)
