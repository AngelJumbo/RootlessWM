namespace RootlessWM.Domain;

public sealed class FullscreenState
{
    private readonly Dictionary<(nint Monitor, int Workspace), nint> _windowsByScope = [];

    public bool Toggle(nint monitorHandle, int workspace, nint windowHandle)
    {
        if (monitorHandle == nint.Zero || windowHandle == nint.Zero)
        {
            return false;
        }

        var scope = (monitorHandle, workspace);
        if (_windowsByScope.TryGetValue(scope, out var current) && current == windowHandle)
        {
            _ = _windowsByScope.Remove(scope);
            return true;
        }

        _windowsByScope[scope] = windowHandle;
        return true;
    }

    public bool TryGetWindow(nint monitorHandle, int workspace, out nint windowHandle)
    {
        return _windowsByScope.TryGetValue((monitorHandle, workspace), out windowHandle);
    }

    public bool IsFullscreen(nint windowHandle)
    {
        return windowHandle != nint.Zero && _windowsByScope.ContainsValue(windowHandle);
    }

    public void Clear()
    {
        _windowsByScope.Clear();
    }

    public void Synchronize(IReadOnlyCollection<nint> managedHandles)
    {
        ArgumentNullException.ThrowIfNull(managedHandles);

        foreach (var scope in _windowsByScope
            .Where(entry => !managedHandles.Contains(entry.Value))
            .Select(entry => entry.Key)
            .ToArray())
        {
            _ = _windowsByScope.Remove(scope);
        }
    }
}
