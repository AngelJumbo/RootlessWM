namespace RootlessWM.Domain;

internal sealed class MonocleLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.Monocle;

    public IReadOnlyList<WindowPlacement> Calculate(
        int left,
        int top,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions options)
    {
        var bounds = new WindowBounds(left, top, tileWidth, tileHeight);
        return orderedHandles.Select(handle => new WindowPlacement(handle, bounds)).ToArray();
    }
}
