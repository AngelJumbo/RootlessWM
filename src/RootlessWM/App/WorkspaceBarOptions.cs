using System.Drawing;
using RootlessWM.Domain;

namespace RootlessWM.App;

public sealed record WorkspaceBarOptions(
    bool Visible,
    int Height,
    Color Background,
    WorkspaceBarWorkspaceOptions Workspaces,
    WorkspaceBarLayoutOptions Layout,
    WorkspaceBarTitleOptions Title,
    IReadOnlyList<WorkspaceBarWidgetOptions> Widgets,
    WorkspaceBarStyleOptions? Style = null,
    IReadOnlyList<WorkspaceBarSectionOptions>? Sections = null,
    IReadOnlyList<WorkspaceBarModuleOptions>? ModulesLeft = null,
    IReadOnlyList<WorkspaceBarModuleOptions>? ModulesCenter = null,
    IReadOnlyList<WorkspaceBarModuleOptions>? ModulesRight = null)
{
    public WindowBounds ReserveTopSpace(WindowBounds workArea)
    {
        var margin = Style?.Margin ?? Padding.Empty;
        var reservedHeight = Height + margin.Top + margin.Bottom;
        if (!Visible || reservedHeight >= workArea.Height)
        {
            return workArea;
        }

        return new WindowBounds(
            workArea.Left,
            workArea.Top + reservedHeight,
            workArea.Width,
            workArea.Height - reservedHeight);
    }
}

public enum WorkspaceBarSectionAlignment
{
    Left,
    Center,
    Right
}

public sealed record WorkspaceBarStyleOptions(
    Color Foreground,
    Color Background,
    int BorderWidth,
    Color BorderColor,
    int BorderRadius,
    Padding Padding,
    Padding Margin,
    int Spacing,
    string FontFamily,
    float FontSize,
    FontStyle FontStyle,
    WorkspaceBarSectionAlignment Alignment,
    int? MinWidth,
    int? MaxWidth,
    bool Visible)
{
    public static WorkspaceBarStyleOptions Default { get; } = new(
        Color.White,
        Color.Transparent,
        0,
        Color.Transparent,
        0,
        Padding.Empty,
        Padding.Empty,
        0,
        "Segoe UI",
        9F,
        FontStyle.Regular,
        WorkspaceBarSectionAlignment.Left,
        null,
        null,
        true);
}

public sealed record WorkspaceBarSectionOptions(
    string Id,
    WorkspaceBarSectionAlignment Alignment,
    WorkspaceBarStyleOptions Style,
    IReadOnlyList<string> Widgets);

public sealed record WorkspaceBarWorkspaceOptions(
    Color Background,
    Color Foreground,
    Color CurrentBackground,
    Color CurrentForeground,
    IReadOnlyList<string> Symbols);

public sealed record WorkspaceBarLayoutOptions(
    Color Background,
    Color Foreground,
    IReadOnlyDictionary<MasterStackLayoutMode, string> Symbols);

public sealed record WorkspaceBarTitleOptions(
    Color Background,
    Color CurrentBackground,
    Color CurrentForeground);

public sealed record WorkspaceBarWidgetOptions(
    string Kind,
    string Symbol,
    Color SymbolBackground,
    Color SymbolForeground,
    Color ResultBackground,
    Color ResultForeground,
    string Text = "",
    string Command = "",
    int IntervalMilliseconds = 5000,
    WorkspaceBarStyleOptions? Style = null)
{
    public string Id { get; init; } = Kind;
    public IReadOnlyDictionary<string, string>? BatterySymbols { get; init; }
}

public enum WorkspaceBarModuleMonitor
{
    All,
    Primary,
    Focused
}

public sealed record WorkspaceBarModuleOptions(
    string Id,
    string Type,
    WorkspaceBarModuleMonitor Monitor,
    WorkspaceBarStyleOptions Style,
    string? Format = null,
    string? Text = null,
    string? Command = null,
    int IntervalMilliseconds = 5000,
    IReadOnlyList<string>? Labels = null,
    IReadOnlyDictionary<MasterStackLayoutMode, string>? Symbols = null,
    string? Symbol = null,
    Color? ActiveBackground = null,
    Color? ActiveForeground = null,
    IReadOnlyDictionary<string, string>? BatterySymbols = null)
{
    public bool IsShownOn(bool isPrimary, bool isFocused)
        => Monitor == WorkspaceBarModuleMonitor.All
            || Monitor == WorkspaceBarModuleMonitor.Primary && isPrimary
            || Monitor == WorkspaceBarModuleMonitor.Focused && isFocused;
}
