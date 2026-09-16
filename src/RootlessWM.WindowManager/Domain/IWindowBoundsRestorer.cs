namespace RootlessWM.Domain;

public interface IWindowBoundsRestorer
{
    PlacementResult Restore(WindowPlacement placement);
}
