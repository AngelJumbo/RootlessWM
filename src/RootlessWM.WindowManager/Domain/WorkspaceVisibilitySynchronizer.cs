namespace RootlessWM.Domain;

public sealed class WorkspaceVisibilitySynchronizer(
    WorkspaceState workspaceState,
    MonitorOwnership monitorOwnership,
    IWindowCommander windowCommander,
    ManagementState managementState)
{
    public void Synchronize(IReadOnlyList<nint> managedHandles)
    {
        ArgumentNullException.ThrowIfNull(managedHandles);

        if (!managementState.IsEnabled)
        {
            return;
        }

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
