namespace RootlessWM.Domain;

public sealed class WorkspaceCommandProcessor(
    WorkspaceState workspaceState)
{
    public bool Execute(
        TilingCommand command,
        nint activeHandle,
        nint activeMonitor,
        Action synchronizeVisibility,
        Action retile)
    {
        ArgumentNullException.ThrowIfNull(synchronizeVisibility);
        ArgumentNullException.ThrowIfNull(retile);

        var workspace = GetWorkspace(command);
        if (workspace is not null)
        {
            var changed = IsMoveCommand(command)
                ? workspaceState.MoveToWorkspace(activeHandle, workspace.Value)
                : workspaceState.Select(activeMonitor, workspace.Value);
            if (!changed)
            {
                return false;
            }

            synchronizeVisibility();
            retile();
            return true;
        }

        var direction = GetDirection(command);
        if (direction == 0)
        {
            return false;
        }

        if (command is TilingCommand.NextWorkspace or TilingCommand.PreviousWorkspace)
        {
            _ = workspaceState.Switch(activeMonitor, direction);
            synchronizeVisibility();
            retile();
            return true;
        }

        if (workspaceState.MoveToAdjacentWorkspace(activeHandle, activeMonitor, direction))
        {
            synchronizeVisibility();
            retile();
            return true;
        }

        return false;
    }

    private static int GetDirection(TilingCommand command)
    {
        return command switch
        {
            TilingCommand.NextWorkspace or TilingCommand.MoveToNextWorkspace => 1,
            TilingCommand.PreviousWorkspace or TilingCommand.MoveToPreviousWorkspace => -1,
            _ => 0
        };
    }

    private static int? GetWorkspace(TilingCommand command)
    {
        return command switch
        {
            TilingCommand.SelectWorkspace1 or TilingCommand.MoveToWorkspace1 => 0,
            TilingCommand.SelectWorkspace2 or TilingCommand.MoveToWorkspace2 => 1,
            TilingCommand.SelectWorkspace3 or TilingCommand.MoveToWorkspace3 => 2,
            TilingCommand.SelectWorkspace4 or TilingCommand.MoveToWorkspace4 => 3,
            TilingCommand.SelectWorkspace5 or TilingCommand.MoveToWorkspace5 => 4,
            TilingCommand.SelectWorkspace6 or TilingCommand.MoveToWorkspace6 => 5,
            TilingCommand.SelectWorkspace7 or TilingCommand.MoveToWorkspace7 => 6,
            TilingCommand.SelectWorkspace8 or TilingCommand.MoveToWorkspace8 => 7,
            TilingCommand.SelectWorkspace9 or TilingCommand.MoveToWorkspace9 => 8,
            _ => null
        };
    }

    private static bool IsMoveCommand(TilingCommand command)
    {
        return command is >= TilingCommand.MoveToWorkspace1 and <= TilingCommand.MoveToWorkspace9;
    }
}
