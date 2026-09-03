namespace RootlessWM.Domain;

public static class MonitorWorkAreaResolver
{
    public static WindowBounds Resolve(
        WindowBounds monitorBounds,
        WindowBounds reservedWorkArea,
        bool hasVisibleTaskbar)
    {
        return hasVisibleTaskbar ? reservedWorkArea : monitorBounds;
    }
}
