using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class Win32WindowCommander : IWindowCommander
{
    public bool Focus(nint handle)
    {
        return NativeMethods.IsWindow(handle) && NativeMethods.SetForegroundWindow(handle);
    }

    public bool Close(nint handle)
    {
        return NativeMethods.IsWindow(handle) && NativeMethods.PostMessage(handle, NativeMethods.WmClose, 0, nint.Zero);
    }

    public bool Show(nint handle)
    {
        return NativeMethods.IsWindow(handle) && NativeMethods.ShowWindow(handle, NativeMethods.SwShow);
    }

    public bool Hide(nint handle)
    {
        return NativeMethods.IsWindow(handle) && NativeMethods.ShowWindow(handle, NativeMethods.SwHide);
    }
}
