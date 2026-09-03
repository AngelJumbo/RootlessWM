namespace RootlessWM.Domain;

public sealed class TilingCommandProcessor(TilingState tilingState, IWindowCommander windowCommander)
{
    public bool Execute(
        TilingCommand command,
        nint activeHandle,
        Action retile,
        IReadOnlyList<nint>? focusScopeHandles = null)
    {
        ArgumentNullException.ThrowIfNull(retile);

        return command switch
        {
            TilingCommand.PromoteToMaster => RetileWhen(tilingState.PromoteToMaster(activeHandle), retile),
            TilingCommand.SwapWithNext => RetileWhen(tilingState.SwapWithNext(activeHandle), retile),
            TilingCommand.SwapWithPrevious => RetileWhen(tilingState.SwapWithPrevious(activeHandle), retile),
            TilingCommand.ToggleFloating => RetileWhen(tilingState.ToggleFloating(activeHandle), retile),
            TilingCommand.FocusNext => FocusNext(activeHandle, focusScopeHandles),
            TilingCommand.FocusPrevious => FocusPrevious(activeHandle, focusScopeHandles),
            TilingCommand.Close => windowCommander.Close(activeHandle),
            _ => false
        };
    }

    public bool TryGetFocusTarget(
        TilingCommand command,
        nint activeHandle,
        out nint targetHandle,
        IReadOnlyList<nint>? focusScopeHandles = null)
    {
        targetHandle = nint.Zero;
        var direction = command switch
        {
            TilingCommand.FocusNext => 1,
            TilingCommand.FocusPrevious => -1,
            _ => 0
        };
        if (direction == 0)
        {
            return false;
        }

        var handles = focusScopeHandles ?? tilingState.TiledHandles;
        var activeIndex = FindIndex(handles, activeHandle);
        if (activeIndex < 0 || handles.Count < 2)
        {
            return false;
        }

        targetHandle = handles[(activeIndex + direction + handles.Count) % handles.Count];
        return true;
    }

    private bool FocusNext(nint activeHandle, IReadOnlyList<nint>? focusScopeHandles)
    {
        return TryGetFocusTarget(TilingCommand.FocusNext, activeHandle, out var targetHandle, focusScopeHandles)
            && windowCommander.Focus(targetHandle);
    }

    private bool FocusPrevious(nint activeHandle, IReadOnlyList<nint>? focusScopeHandles)
    {
        return TryGetFocusTarget(TilingCommand.FocusPrevious, activeHandle, out var targetHandle, focusScopeHandles)
            && windowCommander.Focus(targetHandle);
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

    private static bool RetileWhen(bool changed, Action retile)
    {
        if (changed)
        {
            retile();
        }

        return changed;
    }
}
