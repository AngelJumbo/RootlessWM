namespace RootlessWM.Domain;

public interface IWindowPlacementApplier
{
    PlacementResult Apply(WindowPlacement placement);
}
