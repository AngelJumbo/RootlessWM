namespace RootlessWM.Domain;

public sealed class ManagementState
{
    public bool IsEnabled { get; private set; }

    public bool Enable()
    {
        if (IsEnabled)
        {
            return false;
        }

        IsEnabled = true;
        return true;
    }

    public bool Disable()
    {
        if (!IsEnabled)
        {
            return false;
        }

        IsEnabled = false;
        return true;
    }
}
