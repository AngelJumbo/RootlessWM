namespace RootlessWM.Domain;

public sealed class WindowTiler(MasterStackLayout layout, IWindowPlacementApplier placementApplier)
{
    public TilingOperationResult Tile(
        WindowBounds workArea,
        IEnumerable<TrackedWindow> trackedWindows,
        MasterStackLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(trackedWindows);

        var managedHandles = trackedWindows
            .Where(window => window.Eligibility == WindowEligibility.Managed)
            .Select(window => window.Handle)
            .ToArray();

        return Tile(workArea, managedHandles, options);
    }

    public TilingOperationResult Tile(
        WindowBounds workArea,
        IReadOnlyList<nint> orderedHandles,
        MasterStackLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(orderedHandles);

        var placements = layout.Calculate(workArea, orderedHandles, options);
        var results = new List<PlacementResult>(placements.Count);

        foreach (var placement in placements)
        {
            results.Add(placementApplier.Apply(placement));
        }

        return new TilingOperationResult(placements, results);
    }
}
