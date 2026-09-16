namespace RootlessWM.Domain;

public sealed record WindowEvent(WindowEventKind Kind, nint Handle);
