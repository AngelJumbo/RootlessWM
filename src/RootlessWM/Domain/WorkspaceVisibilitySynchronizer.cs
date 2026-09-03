namespace RootlessWM.Domain;

public sealed class WorkspaceVisibilitySynchronizer(
    WorkspaceState workspaceState,
    MonitorOwnership monitorOwnership,
    IWindowCommander windowCommander)
{
    public void Synchronize(IReadOnlyList<nint> managedHandles)
    {
        ArgumentNullException.ThrowIfNull(managedHandles);

        foreach (var handle in managedHandles)
        {
            if (!monitorOwnership.TryGetMonitor(handle, out var monitorHandle)
                || workspaceState.IsInCurrentWorkspace(handle, monitorHandle))
            {
                _ = windowCommander.Show(handle);
            }
            else
            {
                _ = windowCommander.Hide(handle);
            }
        }
    }
}
