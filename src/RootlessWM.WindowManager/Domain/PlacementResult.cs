namespace RootlessWM.Domain;

public readonly record struct PlacementResult(nint Handle, bool Applied, string Reason)
{
    public static PlacementResult Success(nint handle) => new(handle, true, "applied");

    public static PlacementResult Skipped(nint handle, string reason) => new(handle, false, reason);
}
