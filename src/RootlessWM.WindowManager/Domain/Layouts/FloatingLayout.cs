namespace RootlessWM.Domain;

internal sealed class FloatingLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.Floating;

    public IReadOnlyList<WindowPlacement> Calculate(
        int left,
        int top,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions options)
        => [];
}
