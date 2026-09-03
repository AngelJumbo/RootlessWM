namespace RootlessWM.Domain;

public static class ManagedWindowStateSet
{
    public static IReadOnlyList<ManagedWindowState> AddIfMissing(
        IReadOnlyList<ManagedWindowState> existingStates,
        TrackedWindow trackedWindow)
    {
        ArgumentNullException.ThrowIfNull(existingStates);

        if (existingStates.Any(state => state.WindowHandle == trackedWindow.Handle.ToInt64()))
        {
            return existingStates;
        }

        return [.. existingStates, new ManagedWindowState(trackedWindow.Handle.ToInt64(), trackedWindow.OriginalBounds)];
    }
}
