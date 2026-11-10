namespace RootlessWM.Domain;

// Scale is the monitor's effective DPI scale (1F = 96 DPI), used to convert logical sizes
// (e.g. the status bar height) into the physical pixels Bounds is expressed in.
public readonly record struct MonitorWorkArea(nint Handle, WindowBounds Bounds, bool IsPrimary, float Scale = 1F)
{
    public bool IsUsable => Handle != nint.Zero && Bounds.IsUsable;
}
