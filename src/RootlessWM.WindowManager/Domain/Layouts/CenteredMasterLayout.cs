namespace RootlessWM.Domain;

internal sealed class CenteredMasterLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.CenteredMaster;

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

        var gap = options.InnerGap;
        var masterCount = Math.Min(options.MasterCount, count);
        var secondaryCount = count - masterCount;

        int masterWidth;
        int masterLeft;
        if (secondaryCount == 0)
        {
            masterWidth = tileWidth;
            masterLeft = left;
        }
        else
        {
            var clampedGap = LayoutMath.ClampGap(gap, tileWidth, 2);
            var availableWidth = Math.Max(tileWidth - (clampedGap * 2), 3);
            masterWidth = Math.Clamp(
                (int)Math.Round(availableWidth * options.MasterRatio, MidpointRounding.AwayFromZero),
                1,
                availableWidth - 2);
            var leftover = tileWidth - masterWidth;
            masterLeft = left + (leftover / 2);
        }

        var placements = new List<WindowPlacement>(count);
        var masterRows = LayoutMath.LayoutSegments(top, tileHeight, masterCount, gap);
        for (var index = 0; index < masterCount; index++)
        {
            var (rowTop, rowHeight) = masterRows[index];
            placements.Add(new WindowPlacement(orderedHandles[index], new WindowBounds(masterLeft, rowTop, masterWidth, rowHeight)));
        }

        if (secondaryCount == 0)
        {
            return placements;
        }

        var leftHandles = new List<nint>();
        var rightHandles = new List<nint>();
        for (var index = 0; index < secondaryCount; index++)
        {
            var handle = orderedHandles[masterCount + index];
            if (index % 2 == 0)
            {
                rightHandles.Add(handle);
            }
            else
            {
                leftHandles.Add(handle);
            }
        }

        if (rightHandles.Count > 0)
        {
            var rightLeft = masterLeft + masterWidth + gap;
            var rightWidth = Math.Max((left + tileWidth) - rightLeft, 1);
            var rightRows = LayoutMath.LayoutSegments(top, tileHeight, rightHandles.Count, gap);
            for (var index = 0; index < rightHandles.Count; index++)
            {
                var (rowTop, rowHeight) = rightRows[index];
                placements.Add(new WindowPlacement(rightHandles[index], new WindowBounds(rightLeft, rowTop, rightWidth, rowHeight)));
            }
        }

        if (leftHandles.Count > 0)
        {
            var leftWidth = Math.Max((masterLeft - gap) - left, 1);
            var leftRows = LayoutMath.LayoutSegments(top, tileHeight, leftHandles.Count, gap);
            for (var index = 0; index < leftHandles.Count; index++)
            {
                var (rowTop, rowHeight) = leftRows[index];
                placements.Add(new WindowPlacement(leftHandles[index], new WindowBounds(left, rowTop, leftWidth, rowHeight)));
            }
        }

        return placements;
    }
}
