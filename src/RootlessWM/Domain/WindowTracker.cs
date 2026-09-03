namespace RootlessWM.Domain;

public sealed class WindowTracker(WindowEligibilityClassifier eligibilityClassifier)
{
    private readonly object _gate = new();
    private readonly Dictionary<nint, TrackedWindow> _windows = [];

    public IReadOnlyList<TrackedWindow> Seed(IEnumerable<WindowCandidate> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        lock (_gate)
        {
            foreach (var window in windows)
            {
                ObserveCore(window);
            }

            return _windows.Values.OrderBy(window => window.Handle).ToArray();
        }
    }

    public TrackedWindow Observe(WindowCandidate window)
    {
        lock (_gate)
        {
            return ObserveCore(window);
        }
    }

    public bool Remove(nint handle)
    {
        lock (_gate)
        {
            return _windows.Remove(handle);
        }
    }

    public bool Contains(nint handle)
    {
        lock (_gate)
        {
            return _windows.ContainsKey(handle);
        }
    }

    public IReadOnlyList<TrackedWindow> Snapshot()
    {
        lock (_gate)
        {
            return _windows.Values.OrderBy(window => window.Handle).ToArray();
        }
    }

    private TrackedWindow ObserveCore(WindowCandidate window)
    {
        var eligibility = eligibilityClassifier.Classify(window);
        if (_windows.TryGetValue(window.Handle, out var existing))
        {
            var updated = existing with { Candidate = window, Eligibility = eligibility };
            _windows[window.Handle] = updated;
            return updated;
        }

        var trackedWindow = new TrackedWindow(window, eligibility, window.Bounds);
        _windows.Add(window.Handle, trackedWindow);
        return trackedWindow;
    }
}
