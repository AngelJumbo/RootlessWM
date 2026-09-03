namespace RootlessWM.Domain;

public sealed class MasterStackLayout
{
    public IReadOnlyList<WindowPlacement> Calculate(
        WindowBounds workArea,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(orderedHandles);
        if (!workArea.IsUsable)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea), "The work area must have positive dimensions.");
        }

        if (orderedHandles.Count == 0)
        {
            return [];
        }

        var layoutOptions = options ?? MasterStackLayoutOptions.Default;
        layoutOptions.Validate();

        if (layoutOptions.Mode == MasterStackLayoutMode.Floating)
        {
            return [];
        }

        var outerGap = Math.Min(layoutOptions.OuterGap, Math.Min(workArea.Width / 2, workArea.Height / 2));
        var tileHeight = workArea.Height - (outerGap * 2);
        var tileWidth = workArea.Width - (outerGap * 2);
        var left = workArea.Left + outerGap;
        var top = workArea.Top + outerGap;

        if (layoutOptions.Mode == MasterStackLayoutMode.Monocle)
        {
            var bounds = new WindowBounds(left, top, tileWidth, tileHeight);
            return orderedHandles.Select(handle => new WindowPlacement(handle, bounds)).ToArray();
        }

        if (orderedHandles.Count == 1)
        {
            return [new WindowPlacement(orderedHandles[0], new WindowBounds(left, top, tileWidth, tileHeight))];
        }

        var masterCount = Math.Min(layoutOptions.MasterCount, orderedHandles.Count);
        var stackCount = orderedHandles.Count - masterCount;
        return layoutOptions.Mode == MasterStackLayoutMode.MasterTop
            ? CalculateMasterTop(orderedHandles, left, top, tileWidth, tileHeight, masterCount, stackCount, layoutOptions)
            : CalculateMasterLeft(orderedHandles, left, top, tileWidth, tileHeight, masterCount, stackCount, layoutOptions);
    }

    private static IReadOnlyList<WindowPlacement> CalculateMasterLeft(
        IReadOnlyList<nint> orderedHandles,
        int left,
        int top,
        int tileWidth,
        int tileHeight,
        int masterCount,
        int stackCount,
        MasterStackLayoutOptions options)
    {
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

    private static IReadOnlyList<WindowPlacement> CalculateMasterTop(
        IReadOnlyList<nint> orderedHandles,
        int left,
        int top,
        int tileWidth,
        int tileHeight,
        int masterCount,
        int stackCount,
        MasterStackLayoutOptions options)
    {
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
