namespace RootlessWM.Domain;

internal sealed class DwindleLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.Dwindle;

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

        // The master window claims a MasterRatio-weighted share; every remaining window then
        // dwindles the leftover rectangle in half, alternating split direction.
        var (masterSegment, restSegment) = LayoutMath.SplitWeighted(left, tileWidth, options.MasterRatio, options.InnerGap);
        placements.Add(new WindowPlacement(orderedHandles[0], new WindowBounds(masterSegment.Offset, top, masterSegment.Size, tileHeight)));

        var (rectLeft, rectTop, rectWidth, rectHeight) = (restSegment.Offset, top, restSegment.Size, tileHeight);
        var remainingCount = count - 1;
        for (var index = 0; index < remainingCount; index++)
        {
            var handle = orderedHandles[1 + index];
            if (index == remainingCount - 1)
            {
                placements.Add(LayoutMath.FullTile(handle, rectLeft, rectTop, rectWidth, rectHeight));
                break;
            }

            if (index % 2 == 0)
            {
                var segments = LayoutMath.LayoutSegments(rectTop, rectHeight, 2, options.InnerGap);
                placements.Add(new WindowPlacement(handle, new WindowBounds(rectLeft, segments[0].Offset, rectWidth, segments[0].Size)));
                rectTop = segments[1].Offset;
                rectHeight = segments[1].Size;
            }
            else
            {
                var segments = LayoutMath.LayoutSegments(rectLeft, rectWidth, 2, options.InnerGap);
                placements.Add(new WindowPlacement(handle, new WindowBounds(segments[0].Offset, rectTop, segments[0].Size, rectHeight)));
                rectLeft = segments[1].Offset;
                rectWidth = segments[1].Size;
            }
        }

        return placements;
    }
}
