namespace RootlessWM.Domain;

public sealed class WindowRestorer(IWindowBoundsRestorer boundsRestorer)
{
    public IReadOnlyList<PlacementResult> Restore(IEnumerable<ManagedWindowState> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var results = new List<PlacementResult>();
        foreach (var window in windows)
        {
            results.Add(boundsRestorer.Restore(new WindowPlacement(window.Handle, window.OriginalBounds)));
        }

        return results;
    }
}
