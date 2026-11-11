using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class AnimatedWindowPlacementApplier : IWindowPlacementApplier, IDisposable
{
    private const int FrameIntervalMs = 16;
    private readonly object _gate = new();
    private readonly IWindowPlacementApplier _inner;
    private readonly Func<nint, WindowBounds?> _getBounds;
    private readonly Func<nint, bool> _isMaximized;
    private readonly Action<PlacementResult> _reportFailure;
    private readonly Func<long> _clock;
    private readonly Timer? _timer;
    private readonly Dictionary<nint, Transition> _transitions = [];
    private readonly HashSet<nint> _dragging = [];
    private bool _enabled = true;
    private bool _suspended;
    private bool _disposed;
    private int _durationMs = 160;

    public AnimatedWindowPlacementApplier(
        IWindowPlacementApplier inner,
        Func<nint, WindowBounds?> getBounds,
        Func<nint, bool> isMaximized,
        Action<PlacementResult> reportFailure,
        Func<long>? clock = null,
        bool runTimer = true)
    {
        _inner = inner;
        _getBounds = getBounds;
        _isMaximized = isMaximized;
        _reportFailure = reportFailure;
        _clock = clock ?? (() => Environment.TickCount64);
        _timer = runTimer ? new Timer(OnTimer) : null;
    }

    public void Configure(bool enabled, int durationMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationMs, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(durationMs, 1000);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_enabled != enabled || _durationMs != durationMs)
            {
                CancelAllCore();
            }

            _enabled = enabled;
            _durationMs = durationMs;
        }
    }

    public PlacementResult Apply(WindowPlacement placement)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_suspended)
            {
                return PlacementResult.Success(placement.Handle);
            }

            if (!_enabled)
            {
                _transitions.Remove(placement.Handle);
                return _inner.Apply(placement);
            }

            if (_dragging.Contains(placement.Handle))
            {
                return PlacementResult.Success(placement.Handle);
            }

            if (_isMaximized(placement.Handle))
            {
                _transitions.Remove(placement.Handle);
                return _inner.Apply(placement);
            }

            if (_transitions.TryGetValue(placement.Handle, out var current)
                && current.Target == placement.Bounds)
            {
                return PlacementResult.Success(placement.Handle);
            }

            _transitions.Remove(placement.Handle);
            var start = _getBounds(placement.Handle);
            if (start is null || !start.Value.IsUsable || !placement.Bounds.IsUsable)
            {
                return _inner.Apply(placement);
            }

            // Validate the placement synchronously so the tiler can still handle failures.
            var result = _inner.Apply(new WindowPlacement(placement.Handle, start.Value));
            if (!result.Applied || start.Value == placement.Bounds)
            {
                return result;
            }

            var actualStart = _getBounds(placement.Handle);
            if (actualStart is null || !actualStart.Value.IsUsable)
            {
                return PlacementResult.Skipped(placement.Handle, "invalid_window");
            }

            _transitions[placement.Handle] = new Transition(actualStart.Value, placement.Bounds, _clock(), _durationMs);
            _timer?.Change(FrameIntervalMs, FrameIntervalMs);
            return result;
        }
    }

    public void Tick()
    {
        lock (_gate)
        {
            if (_disposed || _suspended)
            {
                return;
            }

            var now = _clock();
            foreach (var (handle, transition) in _transitions.ToArray())
            {
                var progress = Math.Clamp((double)(now - transition.StartTime) / transition.DurationMs, 0, 1);
                var bounds = Interpolate(transition.Start, transition.Target, progress);
                var result = _inner.Apply(new WindowPlacement(handle, bounds));
                if (!result.Applied)
                {
                    _transitions.Remove(handle);
                    _reportFailure(result);
                }
                else if (progress >= 1)
                {
                    _transitions.Remove(handle);
                }
            }

            if (_transitions.Count == 0)
            {
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
    }

    public void BeginMoveSize(nint handle)
    {
        lock (_gate)
        {
            _dragging.Add(handle);
            _transitions.Remove(handle);
        }
    }

    public void EndMoveSize(nint handle)
    {
        lock (_gate)
        {
            _dragging.Remove(handle);
        }
    }

    public void Cancel(nint handle)
    {
        lock (_gate)
        {
            _transitions.Remove(handle);
            _dragging.Remove(handle);
        }
    }

    public void Retain(IReadOnlySet<nint> handles)
    {
        lock (_gate)
        {
            foreach (var handle in _transitions.Keys.Where(handle => !handles.Contains(handle)).ToArray())
            {
                _transitions.Remove(handle);
            }
        }
    }

    public void CancelAll()
    {
        lock (_gate)
        {
            CancelAllCore();
        }
    }

    public void SetSuspended(bool suspended)
    {
        lock (_gate)
        {
            _suspended = suspended;
            if (suspended)
            {
                CancelAllCore();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            CancelAllCore();
            _timer?.Dispose();
            _disposed = true;
        }
    }

    private void CancelAllCore()
    {
        _transitions.Clear();
        _dragging.Clear();
        if (!_disposed)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void OnTimer(object? state)
    {
        try
        {
            Tick();
        }
        catch (Exception exception)
        {
            CancelAll();
            _reportFailure(PlacementResult.Skipped(nint.Zero, $"animation_failed_{exception.GetType().Name}"));
        }
    }

    private static WindowBounds Interpolate(WindowBounds start, WindowBounds target, double progress)
    {
        var eased = 1 - Math.Pow(1 - progress, 3);
        static int Lerp(int from, int to, double amount) => (int)Math.Round(from + ((double)to - from) * amount);
        return new WindowBounds(
            Lerp(start.Left, target.Left, eased),
            Lerp(start.Top, target.Top, eased),
            Lerp(start.Width, target.Width, eased),
            Lerp(start.Height, target.Height, eased));
    }

    private sealed record Transition(WindowBounds Start, WindowBounds Target, long StartTime, int DurationMs);
}
