namespace RootlessWM.Domain;

internal sealed class MasterTopLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.MasterTop;

    public IReadOnlyList<WindowPlacement> Calculate(
        int left,
        int top,
        int tileWidth,
        int tileHeight,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions options)
    {
        if (orderedHandles.Count == 1)
        {
            return [LayoutMath.FullTile(orderedHandles[0], left, top, tileWidth, tileHeight)];
        }

        var masterCount = Math.Min(options.MasterCount, orderedHandles.Count);
        var stackCount = orderedHandles.Count - masterCount;

        var verticalGap = Math.Min(options.InnerGap, tileHeight);
        var availableHeight = tileHeight - verticalGap;
        var masterHeight = stackCount > 0
            ? (int)Math.Round(availableHeight * options.MasterRatio, MidpointRounding.AwayFromZero)
            : tileHeight;
        var stackHeight = availableHeight - masterHeight;
        var placements = new List<WindowPlacement>(orderedHandles.Count);

        var masterHorizontalGap = masterCount > 1 ? Math.Min(options.InnerGap, tileWidth / (masterCount - 1)) : 0;
        var masterAvailableWidth = tileWidth - (masterHorizontalGap * (masterCount - 1));
        var baseMasterWidth = masterAvailableWidth / masterCount;
        var masterRemainder = masterAvailableWidth % masterCount;
        var masterLeft = left;
        for (var index = 0; index < masterCount; index++)
        {
            var width = baseMasterWidth + (index < masterRemainder ? 1 : 0);
            placements.Add(new WindowPlacement(orderedHandles[index], new WindowBounds(masterLeft, top, width, masterHeight)));
            masterLeft += width + masterHorizontalGap;
        }

        if (stackCount > 0)
        {
            var horizontalGap = stackCount > 1
                ? Math.Min(options.InnerGap, tileWidth / (stackCount - 1))
                : 0;
            var availableWidth = tileWidth - (horizontalGap * (stackCount - 1));
            var baseStackWidth = availableWidth / stackCount;
            var remainder = availableWidth % stackCount;
            var stackLeft = left;
            var stackTop = top + masterHeight + verticalGap;
            for (var index = 0; index < stackCount; index++)
            {
                var width = baseStackWidth + (index < remainder ? 1 : 0);
                placements.Add(new WindowPlacement(orderedHandles[masterCount + index], new WindowBounds(stackLeft, stackTop, width, stackHeight)));
                stackLeft += width + horizontalGap;
            }
        }

        return placements;
    }
}
