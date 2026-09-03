namespace RootlessWM.Domain;

public static class RestoreResultPolicy
{
    public static bool RetainsForRetry(PlacementResult result)
    {
        return !result.Applied
            && result.Reason is not "invalid_window"
                and not "invalid_bounds"
                and not "restore_window_pos_failed_5";
    }
}
