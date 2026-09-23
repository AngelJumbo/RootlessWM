using System.Drawing;
using System.Text.Json.Serialization;
using RootlessWM.Domain;

namespace RootlessWM.App;

public sealed record RootlessWMSettings(
    int OuterGap,
    int InnerGap,
    IReadOnlyDictionary<string, string>? Hotkeys = null,
    [property: JsonPropertyName("StatusBar")] WorkspaceBarSettings? WorkspaceBar = null,
    ToggleExplorerBehaviour ToggleExplorerBehaviour = ToggleExplorerBehaviour.TaskbarOnly,
    RunnerSettings? Runner = null,
    bool HideExplorerOnStart = false,
    IReadOnlyList<string>? ExcludedExecutables = null,
    bool FocusFollowsMouse = true,
    IReadOnlyList<LaunchHotkeySettings>? Launch = null,
    LayoutsSettings? Layouts = null)
{
    public static RootlessWMSettings Default { get; } = new(0, 0);

    public MasterStackLayoutOptions ToLayoutOptions()
    {
        var layouts = Layouts ?? LayoutsSettings.Empty;
        if (!Enum.TryParse<MasterStackLayoutMode>(layouts.Default, true, out var mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Layouts), layouts.Default, "The layout mode is not supported.");
        }

        var options = new MasterStackLayoutOptions(layouts.MasterRatio, OuterGap, InnerGap, mode, layouts.MasterCount);
        options.Validate();
        return options;
    }

    public IReadOnlyList<MasterStackLayoutMode> ToLayoutCycleOrder()
    {
        var names = Layouts?.Enabled;
        if (names is null || names.Count == 0)
        {
            return LayoutCatalog.DefaultCycleOrder;
        }

        var seen = new HashSet<MasterStackLayoutMode>();
        var order = new List<MasterStackLayoutMode>(names.Count);
        foreach (var name in names)
        {
            if (!Enum.TryParse<MasterStackLayoutMode>(name, true, out var mode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Layouts),
                    name,
                    $"Unknown layout '{name}' in layouts.enabled. Valid values: {string.Join(", ", Enum.GetNames<MasterStackLayoutMode>())}.");
            }

            if (!seen.Add(mode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Layouts),
                    name,
                    $"Duplicate layout '{name}' in layouts.enabled.");
            }

            order.Add(mode);
        }

        return order;
    }

    internal void ValidateRunnerColors()
    {
        if (Runner is null)
        {
            return;
        }

        var style = Runner.Style ?? RunnerStyleSettings.Default;
        ParseColor(style.Background, nameof(style.Background));
        ParseColor(style.Color, nameof(style.Color));
        ParseColor(style.BorderColor, nameof(style.BorderColor));

        var input = Runner.Input ?? RunnerInputSettings.Default;
        ParseColor(input.Background, nameof(input.Background));
        ParseColor(input.Color, nameof(input.Color));
        ParseColor(input.PlaceholderColor, nameof(input.PlaceholderColor));

        var results = Runner.Results ?? RunnerResultsSettings.Default;
        ParseColor(results.Background, nameof(results.Background));
        ParseColor(results.Color, nameof(results.Color));
        ParseColor(results.SelectedBackground, nameof(results.SelectedBackground));
        ParseColor(results.SelectedColor, nameof(results.SelectedColor));
        ParseColor(results.SecondaryColor, nameof(results.SecondaryColor));
    }

    public WorkspaceBarOptions ToWorkspaceBarOptions()
    {
        var bar = WorkspaceBar ?? WorkspaceBarSettings.Default;
        var thickness = bar.Thickness ?? bar.Height;
        if (thickness is < 16 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(WorkspaceBar), "The workspace bar height/thickness must be between 16 and 64.");
        }

        if (!Enum.TryParse<WorkspaceBarPosition>(bar.Position ?? "Top", true, out var position))
        {
            throw new ArgumentOutOfRangeException(nameof(WorkspaceBar), bar.Position, "The workspace bar position must be top, bottom, left, or right.");
        }

        var style = ParseStyle(bar.Style, ParseColor(bar.Background, nameof(bar.Background)));
        var defaultModules = bar.Modules is null;
        var modules = defaultModules ? DefaultModules() : bar.Modules!;
        var moduleOptions = modules.ToDictionary(
            pair => pair.Key,
            pair => BuildModuleOption(pair.Key, pair.Value, style),
            StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<WorkspaceBarModuleOptions> BuildModules(IReadOnlyList<string>? ids)
            => (ids ?? []).Where(moduleOptions.ContainsKey).Select(id => moduleOptions[id]).ToList();

        return new WorkspaceBarOptions(
            bar.Visible,
            thickness,
            ParseColor(bar.Background, nameof(bar.Background)),
            style,
            BuildModules(defaultModules ? ["workspaces", "layout"] : bar.ModulesLeft),
            BuildModules(defaultModules ? ["window-title"] : bar.ModulesCenter),
            BuildModules(defaultModules ? ["datetime"] : bar.ModulesRight),
            position);
    }

    // Modules used when the configuration defines no [module.*] tables, so the bar still shows
    // workspaces, the layout symbol, the focused window title, and the time out of the box.
    private static IReadOnlyDictionary<string, WorkspaceBarModuleSettings> DefaultModules()
        => new Dictionary<string, WorkspaceBarModuleSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["workspaces"] = new(),
            ["layout"] = new(),
            ["window-title"] = new(),
            ["datetime"] = new()
        };

    private static WorkspaceBarModuleOptions BuildModuleOption(string id, WorkspaceBarModuleSettings module, WorkspaceBarStyleOptions barStyle)
    {
        if (!Enum.TryParse<WorkspaceBarModuleMonitor>(module.Monitor ?? "all", true, out var monitor))
        {
            throw new ArgumentOutOfRangeException(nameof(module.Monitor), module.Monitor, "The status bar module monitor must be all, primary, or focused.");
        }

        var type = module.Type ?? id;
        var style = ParseModuleStyle(module.Style, barStyle);
        if (string.Equals(type, "workspaces", StringComparison.OrdinalIgnoreCase) && module.Style?.Spacing is null)
        {
            style = style with { Spacing = 4 };
        }

        return new WorkspaceBarModuleOptions(
            id,
            type,
            monitor,
            style,
            module.Format ?? DefaultModuleFormat(type),
            module.Text,
            module.Command,
            Math.Clamp(module.IntervalMilliseconds, 250, 3600000),
            module.Labels,
            ParseLayoutSymbols(module.Symbols),
            module.Symbol,
            module.ActiveBackground is null ? null : ParseColor(module.ActiveBackground, nameof(module.ActiveBackground)),
            module.ActiveForeground is null ? null : ParseColor(module.ActiveForeground, nameof(module.ActiveForeground)),
            string.Equals(type, "battery", StringComparison.OrdinalIgnoreCase) ? module.Symbols : null,
            module.OnClick ?? DefaultModuleOnClick(type));
    }

    private static string? DefaultModuleFormat(string type)
        => type.ToLowerInvariant() switch
        {
            "cpu" => "{percent}%",
            "memory" => "{used_percent}%",
            "datetime" => "{hours}:{minutes}:{seconds}",
            "uptime" => "{days}d {hours}:{minutes}",
            "battery" => "{output}",
            "disk" => "{output}",
            "network" => "{output}",
            "command" => "{output}",
            _ => null
        };

    private static string? DefaultModuleOnClick(string type)
        => string.Equals(type, "workspaces", StringComparison.OrdinalIgnoreCase) ? "workspace" : null;

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

    private static WorkspaceBarStyleOptions ParseStyle(WorkspaceBarStyleSettings? style, Color fallbackBackground)
        => ParseStyle(style, WorkspaceBarStyleOptions.Default with { Background = fallbackBackground });

    private static WorkspaceBarStyleOptions ParseModuleStyle(WorkspaceBarStyleSettings? style, WorkspaceBarStyleOptions barStyle)
    {
        style ??= new WorkspaceBarStyleSettings();
        return ParseStyle(
            style with
            {
                Color = style.Color ?? ColorTranslator.ToHtml(barStyle.Foreground),
                Background = style.Background ?? "#00000000",
                BorderWidth = style.BorderWidth ?? 0,
                BorderColor = style.BorderColor ?? "#00000000",
                Radius = style.Radius ?? 0,
                PaddingTop = style.PaddingTop ?? 0,
                PaddingRight = style.PaddingRight ?? 0,
                PaddingBottom = style.PaddingBottom ?? 0,
                PaddingLeft = style.PaddingLeft ?? 0,
                MarginTop = style.MarginTop ?? 0,
                MarginRight = style.MarginRight ?? 0,
                MarginBottom = style.MarginBottom ?? 0,
                MarginLeft = style.MarginLeft ?? 0,
                Spacing = style.Spacing ?? 0,
                FontFamily = style.FontFamily ?? barStyle.FontFamily,
                FontSize = style.FontSize ?? barStyle.FontSize,
                Weight = style.Weight ?? (barStyle.FontStyle.HasFlag(FontStyle.Bold) ? "bold" : "normal"),
                Italic = style.Italic ?? barStyle.FontStyle.HasFlag(FontStyle.Italic)
            },
            WorkspaceBarStyleOptions.Default with
            {
                Foreground = barStyle.Foreground,
                FontFamily = barStyle.FontFamily,
                FontSize = barStyle.FontSize,
                FontStyle = barStyle.FontStyle
            }) with
        { Foreground = barStyle.Foreground };
    }

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
            style.MinWidth ?? fallback.MinWidth,
            style.MaxWidth ?? fallback.MaxWidth,
            style.Visible ?? fallback.Visible,
            style.MaxLength ?? fallback.MaxLength);
    }

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
    int? Thickness = null,
    string? Position = null,
    string Background = "#101010",
    WorkspaceBarStyleSettings? Style = null,
    IReadOnlyList<string>? ModulesLeft = null,
    IReadOnlyList<string>? ModulesCenter = null,
    IReadOnlyList<string>? ModulesRight = null,
    IReadOnlyDictionary<string, WorkspaceBarModuleSettings>? Modules = null)
{
    public static WorkspaceBarSettings Default { get; } = new();
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
    int? MinWidth = null,
    int? MaxWidth = null,
    bool? Visible = null,
    int? MaxLength = null);

public sealed record WorkspaceBarModuleSettings(
    string? Type = null,
    string? Monitor = null,
    string? Format = null,
    int IntervalMilliseconds = 5000,
    string? Text = null,
    string? Command = null,
    IReadOnlyList<string>? Labels = null,
    IReadOnlyDictionary<string, string>? Symbols = null,
    string? Symbol = null,
    string? ActiveBackground = null,
    string? ActiveForeground = null,
    string? OnClick = null,
    WorkspaceBarStyleSettings? Style = null);

public sealed record RunnerSettings(
    bool Enabled = true,
    int MaxResults = 8,
    string Matching = "fuzzy",
    IReadOnlyList<string>? Sources = null,
    RunnerWindowSettings Window = null!,
    RunnerStyleSettings Style = null!,
    RunnerInputSettings Input = null!,
    RunnerResultsSettings Results = null!,
    RunnerIconsSettings Icons = null!)
{
    public static RunnerSettings Default { get; } = new(
        Window: RunnerWindowSettings.Default,
        Style: RunnerStyleSettings.Default,
        Input: RunnerInputSettings.Default,
        Results: RunnerResultsSettings.Default,
        Icons: RunnerIconsSettings.Default);
}

public sealed record RunnerWindowSettings(
    string Position = "top-center",
    int Width = 720,
    int MaxHeight = 560,
    int OffsetX = 0,
    int OffsetY = 96)
{
    public static RunnerWindowSettings Default { get; } = new();
}

public sealed record RunnerStyleSettings(
    string Background = "#1e1e2e",
    string Color = "#cdd6f4",
    string BorderColor = "#45475a",
    int BorderWidth = 1,
    int Radius = 6,
    int? PaddingTop = null,
    int? PaddingRight = null,
    int? PaddingBottom = null,
    int? PaddingLeft = null,
    string FontFamily = "Cascadia Mono",
    float FontSize = 12,
    string? Weight = null,
    bool? Italic = null)
{
    public static RunnerStyleSettings Default { get; } = new();
}

public sealed record RunnerInputSettings(
    string Prompt = "run",
    string Background = "#313244",
    string Color = "#cdd6f4",
    string PlaceholderColor = "#6c7086",
    int Height = 36)
{
    public static RunnerInputSettings Default { get; } = new();
}

public sealed record RunnerResultsSettings(
    int RowHeight = 40,
    int Spacing = 4,
    string Background = "#1e1e2e",
    string Color = "#cdd6f4",
    string SelectedBackground = "#cba6f7",
    string SelectedColor = "#1e1e2e",
    string SecondaryColor = "#a6adc8")
{
    public static RunnerResultsSettings Default { get; } = new();
}

public sealed record RunnerIconsSettings(
    bool Visible = true,
    int Size = 24,
    int Padding = 8)
{
    public static RunnerIconsSettings Default { get; } = new();
}

