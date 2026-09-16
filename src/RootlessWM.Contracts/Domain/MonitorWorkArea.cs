namespace RootlessWM.Domain;

public readonly record struct MonitorWorkArea(nint Handle, WindowBounds Bounds, bool IsPrimary)
{
    public bool IsUsable => Handle != nint.Zero && Bounds.IsUsable;
}
