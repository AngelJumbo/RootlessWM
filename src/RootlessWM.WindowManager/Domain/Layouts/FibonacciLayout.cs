namespace RootlessWM.Domain;

internal sealed class FibonacciLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.Fibonacci;

    public IReadOnlyList<WindowPlacement> Calculate(
        int left,
        int top,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions options)
    {
        var count = orderedHandles.Count;
        if (count == 1)
        {
            return [LayoutMath.FullTile(orderedHandles[0], left, top, tileWidth, tileHeight)];
        }

        var placements = new List<WindowPlacement>(count);
        var (rectLeft, rectTop, rectWidth, rectHeight) = (left, top, tileWidth, tileHeight);
        for (var index = 0; index < count; index++)
        {
            var handle = orderedHandles[index];
            if (index == count - 1)
            {
                placements.Add(LayoutMath.FullTile(handle, rectLeft, rectTop, rectWidth, rectHeight));
                break;
            }

            if (index % 2 == 0)
            {
                var segments = LayoutMath.LayoutSegments(rectLeft, rectWidth, 2, options.InnerGap);
                placements.Add(new WindowPlacement(handle, new WindowBounds(segments[0].Offset, rectTop, segments[0].Size, rectHeight)));
                rectLeft = segments[1].Offset;
                rectWidth = segments[1].Size;
            }
            else
            {
                var segments = LayoutMath.LayoutSegments(rectTop, rectHeight, 2, options.InnerGap);
                placements.Add(new WindowPlacement(handle, new WindowBounds(rectLeft, segments[0].Offset, rectWidth, segments[0].Size)));
                rectTop = segments[1].Offset;
                rectHeight = segments[1].Size;
            }
        }

        return placements;
    }
}
