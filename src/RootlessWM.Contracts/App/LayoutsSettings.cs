namespace RootlessWM.App;

public sealed record LayoutsSettings(
    IReadOnlyList<string>? Enabled = null,
    string Default = "MasterLeft",
    double MasterRatio = 0.6,
    int MasterCount = 1,
    bool Animate = true,
    int AnimationDurationMs = 160)
{
    public static LayoutsSettings Empty { get; } = new();
}
