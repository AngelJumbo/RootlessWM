namespace RootlessWM.Domain;

internal sealed class MasterLeftLayout : ILayout
{
    public MasterStackLayoutMode Mode => MasterStackLayoutMode.MasterLeft;

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

        var horizontalGap = Math.Min(options.InnerGap, tileWidth);
        var availableWidth = tileWidth - horizontalGap;
        var masterWidth = stackCount > 0
            ? (int)Math.Round(availableWidth * options.MasterRatio, MidpointRounding.AwayFromZero)
            : tileWidth;
        var stackWidth = availableWidth - masterWidth;
        var placements = new List<WindowPlacement>(orderedHandles.Count);

        var masterVerticalGap = masterCount > 1 ? Math.Min(options.InnerGap, tileHeight / (masterCount - 1)) : 0;
        var masterAvailableHeight = tileHeight - (masterVerticalGap * (masterCount - 1));
        var baseMasterHeight = masterAvailableHeight / masterCount;
        var masterRemainder = masterAvailableHeight % masterCount;
        var masterTop = top;
        for (var index = 0; index < masterCount; index++)
        {
            var masterHeight = baseMasterHeight + (index < masterRemainder ? 1 : 0);
            placements.Add(new WindowPlacement(orderedHandles[index], new WindowBounds(left, masterTop, masterWidth, masterHeight)));
            masterTop += masterHeight + masterVerticalGap;
        }

        if (stackCount > 0)
        {
            var verticalGap = Math.Min(options.InnerGap, tileHeight / (stackCount - 1 == 0 ? 1 : stackCount - 1));
            var availableHeight = tileHeight - (verticalGap * (stackCount - 1));
            var baseStackHeight = availableHeight / stackCount;
            var remainder = availableHeight % stackCount;
            var stackLeft = left + masterWidth + horizontalGap;
            var stackTop = top;
            for (var index = 0; index < stackCount; index++)
            {
                var stackHeight = baseStackHeight + (index < remainder ? 1 : 0);
                placements.Add(new WindowPlacement(orderedHandles[masterCount + index], new WindowBounds(stackLeft, stackTop, stackWidth, stackHeight)));
                stackTop += stackHeight + verticalGap;
            }
        }

        return placements;
    }
}
