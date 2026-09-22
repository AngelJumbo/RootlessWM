namespace RootlessWM.Domain;

internal interface ILayout
{
    MasterStackLayoutMode Mode { get; }

    IReadOnlyList<WindowPlacement> Calculate(
        int left,
        int top,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions options);
}
