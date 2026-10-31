# Status Bar

RootlessWM includes an optional status bar displayed on an edge of every monitor.

## Configuration (`[statusbar]`)

Colors use `#RRGGBB` for opaque colors or `#RRGGBBAA` for colors with alpha (`00` transparent, `FF` opaque).

| Key | Type / Format | Description |
| :--- | :--- | :--- |
| `visible` | Boolean | Enables or disables the bar across all monitors. |
| `independent-modules` | Boolean | Default `false`. Hides the global bar background, border, rounded frame, and Windows outline/shadow; modules retain their own styling. |
| `position` | `"top"` \| `"bottom"` \| `"left"` \| `"right"` | Which monitor edge the bar docks to. Defaults to `top`. |
| `height` | Integer (`16`–`64`) | Reserved thickness of the bar: its height when docked `top`/`bottom`, or its width when docked `left`/`right`. `thickness` is accepted as an alias for this key. |
| `background` | Hex color | Base bar background. |
| `border` | Table (`width`, `color`) | Border thickness and color. |
| `radius` | Integer | Corner radius in pixels. |
| `padding` | Table (`top`, `bottom`, `left`, `right`) | Outer content padding. |
| `spacing` | Integer | Spacing between modules. |
| `font` | Table (`family`, `size`, `weight`) | Global text font properties. |

## Modules

For visually independent modules, set:

```toml
[statusbar]
independent-modules = true
```

This overrides global background/border drawing without changing module backgrounds, borders, or radii. Global padding, margins, spacing, group positions, and reserved tiling space remain unchanged. Works on every bar edge. Set it back to `false` to restore the configured global appearance.

Use ordered module lists:

```toml
modules-left = ["workspaces", "layout"]
modules-center = ["window-title"]
modules-right = ["cpu", "memory", "datetime"]

[module.cpu]
type = "cpu"
monitor = "primary"
format = "CPU {percent}%"
```

Supported module types are `workspaces`, `layout`, `window-title`, `cpu`, `memory`, `datetime`, `uptime`, `battery`, `disk`, `network`, `volume`, `brightness`, `text`, and `command`.

Text is always centered within each module, including each line of multiline text. Module groups retain their configured left, center, or right placement.

A module's TOML table name (the `[module.<id>]` key) and its `type` are independent, so multiple
modules can share the same `type` with different formats and styling. For example, to show the
time and date as two separately styled modules:

```toml
modules-right = ["cpu", "memory", "clock", "date"]

[module.clock]
type = "datetime"
monitor = "primary"
format = "{hours}:{minutes}"
font = { family = "Cascadia Mono", size = 13, weight = "normal" }

[module.date]
type = "datetime"
monitor = "primary"
format = "{day}-{month}-{year}"
font = { family = "Cascadia Mono", size = 13, weight = "normal" }
```

## Vertical bars (`position = "left"` or `"right"`)

When the bar is docked to a side edge, modules stack top-to-bottom instead of left-to-right, and `modules-left`/`modules-center`/`modules-right` map to top/middle/bottom placement along that column. Text is centered across the bar's thickness.

The `window-title` module renders one character per line in vertical bars instead of ellipsizing horizontally. Its `max-length` style option gives the maximum character count (default `24`) before the title is truncated with `...`; the same `max-length` also truncates the title in horizontal bars.

Any module or widget text containing a literal newline (`\n`) is rendered as multiple stacked lines in both orientations.

`monitor` defaults to `all`; `primary` shows only on the primary monitor and `focused` only on the focused monitor. Only `color` and `font` inherit from `[statusbar]`. Module `background`, `padding`, `margin`, `radius`, and `border` default independently to transparent, zero, zero, zero, and no border.

## Built-in Module Values and Formats

| Module | Format behavior | Default output / format | Supported values |
| :--- | :--- | :--- | :--- |
| `workspaces` | No `format` | Labels from `labels`, or `1` through `9` | Active state uses `active-bg` and `active-fg`. |
| `layout` | No `format` | Configured `symbols`, or built-in abbreviation | `MasterLeft`, `MasterTop`, `monocle`, `floating`, `grid`, `fibonacci`, `dwindle`, `centeredmaster`. |
| `window-title` | No `format` | Focused window title | `max-length` limits the title (character count). |
| `cpu` | Yes | `{percent}%` | `{percent}` |
| `memory` | Yes | `{used_percent}%` | `{used_percent}` |
| `datetime` | Yes | `{hours}:{minutes}:{seconds}` | `{year}`, `{short_year}`, `{month}`, `{day}`, `{hours}`, `{minutes}`, `{seconds}` |
| `uptime` | Yes | `{days}d {hours}:{minutes}` | `{days}`, `{hours}`, `{minutes}`, `{seconds}`, `{total_seconds}` |
| `battery` | Yes | `{percent}%{charging}` | `{percent}`, `{battery_symbol}`, `{charging}`, `{ac_status}`. Empty when no battery exists. |
| `disk` | Yes | `{output}` | `{root}`, `{used_percent}`, `{used_bytes}`, `{free_bytes}`, `{total_bytes}`, `{output}` |
| `network` | Yes | `{output}` | `{download_bps}`, `{upload_bps}`, `{download_rate}`, `{upload_rate}`, `{output}` |
| `volume` | Yes | `{output}` | `{volume_symbol}`, `{percent}`, `{muted}` (`true`/`false`), `{status}` (`muted` or empty), `{output}` |
| `brightness` | Yes | `{output}` | `{brightness_symbol}`, `{percent}`, `{output}` |
| `text` | No `format` | Literal `text` value | Configured `text`, unchanged. |
| `command` | Yes | `{output}` | Trimmed command output. |

For `battery`, `symbols` configures state glyphs and `charging` configures the charging glyph. The default charging glyph is `⚡`. Numeric values use invariant formatting. Unknown placeholders remain unchanged.

## System Volume

Add `"volume"` to a module list and configure it like any other module:

```toml
# Add "volume" to your existing modules-right list.
[module.volume]
type = "volume"
format = "VOL {output}"
```

Configure `symbols` to select a glyph through the single `{volume_symbol}` placeholder:

```toml
[module.volume]
type = "volume"
format = "{volume_symbol} {percent}%"
symbols = { volumeZero = "\U0001F508", volumeLow = "\U0001F508", volumeMedium = "\U0001F509", volumeHigh = "\U0001F50A", muted = "\U0001F507" }
```

Use any glyph supported by your configured font. `volumeZero` applies at 0%, `volumeLow` at 1–33%, `volumeMedium` at 34–66%, and `volumeHigh` at 67–100%, using the displayed rounded percentage. `muted` overrides every percentage range. Symbol keys are case-insensitive. Missing symbols (or no playback device) produce an empty `{volume_symbol}`; existing `{output}` text is unchanged. Separate volume modules can configure different glyphs.

The module displays the Windows default multimedia playback device's master volume and mute status. Left-click it to toggle mute without changing the volume level. Hover over it and scroll up to increase volume or down to decrease it, by 2 percentage points per wheel notch, limited to 0–100%. Partial wheel deltas accumulate until a full notch. Scrolling does not change mute state. Neither clicking nor scrolling activates the bar. Both interactions work in horizontal and vertical bars, including module padding; no `on-click` configuration is needed for volume.

Device changes and external volume changes are reflected on the next bar refresh (normally once per second); wheel and mute changes refresh immediately. Without an available playback device, the module shows `N/A` and logs the audio error instead of showing a false volume value. In this state, `{percent}` is `N/A`, `{muted}` is `unknown`, and `{status}` is `unavailable`.

## Screen Brightness

Add `"brightness"` to your module list:

```toml
[module.brightness]
type = "brightness"
format = "{brightness_symbol} {percent}%"
font = { family = "FiraCode Nerd Font Mono", size = 12 }
symbols = { brightnessLow = "\U000F00DE", brightnessMedium = "\U000F00DF", brightnessHigh = "\U000F00E0" }
```

The three symbols represent 0–33%, 34–66%, and 67–100%. Keys are case-insensitive; missing symbols render empty. The example uses Nerd Font Material Design brightness 5, 6, and 7 glyphs.

Hover and scroll up/down to adjust the integrated laptop screen by 2 percentage points per notch, limited to 0–100%. Partial wheel movements accumulate. Panels with discrete brightness levels advance to the next supported level in the requested direction. No click action is assigned. Bar orientation and monitor placement do not change the target: all brightness modules control the integrated display, not external monitors.

Wheel changes refresh immediately; Windows/external brightness changes refresh on the next bar tick. If the display/driver does not support WMI brightness, the module displays `N/A` and logs the error. No brightness state is saved to disk.

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
