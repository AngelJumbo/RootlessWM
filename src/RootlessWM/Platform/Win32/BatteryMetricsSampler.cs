namespace RootlessWM.Platform.Win32;

internal sealed class BatteryMetricsSampler
{
    public NativeMethods.SystemPowerStatus? Sample()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var status))
        {
            return null;
        }

        return status;
    }
}
