namespace RootlessWM.Domain;

public sealed class MonitorCommandProcessor(
    TilingState tilingState,
    MonitorOwnership monitorOwnership,
    IWindowCommander windowCommander)
{
    public bool Execute(
        TilingCommand command,
        nint activeHandle,
        IReadOnlyList<nint> orderedMonitorHandles,
        Action retile)
    {
        ArgumentNullException.ThrowIfNull(orderedMonitorHandles);
        ArgumentNullException.ThrowIfNull(retile);

        if (command is TilingCommand.FocusNextMonitor or TilingCommand.FocusPreviousMonitor)
        {
            return TryGetFocusTarget(command, activeHandle, orderedMonitorHandles, out var target)
                && windowCommander.Focus(target);
        }

        var direction = GetDirection(command);
        if (direction == 0 || !TryGetDestinationMonitor(activeHandle, direction, orderedMonitorHandles, out var destinationMonitor))
        {
            return false;
        }

        if (command is TilingCommand.MoveToNextMonitor or TilingCommand.MoveToPreviousMonitor
            && monitorOwnership.Assign(activeHandle, destinationMonitor))
        {
            retile();
            return true;
        }

        return false;
    }

    public bool TryGetFocusTarget(
        TilingCommand command,
        nint activeHandle,
        IReadOnlyList<nint> orderedMonitorHandles,
        out nint targetHandle)
    {
        targetHandle = nint.Zero;
        var direction = command switch
        {
            TilingCommand.FocusNextMonitor => 1,
            TilingCommand.FocusPreviousMonitor => -1,
            _ => 0
        };
        if (direction == 0
            || !TryGetDestinationMonitor(activeHandle, direction, orderedMonitorHandles, out var destinationMonitor))
        {
            return false;
        }

        targetHandle = monitorOwnership.GetWindows(destinationMonitor, tilingState.TiledHandles).FirstOrDefault();
        return targetHandle != nint.Zero;
    }

    private bool TryGetDestinationMonitor(
        nint activeHandle,
        int direction,
        IReadOnlyList<nint> orderedMonitorHandles,
        out nint destinationMonitor)
    {
        destinationMonitor = nint.Zero;
        if (direction == 0 || !monitorOwnership.TryGetMonitor(activeHandle, out var currentMonitor))
        {
            return false;
        }

        var currentIndex = FindIndex(orderedMonitorHandles, currentMonitor);
        if (currentIndex < 0 || orderedMonitorHandles.Count < 2)
        {
            return false;
        }

        destinationMonitor = orderedMonitorHandles[
            (currentIndex + direction + orderedMonitorHandles.Count) % orderedMonitorHandles.Count];
        return true;
    }

    private static int GetDirection(TilingCommand command)
    {
        return command switch
        {
            TilingCommand.FocusNextMonitor or TilingCommand.MoveToNextMonitor => 1,
            TilingCommand.FocusPreviousMonitor or TilingCommand.MoveToPreviousMonitor => -1,
            _ => 0
        };
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
}
