namespace RootlessWM.Domain;

public sealed record TilingOperationResult(
    IReadOnlyList<WindowPlacement> PlannedPlacements,
    IReadOnlyList<PlacementResult> PlacementResults);
