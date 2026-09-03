namespace RootlessWM.Domain;

public sealed record WindowCandidate(
    nint Handle,
    bool IsVisible,
    bool IsChildWindow,
    bool IsToolWindow,
    bool IsMinimized,
    string ClassName,
    string? ProcessName,
    WindowBounds Bounds,
    uint Style = 0,
    uint ExtendedStyle = 0,
    bool IsCloaked = false,
    bool HasOwner = false);
