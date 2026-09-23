using System.Drawing;
using RootlessWM.Domain;

namespace RootlessWM.App;

public sealed record WorkspaceBarOptions(
    bool Visible,
    int Height,
    Color Background,
    WorkspaceBarStyleOptions? Style = null,
    IReadOnlyList<WorkspaceBarModuleOptions>? ModulesLeft = null,
    IReadOnlyList<WorkspaceBarModuleOptions>? ModulesCenter = null,
    IReadOnlyList<WorkspaceBarModuleOptions>? ModulesRight = null,
    WorkspaceBarPosition Position = WorkspaceBarPosition.Top)
{
    public bool IsVertical => Position is WorkspaceBarPosition.Left or WorkspaceBarPosition.Right;

    public WindowBounds Reserve(WindowBounds workArea)
    {
        if (!Visible)
        {
            return workArea;
        }

        var margin = Style?.Margin ?? Padding.Empty;
        return Position switch
        {
            WorkspaceBarPosition.Top => ReserveEdge(workArea, Height + margin.Top + margin.Bottom, fromStart: true, horizontal: false),
            WorkspaceBarPosition.Bottom => ReserveEdge(workArea, Height + margin.Top + margin.Bottom, fromStart: false, horizontal: false),
            WorkspaceBarPosition.Left => ReserveEdge(workArea, Height + margin.Left + margin.Right, fromStart: true, horizontal: true),
            WorkspaceBarPosition.Right => ReserveEdge(workArea, Height + margin.Left + margin.Right, fromStart: false, horizontal: true),
            _ => workArea
        };
    }

    private static WindowBounds ReserveEdge(WindowBounds workArea, int reserved, bool fromStart, bool horizontal)
    {
        var available = horizontal ? workArea.Width : workArea.Height;
        if (reserved >= available)
        {
            return workArea;
        }

        if (horizontal)
        {
            return fromStart
                ? new WindowBounds(workArea.Left + reserved, workArea.Top, workArea.Width - reserved, workArea.Height)
                : new WindowBounds(workArea.Left, workArea.Top, workArea.Width - reserved, workArea.Height);
        }

        return fromStart
            ? new WindowBounds(workArea.Left, workArea.Top + reserved, workArea.Width, workArea.Height - reserved)
            : new WindowBounds(workArea.Left, workArea.Top, workArea.Width, workArea.Height - reserved);
    }
}

public enum WorkspaceBarPosition
{
    Top,
    Bottom,
    Left,
    Right
}

public enum WorkspaceBarAlignment
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
    int? MinWidth,
    int? MaxWidth,
    bool Visible,
    int? MaxLength = null)
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
        null,
        null,
        true);
}

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
    IReadOnlyDictionary<string, string>? BatterySymbols = null,
    string? OnClick = null)
{
    public bool IsShownOn(bool isPrimary, bool isFocused)
        => Monitor == WorkspaceBarModuleMonitor.All
            || Monitor == WorkspaceBarModuleMonitor.Primary && isPrimary
            || Monitor == WorkspaceBarModuleMonitor.Focused && isFocused;
}
