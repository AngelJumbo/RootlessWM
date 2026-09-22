namespace RootlessWM.Domain;

public static class LayoutCatalog
{
    public static IReadOnlyList<MasterStackLayoutMode> DefaultCycleOrder { get; } =
    [
        MasterStackLayoutMode.MasterLeft,
        MasterStackLayoutMode.MasterTop,
        MasterStackLayoutMode.Monocle,
        MasterStackLayoutMode.Floating
    ];
}
