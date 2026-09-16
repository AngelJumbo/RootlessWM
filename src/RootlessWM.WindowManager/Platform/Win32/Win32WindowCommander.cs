using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class Win32WindowCommander : IWindowCommander
{
    public bool Focus(nint handle)
    {
        if (!NativeMethods.IsWindow(handle))
        {
            return false;
        }

        if (NativeMethods.SetForegroundWindow(handle))
        {
            return true;
        }

        var foregroundHandle = NativeMethods.GetForegroundWindow();
        var foregroundThreadId = NativeMethods.GetWindowThreadProcessId(foregroundHandle, out _);
        var targetThreadId = NativeMethods.GetWindowThreadProcessId(handle, out _);
        var currentThreadId = NativeMethods.GetCurrentThreadId();
        _ = NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PmNoRemove);
        if (foregroundThreadId == 0
            || targetThreadId == 0)
        {
            return false;
        }

        var attachedToForeground = foregroundThreadId != currentThreadId
            && NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
        if (foregroundThreadId != currentThreadId && !attachedToForeground)
        {
            return false;
        }

        var attachedToTarget = targetThreadId != currentThreadId
            && targetThreadId != foregroundThreadId
            && NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
        if (targetThreadId != currentThreadId
            && targetThreadId != foregroundThreadId
            && !attachedToTarget)
        {
            _ = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            return false;
        }

        try
        {
            _ = NativeMethods.BringWindowToTop(handle);
            _ = NativeMethods.SetActiveWindow(handle);
            _ = NativeMethods.SetFocus(handle);
            _ = NativeMethods.SetForegroundWindow(handle);
            return NativeMethods.GetForegroundWindow() == handle;
        }
        finally
        {
            if (attachedToTarget)
            {
                _ = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
            }

            if (attachedToForeground)
            {
                _ = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
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
