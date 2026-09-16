using System.ComponentModel;
using System.Runtime.InteropServices;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class HotkeyMessageLoop : IDisposable
{
    private bool _disposed;
    private uint _threadId;

    public void Run(Action<uint, nuint> onMessage)
    {
        ArgumentNullException.ThrowIfNull(onMessage);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _threadId = NativeMethods.GetCurrentThreadId();
        _ = NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PmNoRemove);
        while (true)
        {
            var result = NativeMethods.GetMessage(out var message, nint.Zero, 0, 0);
            if (result == 0)
            {
                return;
            }

            if (result == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetMessage failed.");
            }

            if (message.MessageId == NativeMethods.WmHotkey)
            {
                onMessage(message.MessageId, message.WParam);
            }

            _ = NativeMethods.TranslateMessage(message);
            _ = NativeMethods.DispatchMessage(message);
        }
    }

    public void Stop()
    {
        if (!_disposed && _threadId != 0)
        {
            _ = NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmQuit, 0, nint.Zero);
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
