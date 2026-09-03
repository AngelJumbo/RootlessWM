namespace RootlessWM.Domain;

public sealed class MonitorOwnership
{
    private readonly Dictionary<nint, nint> _monitorByWindow = [];

    public bool Assign(nint windowHandle, nint monitorHandle)
    {
        if (windowHandle == nint.Zero || monitorHandle == nint.Zero)
        {
            return false;
        }

        if (_monitorByWindow.TryGetValue(windowHandle, out var existingMonitor)
            && existingMonitor == monitorHandle)
        {
            return false;
        }

        _monitorByWindow[windowHandle] = monitorHandle;
        return true;
    }

    public bool Remove(nint windowHandle)
    {
        return _monitorByWindow.Remove(windowHandle);
    }

    public bool TryGetMonitor(nint windowHandle, out nint monitorHandle)
    {
        return _monitorByWindow.TryGetValue(windowHandle, out monitorHandle);
    }

    public bool MoveToAdjacentMonitor(
        nint windowHandle,
        int direction,
        IReadOnlyList<nint> orderedMonitorHandles,
        out nint destinationMonitor)
    {
        ArgumentNullException.ThrowIfNull(orderedMonitorHandles);
        destinationMonitor = nint.Zero;
        if (direction == 0
            || !TryGetMonitor(windowHandle, out var currentMonitor)
            || orderedMonitorHandles.Count < 2)
        {
            return false;
        }

        var currentIndex = FindIndex(orderedMonitorHandles, currentMonitor);
        if (currentIndex < 0)
        {
            return false;
        }

        destinationMonitor = orderedMonitorHandles[(currentIndex + direction + orderedMonitorHandles.Count)
            % orderedMonitorHandles.Count];
        return Assign(windowHandle, destinationMonitor);
    }

    private static int FindIndex(IReadOnlyList<nint> handles, nint handle)
    {
        for (var index = 0; index < handles.Count; index++)
        {
            if (handles[index] == handle)
            {
                return index;
            }
        }

        return -1;
    }

    public IReadOnlyList<nint> GetWindows(nint monitorHandle, IReadOnlyList<nint> orderedHandles)
    {
        ArgumentNullException.ThrowIfNull(orderedHandles);
        return orderedHandles
            .Where(handle => _monitorByWindow.TryGetValue(handle, out var assignedMonitor)
                && assignedMonitor == monitorHandle)
            .ToArray();
    }
}
