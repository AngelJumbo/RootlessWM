namespace RootlessWM.Domain;

public sealed class WindowRecord(nint handle, WindowBounds originalBounds)
{
    public nint Handle { get; } = handle;

    public WindowBounds OriginalBounds { get; } = originalBounds;
}
