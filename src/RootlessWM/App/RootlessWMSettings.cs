using System.Drawing;
using System.Text.Json.Serialization;
using RootlessWM.Domain;

namespace RootlessWM.App;

public sealed record RootlessWMSettings(
    double MasterRatio,
    int OuterGap,
    int InnerGap,
    IReadOnlyDictionary<string, string>? Hotkeys = null,
    string Layout = "MasterLeft",
    int MasterCount = 1,
    [property: JsonPropertyName("StatusBar")] WorkspaceBarSettings? WorkspaceBar = null,
    ToggleExplorerBehaviour ToggleExplorerBehaviour = ToggleExplorerBehaviour.TaskbarOnly)
{
    public static RootlessWMSettings Default { get; } = new(0.55, 0, 0);

    public MasterStackLayoutOptions ToLayoutOptions()
    {
        if (!Enum.TryParse<MasterStackLayoutMode>(Layout, true, out var mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Layout), "The layout mode is not supported.");
        }

        var options = new MasterStackLayoutOptions(MasterRatio, OuterGap, InnerGap, mode, MasterCount);
        options.Validate();
        return options;
    }

    public WorkspaceBarOptions ToWorkspaceBarOptions()
    {
        var bar = WorkspaceBar ?? WorkspaceBarSettings.Default;
        if (bar.Height is < 16 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(WorkspaceBar), "The workspace bar height must be between 16 and 64.");
        }

        var workspaces = bar.Workspaces ?? WorkspaceBarWorkspaceSettings.Default;
        var layout = bar.Layout ?? WorkspaceBarLayoutSettings.Default;
        var title = bar.Title ?? WorkspaceBarTitleSettings.Default;
        var widgets = bar.Widgets ?? WorkspaceBarWidgetsSettings.Default;
        var style = ParseStyle(bar.Style, ParseColor(bar.Background, nameof(bar.Background)));

        return new WorkspaceBarOptions(
            bar.Visible,
            bar.Height,
            ParseColor(bar.Background, nameof(bar.Background)),
            new WorkspaceBarWorkspaceOptions(
                ParseColor(workspaces.Background, nameof(workspaces.Background)),
                ParseColor(workspaces.Foreground, nameof(workspaces.Foreground)),
                ParseColor(workspaces.CurrentBackground, nameof(workspaces.CurrentBackground)),
                ParseColor(workspaces.CurrentForeground, nameof(workspaces.CurrentForeground)),
                workspaces.Symbols ?? []),
            new WorkspaceBarLayoutOptions(
                ParseColor(layout.Background, nameof(layout.Background)),
                ParseColor(layout.Foreground, nameof(layout.Foreground)),
                ParseLayoutSymbols(layout.Symbols)),
            new WorkspaceBarTitleOptions(
                ParseColor(title.Background, nameof(title.Background)),
                ParseColor(title.CurrentBackground, nameof(title.CurrentBackground)),
                ParseColor(title.CurrentForeground, nameof(title.CurrentForeground))),
            BuildWidgetOptions(widgets, style),
            style,
            BuildSections(bar.Sections, style));
    }

    private static IReadOnlyDictionary<MasterStackLayoutMode, string> ParseLayoutSymbols(
        IReadOnlyDictionary<string, string>? symbols)
    {
        var result = new Dictionary<MasterStackLayoutMode, string>();
        if (symbols is null)
        {
            return result;
        }

        foreach (var (key, value) in symbols)
        {
            if (Enum.TryParse<MasterStackLayoutMode>(key, true, out var mode))
            {
                result[mode] = value;
            }
        }

        return result;
    }

    private static IReadOnlyList<WorkspaceBarWidgetOptions> BuildWidgetOptions(WorkspaceBarWidgetsSettings widgets, WorkspaceBarStyleOptions barStyle)
    {
        var configured = widgets.Widgets ?? new Dictionary<string, WorkspaceBarWidgetSettings>(StringComparer.OrdinalIgnoreCase);
        var order = widgets.Order ?? ["cpu", "memory", "clock"];
        var options = new List<WorkspaceBarWidgetOptions>();
        foreach (var id in order)
        {
            if (!configured.TryGetValue(id, out var widget))
            {
                widget = new WorkspaceBarWidgetSettings();
            }

            if (!widget.Enabled)
            {
                continue;
            }

            var symbolBackground = ParseColor(widget.SymbolBackground, nameof(widget.SymbolBackground));
            var symbolForeground = ParseColor(widget.SymbolForeground, nameof(widget.SymbolForeground));
            var resultBackground = ParseColor(widget.ResultBackground, nameof(widget.ResultBackground));
            var resultForeground = ParseColor(widget.ResultForeground, nameof(widget.ResultForeground));
            options.Add(new WorkspaceBarWidgetOptions(
                widget.Kind ?? id,
                widget.Symbol,
                symbolBackground,
                symbolForeground,
                resultBackground,
                resultForeground,
                widget.Text,
                widget.Command,
                Math.Clamp(widget.IntervalMilliseconds, 250, 3600000),
                ParseStyle(widget.Style, barStyle with { Background = resultBackground, Foreground = resultForeground })));
            options[^1] = options[^1] with { Id = id };
        }

        return options;
    }

    private static IReadOnlyList<WorkspaceBarSectionOptions> BuildSections(
        IReadOnlyList<WorkspaceBarSectionSettings>? sections,
        WorkspaceBarStyleOptions barStyle)
    {
        if (sections is null || sections.Count == 0)
        {
            return [
                new("workspaces", WorkspaceBarSectionAlignment.Left, barStyle with { Alignment = WorkspaceBarSectionAlignment.Left }, []),
                new("layout", WorkspaceBarSectionAlignment.Left, barStyle with { Alignment = WorkspaceBarSectionAlignment.Left }, []),
                new("title", WorkspaceBarSectionAlignment.Center, barStyle with { Alignment = WorkspaceBarSectionAlignment.Center }, []),
                new("widgets", WorkspaceBarSectionAlignment.Right, barStyle with { Alignment = WorkspaceBarSectionAlignment.Right }, [])
            ];
        }

        return sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Id))
            .Select(section => new WorkspaceBarSectionOptions(
                section.Id,
                ParseAlignment(section.Align),
                ParseStyle(section.Style, barStyle) with { Alignment = ParseAlignment(section.Align) },
                section.Widgets ?? []))
            .ToList();
    }

    private static WorkspaceBarStyleOptions ParseStyle(WorkspaceBarStyleSettings? style, Color fallbackBackground)
        => ParseStyle(style, WorkspaceBarStyleOptions.Default with { Background = fallbackBackground });

    private static WorkspaceBarStyleOptions ParseStyle(WorkspaceBarStyleSettings? style, WorkspaceBarStyleOptions fallback)
    {
        style ??= new WorkspaceBarStyleSettings();
        var fontStyle = FontStyle.Regular;
        if (style.Weight is null)
        {
            fontStyle |= fallback.FontStyle & FontStyle.Bold;
        }
        else if (string.Equals(style.Weight, "bold", StringComparison.OrdinalIgnoreCase))
        {
            fontStyle |= FontStyle.Bold;
        }

        if (style.Italic ?? fallback.FontStyle.HasFlag(FontStyle.Italic))
        {
            fontStyle |= FontStyle.Italic;
        }

        return new WorkspaceBarStyleOptions(
            ParseColor(style.Color ?? ColorTranslator.ToHtml(fallback.Foreground), nameof(style.Color)),
            ParseColor(style.Background ?? ColorTranslator.ToHtml(fallback.Background), nameof(style.Background)),
            style.BorderWidth ?? fallback.BorderWidth,
            ParseColor(style.BorderColor ?? ColorTranslator.ToHtml(fallback.BorderColor), nameof(style.BorderColor)),
            Math.Max(0, style.Radius ?? 0),
            new Padding(
                style.PaddingLeft ?? 0,
                style.PaddingTop ?? 0,
                style.PaddingRight ?? 0,
                style.PaddingBottom ?? 0),
            new Padding(
                style.MarginLeft ?? 0,
                style.MarginTop ?? 0,
                style.MarginRight ?? 0,
                style.MarginBottom ?? 0),
            style.Spacing ?? fallback.Spacing,
            style.FontFamily ?? fallback.FontFamily,
            style.FontSize ?? fallback.FontSize,
            fontStyle,
            style.Align is null ? fallback.Alignment : ParseAlignment(style.Align),
            style.MinWidth ?? fallback.MinWidth,
            style.MaxWidth ?? fallback.MaxWidth,
            style.Visible ?? fallback.Visible);
    }

    private static WorkspaceBarSectionAlignment ParseAlignment(string value)
        => Enum.TryParse<WorkspaceBarSectionAlignment>(value, true, out var alignment)
            ? alignment
            : WorkspaceBarSectionAlignment.Left;

    private static Color ParseColor(string value, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, propertyName);
        if (value.Length == 9 && value[0] == '#' &&
            byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red) &&
            byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green) &&
            byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue) &&
            byte.TryParse(value.AsSpan(7, 2), System.Globalization.NumberStyles.HexNumber, null, out var alpha))
        {
            return Color.FromArgb(alpha, red, green, blue);
        }

        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new ArgumentOutOfRangeException(propertyName, value, "Color values must be valid HTML hex colors (#RRGGBB or #RRGGBBAA).");
        }
    }
}

public sealed record WorkspaceBarSettings(
    bool Visible = true,
    int Height = 24,
    string Background = "#101010",
    WorkspaceBarWorkspaceSettings? Workspaces = null,
    WorkspaceBarLayoutSettings? Layout = null,
    WorkspaceBarTitleSettings? Title = null,
    WorkspaceBarWidgetsSettings? Widgets = null,
    WorkspaceBarStyleSettings? Style = null,
    IReadOnlyList<WorkspaceBarSectionSettings>? Sections = null)
{
    public static WorkspaceBarSettings Default { get; } = new();
}

public sealed record WorkspaceBarWorkspaceSettings(
    string Background = "#101010",
    string Foreground = "#D0D0D0",
    string CurrentBackground = "#FFFFFF",
    string CurrentForeground = "#101010",
    IReadOnlyList<string>? Symbols = null)
{
    public static WorkspaceBarWorkspaceSettings Default { get; } = new();
}

public sealed record WorkspaceBarLayoutSettings(
    string Background = "#101010",
    string Foreground = "#D0D0D0",
    IReadOnlyDictionary<string, string>? Symbols = null)
{
    public static WorkspaceBarLayoutSettings Default { get; } = new();
}

public sealed record WorkspaceBarTitleSettings(
    string Background = "#101010",
    string CurrentBackground = "#101010",
    string CurrentForeground = "#FFFFFF")
{
    public static WorkspaceBarTitleSettings Default { get; } = new();
}

public sealed record WorkspaceBarWidgetsSettings(
    IReadOnlyList<string>? Order = null,
    IReadOnlyDictionary<string, WorkspaceBarWidgetSettings>? Widgets = null)
{
    public static WorkspaceBarWidgetsSettings Default { get; } = new();
}

public sealed record WorkspaceBarWidgetSettings(
    bool Enabled = true,
    string? Kind = null,
    string Symbol = "",
    string SymbolBackground = "#101010",
    string SymbolForeground = "#D0D0D0",
    string ResultBackground = "#101010",
    string ResultForeground = "#D0D0D0",
    string Text = "",
    string Command = "",
    int IntervalMilliseconds = 5000,
    WorkspaceBarStyleSettings? Style = null)
{
    public static WorkspaceBarWidgetSettings Default { get; } = new();
}

public sealed record WorkspaceBarStyleSettings(
    string? Color = null,
    string? Background = null,
    int? BorderWidth = null,
    string? BorderColor = null,
    int? Radius = null,
    int? PaddingTop = null,
    int? PaddingRight = null,
    int? PaddingBottom = null,
    int? PaddingLeft = null,
    int? MarginTop = null,
    int? MarginRight = null,
    int? MarginBottom = null,
    int? MarginLeft = null,
    int? Spacing = null,
    string? FontFamily = null,
    float? FontSize = null,
    string? Weight = null,
    bool? Italic = null,
    string? Align = null,
    int? MinWidth = null,
    int? MaxWidth = null,
    bool? Visible = null);

public sealed record WorkspaceBarSectionSettings(
    string Id,
    string Align = "left",
    WorkspaceBarStyleSettings? Style = null,
    IReadOnlyList<string>? Widgets = null);
