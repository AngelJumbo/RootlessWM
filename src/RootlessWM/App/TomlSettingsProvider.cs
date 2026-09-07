using Tomlyn;
using Tomlyn.Model;
using RootlessWM.Domain;

namespace RootlessWM.App;

internal sealed class TomlSettingsProvider
{
    private readonly string _filePath;
    private readonly JsonSettingsProvider _jsonFallback;

    public TomlSettingsProvider()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RootlessWM",
            "settings.toml"))
    {
    }

    internal TomlSettingsProvider(string filePath)
    {
        _filePath = filePath;
        _jsonFallback = new JsonSettingsProvider(Path.Combine(Path.GetDirectoryName(filePath)!, "settings.json"));
    }

    public RootlessWMSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return _jsonFallback.Load();
        }

        var model = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(_filePath));
        if (model is null)
        {
            return RootlessWMSettings.Default;
        }

        var settings = MapSettings(model);
        _ = settings.ToLayoutOptions();
        _ = settings.ToWorkspaceBarOptions();
        return settings;
    }

    private static RootlessWMSettings MapSettings(TomlTable table)
    {
        return new RootlessWMSettings(
            GetDouble(table, "MasterRatio") ?? 0.55,
            (int)(GetLong(table, "OuterGap") ?? 0),
            (int)(GetLong(table, "InnerGap") ?? 0),
            GetStringMap(table, "Hotkeys"),
            GetString(table, "Layout") ?? "MasterLeft",
            (int)(GetLong(table, "MasterCount") ?? 1),
            MapWorkspaceBar(GetTable(table, "StatusBar")),
            MapToggleExplorerBehaviour(GetString(table, "ToggleExplorerBehaviour") ?? "TaskbarOnly"),
            MapRunner(GetTable(table, "Runner")));
    }

    private static ToggleExplorerBehaviour MapToggleExplorerBehaviour(string value)
        => Enum.TryParse<ToggleExplorerBehaviour>(value, ignoreCase: true, out var behaviour)
            ? behaviour
            : throw new ArgumentOutOfRangeException(nameof(RootlessWMSettings.ToggleExplorerBehaviour), value, "The toggle explorer behaviour is not supported.");

    private static RunnerSettings? MapRunner(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new RunnerSettings(
            GetBool(table, "Enabled") ?? true,
            (int)(GetLong(table, "MaxResults") ?? 8),
            GetString(table, "Matching") ?? "fuzzy",
            GetStringList(table, "Sources"),
            MapRunnerWindow(GetTable(table, "Window")) ?? RunnerWindowSettings.Default,
            MapRunnerStyle(GetTable(table, "Style") ?? table) ?? RunnerStyleSettings.Default,
            MapRunnerInput(GetTable(table, "Input")) ?? RunnerInputSettings.Default,
            MapRunnerResults(GetTable(table, "Results")) ?? RunnerResultsSettings.Default,
            MapRunnerIcons(GetTable(table, "Icons")) ?? RunnerIconsSettings.Default);
    }

    private static RunnerWindowSettings? MapRunnerWindow(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new RunnerWindowSettings(
            GetString(table, "Position") ?? "top-center",
            (int)(GetLong(table, "Width") ?? 720),
            (int)(GetLong(table, "MaxHeight") ?? 560),
            (int)(GetLong(table, "OffsetX") ?? 0),
            (int)(GetLong(table, "OffsetY") ?? 96));
    }

    private static RunnerStyleSettings? MapRunnerStyle(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new RunnerStyleSettings(
            GetString(table, "Background") ?? "#1e1e2e",
            GetString(table, "Color") ?? "#cdd6f4",
            GetString(table, "BorderColor") ?? "#45475a",
            (int)(GetLong(table, "BorderWidth") ?? 1),
            (int)(GetLong(table, "Radius") ?? 6),
            GetNullableLong(table, "PaddingTop") ?? GetNullableLong(GetTable(table, "Padding"), "Top"),
            GetNullableLong(table, "PaddingRight") ?? GetNullableLong(GetTable(table, "Padding"), "Right"),
            GetNullableLong(table, "PaddingBottom") ?? GetNullableLong(GetTable(table, "Padding"), "Bottom"),
            GetNullableLong(table, "PaddingLeft") ?? GetNullableLong(GetTable(table, "Padding"), "Left"),
            GetString(table, "FontFamily") ?? "Cascadia Mono",
            (float?)(GetDouble(table, "FontSize") ?? 12) ?? 12,
            GetString(table, "Weight"),
            GetBool(table, "Italic"));
    }

    private static RunnerInputSettings? MapRunnerInput(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new RunnerInputSettings(
            GetString(table, "Prompt") ?? "run",
            GetString(table, "Background") ?? "#313244",
            GetString(table, "Color") ?? "#cdd6f4",
            GetString(table, "PlaceholderColor") ?? "#6c7086",
            (int)(GetLong(table, "Height") ?? 36));
    }

    private static RunnerResultsSettings? MapRunnerResults(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new RunnerResultsSettings(
            (int)(GetLong(table, "RowHeight") ?? 40),
            (int)(GetLong(table, "Spacing") ?? 4),
            GetString(table, "Background") ?? "#1e1e2e",
            GetString(table, "Color") ?? "#cdd6f4",
            GetString(table, "SelectedBackground") ?? "#cba6f7",
            GetString(table, "SelectedColor") ?? "#1e1e2e",
            GetString(table, "SecondaryColor") ?? "#a6adc8");
    }

    private static RunnerIconsSettings? MapRunnerIcons(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new RunnerIconsSettings(
            GetBool(table, "Visible") ?? true,
            (int)(GetLong(table, "Size") ?? 24),
            (int)(GetLong(table, "Padding") ?? 8));
    }

    private static WorkspaceBarSettings? MapWorkspaceBar(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        WorkspaceBarWidgetsSettings? widgets = null;
        if (TryGetValue(table, "Widgets", out var widgetsValue))
        {
            widgets = widgetsValue switch
            {
                TomlTable widgetsTable => MapWorkspaceBarWidgets(widgetsTable),
                TomlTableArray widgetsArray => MapWidgetArray(widgetsArray),
                _ => null
            };
        }

        return new WorkspaceBarSettings(
            GetBool(table, "Visible") ?? true,
            (int)(GetLong(table, "Height") ?? 24),
            GetString(table, "Background") ?? "#101010",
            MapWorkspaceBarWorkspaces(GetTable(table, "Workspaces")),
            MapWorkspaceBarLayout(GetTable(table, "Layout")),
            MapWorkspaceBarTitle(GetTable(table, "Title")),
            widgets,
            MapStyle(GetTable(table, "Style") ?? table),
            MapSections(table));
    }

    private static WorkspaceBarWorkspaceSettings? MapWorkspaceBarWorkspaces(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new WorkspaceBarWorkspaceSettings(
            GetString(table, "Background") ?? "#101010",
            GetString(table, "Color", "Foreground") ?? "#D0D0D0",
            GetString(table, "CurrentBackground") ?? "#FFFFFF",
            GetString(table, "CurrentColor", "CurrentForeground") ?? "#101010",
            GetStringList(table, "Symbols"));
    }

    private static WorkspaceBarLayoutSettings? MapWorkspaceBarLayout(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new WorkspaceBarLayoutSettings(
            GetString(table, "Background") ?? "#101010",
            GetString(table, "Color", "Foreground") ?? "#D0D0D0",
            GetStringMap(table, "Symbols"));
    }

    private static WorkspaceBarTitleSettings? MapWorkspaceBarTitle(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        return new WorkspaceBarTitleSettings(
            GetString(table, "Background") ?? "#101010",
            GetString(table, "CurrentBackground") ?? "#101010",
            GetString(table, "CurrentColor", "CurrentForeground") ?? "#FFFFFF");
    }

    private static WorkspaceBarWidgetsSettings? MapWorkspaceBarWidgets(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        var widgets = new Dictionary<string, WorkspaceBarWidgetSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in table)
        {
            if (value is TomlTable widgetTable && !string.Equals(key, "Order", StringComparison.OrdinalIgnoreCase))
            {
                widgets[key] = MapWidget(widgetTable);
            }
        }

        return new WorkspaceBarWidgetsSettings(GetStringList(table, "Order"), widgets);
    }

    private static WorkspaceBarWidgetsSettings MapWidgetArray(TomlTableArray array)
    {
        var order = new List<string>();
        var widgets = new Dictionary<string, WorkspaceBarWidgetSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var widgetTable in array.OfType<TomlTable>())
        {
            var id = GetString(widgetTable, "Id") ?? GetString(widgetTable, "Kind") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            order.Add(id);
            widgets[id] = MapWidget(widgetTable);
        }

        return new WorkspaceBarWidgetsSettings(order, widgets);
    }

    private static WorkspaceBarWidgetSettings MapWidget(TomlTable table)
    {
        return new WorkspaceBarWidgetSettings(
            GetBool(table, "Enabled") ?? true,
            GetString(table, "Kind"),
            GetString(table, "Symbol") ?? "",
            GetString(table, "SymbolBackground", "background") ?? "#101010",
            GetString(table, "SymbolForeground", "symbol-color") ?? "#D0D0D0",
            GetString(table, "ResultBackground", "background") ?? "#101010",
            GetString(table, "ResultForeground", "color") ?? "#D0D0D0",
            GetString(table, "Text") ?? "",
            GetString(table, "Command") ?? "",
            (int)(GetLong(table, "IntervalMilliseconds", "interval-ms") ?? 5000),
            MapStyle(GetTable(table, "Style") ?? table));
    }

    private static WorkspaceBarStyleSettings? MapStyle(TomlTable? table)
    {
        if (table is null)
        {
            return null;
        }

        var border = GetTable(table, "Border");
        var padding = GetTable(table, "Padding");
        var margin = GetTable(table, "Margin");
        var font = GetTable(table, "Font");
        return new WorkspaceBarStyleSettings(
            GetString(table, "Color"),
            GetString(table, "Background"),
            GetNullableLong(table, "BorderWidth") ?? GetNullableLong(border, "Width"),
            GetString(table, "BorderColor") ?? GetString(border, "Color"),
            GetNullableLong(table, "Radius") ?? GetNullableLong(border, "Radius"),
            GetNullableLong(table, "PaddingTop") ?? GetNullableLong(padding, "Top"),
            GetNullableLong(table, "PaddingRight") ?? GetNullableLong(padding, "Right"),
            GetNullableLong(table, "PaddingBottom") ?? GetNullableLong(padding, "Bottom"),
            GetNullableLong(table, "PaddingLeft") ?? GetNullableLong(padding, "Left"),
            GetNullableLong(table, "MarginTop") ?? GetNullableLong(margin, "Top"),
            GetNullableLong(table, "MarginRight") ?? GetNullableLong(margin, "Right"),
            GetNullableLong(table, "MarginBottom") ?? GetNullableLong(margin, "Bottom"),
            GetNullableLong(table, "MarginLeft") ?? GetNullableLong(margin, "Left"),
            GetNullableLong(table, "Spacing"),
            GetString(table, "FontFamily") ?? GetString(font, "Family"),
            (float?)(GetDouble(table, "FontSize") ?? GetDouble(font, "Size")),
            GetString(table, "Weight") ?? GetString(font, "Weight"),
            GetBool(table, "Italic") ?? GetBool(font, "Italic"),
            GetString(table, "Align") ?? "left",
            GetNullableLong(table, "MinWidth"),
            GetNullableLong(table, "MaxWidth"),
            GetBool(table, "Visible"));
    }

    private static IReadOnlyList<WorkspaceBarSectionSettings>? MapSections(TomlTable table)
    {
        if (!TryGetValue(table, "Sections", out var value) || value is not TomlTableArray array)
        {
            return null;
        }

        return array.OfType<TomlTable>()
            .Select(section => new WorkspaceBarSectionSettings(
                GetString(section, "Id") ?? string.Empty,
                GetString(section, "Align") ?? "left",
                MapStyle(GetTable(section, "Style") ?? section),
                GetStringList(section, "Widgets")))
            .ToList();
    }

    private static TomlTable? GetTable(TomlTable? table, string key)
        => TryGetValue(table, key, out var value) ? value as TomlTable : null;

    private static string? GetString(TomlTable? table, params string[] keys)
        => TryGetValue(table, keys, out var value) ? value as string : null;

    private static double? GetDouble(TomlTable? table, string key)
        => TryGetValue(table, key, out var value)
            ? value switch
            {
                double number => number,
                long number => number,
                _ => null
            }
            : null;

    private static long? GetLong(TomlTable? table, params string[] keys)
        => TryGetValue(table, keys, out var value) ? value as long? : null;

    private static int? GetNullableLong(TomlTable? table, string key)
        => GetLong(table, key) is long value ? (int)value : null;

    private static bool? GetBool(TomlTable? table, string key)
        => TryGetValue(table, key, out var value) ? value as bool? : null;

    private static bool TryGetValue(TomlTable? table, string key, out object? value)
        => TryGetValue(table, [key], out value);

    private static bool TryGetValue(TomlTable? table, IReadOnlyList<string> keys, out object? value)
    {
        if (table is not null)
        {
            foreach (var pair in table)
            {
                if (keys.Any(key => NormalizeKey(pair.Key) == NormalizeKey(key)))
                {
                    value = pair.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    private static string NormalizeKey(string key)
        => key.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

    private static IReadOnlyList<string>? GetStringList(TomlTable table, string key)
    {
        if (!TryGetValue(table, key, out var value) || value is not TomlArray array)
        {
            return null;
        }

        return array.OfType<string>().ToList();
    }

    private static IReadOnlyDictionary<string, string>? GetStringMap(TomlTable table, string key)
    {
        if (!TryGetValue(table, key, out var value) || value is not TomlTable nested)
        {
            return null;
        }

        var result = new Dictionary<string, string>();
        foreach (var (nestedKey, nestedValue) in nested)
        {
            if (nestedValue is string text)
            {
                result[nestedKey] = text;
            }
        }

        return result;
    }
}
