namespace RootlessWM.Domain;

public sealed class WorkspaceState(int workspaceCount = 9)
{
    private readonly Dictionary<nint, int> _workspaceByWindow = [];
    private readonly Dictionary<nint, int> _currentWorkspaceByMonitor = [];

    public int WorkspaceCount { get; } = ValidateWorkspaceCount(workspaceCount);

    public WorkspaceStateData Export()
    {
        return new WorkspaceStateData(_workspaceByWindow.ToDictionary(entry => entry.Key.ToInt64(), entry => entry.Value));
    }

    public void Restore(WorkspaceStateData data, IEnumerable<nint> managedHandles)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(managedHandles);
        _workspaceByWindow.Clear();
        var handles = managedHandles.ToHashSet();
        foreach (var entry in data.Assignments)
        {
            var handle = (nint)entry.Key;
            if (handles.Contains(handle))
            {
                ValidateWorkspace(entry.Value);
                _workspaceByWindow[handle] = entry.Value;
            }
        }
    }

    public int GetCurrentWorkspace(nint monitorHandle)
    {
        ValidateMonitor(monitorHandle);
        return _currentWorkspaceByMonitor.TryGetValue(monitorHandle, out var workspace)
            ? workspace
            : 0;
    }

    public void Synchronize(IEnumerable<nint> managedHandles, Func<nint, int>? getDefaultWorkspace = null)
    {
        ArgumentNullException.ThrowIfNull(managedHandles);
        var handles = managedHandles.ToHashSet();
        foreach (var handle in _workspaceByWindow.Keys.Where(handle => !handles.Contains(handle)).ToArray())
        {
            _workspaceByWindow.Remove(handle);
        }

        foreach (var handle in handles)
        {
            if (_workspaceByWindow.ContainsKey(handle))
            {
                continue;
            }

            var workspace = getDefaultWorkspace?.Invoke(handle) ?? 0;
            ValidateWorkspace(workspace);
            _workspaceByWindow[handle] = workspace;
        }
    }

    public void AssignToCurrentWorkspaceIfMissing(nint windowHandle, nint monitorHandle)
    {
        ValidateMonitor(monitorHandle);
        _workspaceByWindow.TryAdd(windowHandle, GetCurrentWorkspace(monitorHandle));
    }

    public bool HasWorkspace(nint windowHandle)
    {
        return _workspaceByWindow.ContainsKey(windowHandle);
    }

    public bool IsInCurrentWorkspace(nint windowHandle, nint monitorHandle)
    {
        return _workspaceByWindow.TryGetValue(windowHandle, out var workspace)
            && workspace == GetCurrentWorkspace(monitorHandle);
    }

    public IReadOnlyList<nint> GetWindows(
        nint monitorHandle,
        IReadOnlyList<nint> orderedHandles,
        Func<nint, nint> getMonitorHandle)
    {
        ValidateMonitor(monitorHandle);
        ArgumentNullException.ThrowIfNull(orderedHandles);
        ArgumentNullException.ThrowIfNull(getMonitorHandle);
        var workspace = GetCurrentWorkspace(monitorHandle);
        return orderedHandles
            .Where(handle => _workspaceByWindow.TryGetValue(handle, out var assignedWorkspace)
                && assignedWorkspace == workspace
                && getMonitorHandle(handle) == monitorHandle)
            .ToArray();
    }

    public bool Switch(nint monitorHandle, int direction)
    {
        ValidateMonitor(monitorHandle);
        if (direction == 0)
        {
            return false;
        }

        _currentWorkspaceByMonitor[monitorHandle] = Wrap(GetCurrentWorkspace(monitorHandle) + direction);
        return true;
    }

    public bool Select(nint monitorHandle, int workspace)
    {
        ValidateMonitor(monitorHandle);
        ValidateWorkspace(workspace);
        if (GetCurrentWorkspace(monitorHandle) == workspace)
        {
            return false;
        }

        _currentWorkspaceByMonitor[monitorHandle] = workspace;
        return true;
    }

    public bool MoveToAdjacentWorkspace(nint windowHandle, nint monitorHandle, int direction)
    {
        ValidateMonitor(monitorHandle);
        if (direction == 0
            || !_workspaceByWindow.ContainsKey(windowHandle))
        {
            return false;
        }

        _workspaceByWindow[windowHandle] = Wrap(GetCurrentWorkspace(monitorHandle) + direction);
        return true;
    }

    public bool MoveToWorkspace(nint windowHandle, int workspace)
    {
        ValidateWorkspace(workspace);
        if (!_workspaceByWindow.TryGetValue(windowHandle, out var currentWorkspace)
            || currentWorkspace == workspace)
        {
            return false;
        }

        _workspaceByWindow[windowHandle] = workspace;
        return true;
    }

    public bool MoveToCurrentWorkspace(nint windowHandle, nint monitorHandle)
    {
        ValidateMonitor(monitorHandle);
        var workspace = GetCurrentWorkspace(monitorHandle);
        if (_workspaceByWindow.TryGetValue(windowHandle, out var currentWorkspace)
            && currentWorkspace == workspace)
        {
            return false;
        }

        _workspaceByWindow[windowHandle] = workspace;
        return true;
    }

    private int Wrap(int workspace)
    {
        return (workspace % WorkspaceCount + WorkspaceCount) % WorkspaceCount;
    }

    private void ValidateWorkspace(int workspace)
    {
        if (workspace < 0 || workspace >= WorkspaceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(workspace));
        }
    }

    private static void ValidateMonitor(nint monitorHandle)
    {
        if (monitorHandle == nint.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(monitorHandle));
        }
    }

    private static int ValidateWorkspaceCount(int workspaceCount)
    {
        return workspaceCount is >= 2 and <= 32
            ? workspaceCount
            : throw new ArgumentOutOfRangeException(nameof(workspaceCount));
    }
}

public sealed record WorkspaceStateData(IReadOnlyDictionary<long, int> Assignments);
