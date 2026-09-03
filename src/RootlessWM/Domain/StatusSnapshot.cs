using System.Globalization;

namespace RootlessWM.Domain;

public readonly record struct StatusSnapshot(
    bool IsEnabled,
    int CurrentWorkspace,
    int WorkspaceCount,
    double MasterRatio,
    int OuterGap,
    int InnerGap,
    int TiledWindowCount,
    int MasterCount);

public static class StatusText
{
    public static string Format(StatusSnapshot snapshot)
    {
        var state = snapshot.IsEnabled ? "ON" : "OFF";
        return $"RootlessWM | {state} | WS {snapshot.CurrentWorkspace + 1}/{snapshot.WorkspaceCount} | "
            + $"{snapshot.TiledWindowCount} tiled | {snapshot.MasterRatio.ToString("P0", CultureInfo.InvariantCulture)} master | "
            + $"{snapshot.MasterCount} master win | outer {snapshot.OuterGap} | inner {snapshot.InnerGap}";
    }
}
