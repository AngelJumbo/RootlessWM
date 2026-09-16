using System.Text.Json.Serialization;

namespace RootlessWM.Domain;

public sealed record ManagedWindowState(long WindowHandle, WindowBounds OriginalBounds)
{
    [JsonIgnore]
    public nint Handle => (nint)WindowHandle;
}
