using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RootlessWM.Platform.Win32;

internal sealed class MouseFocusSource : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private nint _hook;
    private Action<nint>? _onWindowEntered;

    public MouseFocusSource()
    {
        _callback = HandleMouseEvent;
    }

    public void Start(Action<nint> onWindowEntered)
    {
        ArgumentNullException.ThrowIfNull(onWindowEntered);
        if (_hook != nint.Zero)
        {
            throw new InvalidOperationException("The mouse focus source has already started.");
        }

        _onWindowEntered = onWindowEntered;
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _callback, nint.Zero, 0);
        if (_hook == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_MOUSE_LL) failed.");
        }
    }

    public void Dispose()
    {
        if (_hook != nint.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }

        _onWindowEntered = null;
    }

    private nint HandleMouseEvent(int code, nuint message, nint data)
    {
        if (code >= 0 && message == NativeMethods.WmMouseMove)
        {
            var mouseData = Marshal.PtrToStructure<NativeMethods.MouseHookData>(data);
            var hitWindow = NativeMethods.WindowFromPoint(mouseData.Point);
            var topLevelWindow = hitWindow == nint.Zero
                ? nint.Zero
                : NativeMethods.GetAncestor(hitWindow, NativeMethods.GaRoot);
            if (topLevelWindow != nint.Zero)
            {
                _onWindowEntered?.Invoke(topLevelWindow);
            }
        }

        return NativeMethods.CallNextHookEx(_hook, code, message, data);
    }
}
