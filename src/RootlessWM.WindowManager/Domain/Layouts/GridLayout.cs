namespace RootlessWM.Domain;

internal sealed class GridLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.Grid;

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

        var columns = (int)Math.Ceiling(Math.Sqrt(count));
        var rows = (int)Math.Ceiling(count / (double)columns);
        var rowSegments = LayoutMath.LayoutSegments(top, tileHeight, rows, options.InnerGap);

        var placements = new List<WindowPlacement>(count);
        var index = 0;
        for (var row = 0; row < rows; row++)
        {
            var itemsInRow = Math.Min(columns, count - index);
            var (rowTop, rowHeight) = rowSegments[row];
            var columnSegments = LayoutMath.LayoutSegments(left, tileWidth, itemsInRow, options.InnerGap);
            for (var column = 0; column < itemsInRow; column++)
            {
                var (columnLeft, columnWidth) = columnSegments[column];
                placements.Add(new WindowPlacement(orderedHandles[index], new WindowBounds(columnLeft, rowTop, columnWidth, rowHeight)));
                index++;
            }
        }

        return placements;
    }
}
