namespace RootlessWM.Domain;

public readonly record struct WindowPlacement(nint Handle, WindowBounds Bounds);
