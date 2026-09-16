namespace RootlessWM.Domain;

public sealed class TilingState
{
    private readonly List<nint> _tiledHandles = [];
    private readonly HashSet<nint> _floatingHandles = [];

    public IReadOnlyList<nint> TiledHandles => _tiledHandles;

    public IReadOnlySet<nint> FloatingHandles => _floatingHandles;

    public void Synchronize(IEnumerable<TrackedWindow> trackedWindows)
    {
        ArgumentNullException.ThrowIfNull(trackedWindows);

        var managedWindows = trackedWindows
            .Where(window => window.Eligibility == WindowEligibility.Managed)
            .ToArray();
        var managedHandles = managedWindows
            .Select(window => window.Handle)
            .ToHashSet();

        _tiledHandles.RemoveAll(handle => !managedHandles.Contains(handle));
        _floatingHandles.RemoveWhere(handle => !managedHandles.Contains(handle));

        foreach (var window in managedWindows
            .OrderByDescending(window => (long)window.Candidate.Bounds.Width * window.Candidate.Bounds.Height)
            .ThenBy(window => window.Handle))
        {
            if (!_floatingHandles.Contains(window.Handle) && !_tiledHandles.Contains(window.Handle))
            {
                _tiledHandles.Add(window.Handle);
            }
        }
    }

    public bool PromoteToMaster(nint handle)
    {
        var index = _tiledHandles.IndexOf(handle);
        if (index <= 0)
        {
            return index == 0;
        }

        _tiledHandles.RemoveAt(index);
        _tiledHandles.Insert(0, handle);
        return true;
    }

    public bool SwapWithNext(nint handle)
    {
        var index = _tiledHandles.IndexOf(handle);
        if (index < 0 || index == _tiledHandles.Count - 1)
        {
            return false;
        }

        (_tiledHandles[index], _tiledHandles[index + 1]) = (_tiledHandles[index + 1], _tiledHandles[index]);
        return true;
    }

    public bool SwapWithPrevious(nint handle)
    {
        var index = _tiledHandles.IndexOf(handle);
        if (index <= 0)
        {
            return false;
        }

        (_tiledHandles[index], _tiledHandles[index - 1]) = (_tiledHandles[index - 1], _tiledHandles[index]);
        return true;
    }

    public bool ToggleFloating(nint handle)
    {
        var tiledIndex = _tiledHandles.IndexOf(handle);
        if (tiledIndex >= 0)
        {
            _tiledHandles.RemoveAt(tiledIndex);
            _floatingHandles.Add(handle);
            return true;
        }

        if (_floatingHandles.Remove(handle))
        {
            _tiledHandles.Add(handle);
            return true;
        }

        return false;
    }
}
