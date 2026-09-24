namespace RootlessWM.Platform.Win32;

/// <summary>
/// Suppresses or restores the default DWM drop shadow for a top-level window via
/// DWMWA_NCRENDERING_POLICY. Isolated from tiling/tracking logic: callers decide when a
/// window becomes managed/unmanaged and simply invoke Suppress/Restore accordingly.
/// </summary>
internal sealed class WindowShadowController
{
    public void Suppress(nint handle)
    {
        SetPolicy(handle, NativeMethods.DwmncrpDisabled);
    }

    public void Restore(nint handle)
    {
        SetPolicy(handle, NativeMethods.DwmncrpUseWindowStyle);
    }

    public void Apply(IEnumerable<nint> handles, bool disableShadows)
    {
        foreach (var handle in handles)
        {
            if (disableShadows)
            {
                Suppress(handle);
            }
            else
            {
                Restore(handle);
            }
        }
    }

    private static void SetPolicy(nint handle, uint policy)
    {
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaNcrenderingPolicy, ref policy, sizeof(uint));
    }
}
