using System.ComponentModel;
using System.Runtime.InteropServices;
using RootlessWM.Domain;

namespace RootlessWM.Platform.Win32;

internal sealed class WindowEventSource : IDisposable
{
    // WM_APP: private message used to re-deliver a window for eligibility recheck on the loop thread.
    private const uint WmAppRecheckWindow = 0x8000;

    private readonly NativeMethods.WinEventProc _callback;
    private readonly List<nint> _hooks = [];
    private bool _disposed;
    private uint _threadId;
    private Action<WindowEvent>? _onEvent;

    public WindowEventSource()
    {
        _callback = HandleNativeEvent;
    }

    public void Start(Action<WindowEvent> onEvent)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hooks.Count > 0)
        {
            throw new InvalidOperationException("The event source has already started.");
        }

        _onEvent = onEvent;
        _threadId = NativeMethods.GetCurrentThreadId();
        _ = NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PmNoRemove);
        try
        {
            RegisterHook(NativeMethods.EventSystemForeground);
            RegisterHook(NativeMethods.EventObjectCreate);
            RegisterHook(NativeMethods.EventObjectDestroy);
            RegisterHook(NativeMethods.EventObjectShow);
            RegisterHook(NativeMethods.EventObjectHide);
            RegisterHook(NativeMethods.EventObjectLocationChange);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void RunMessageLoop(Action<uint, nuint>? onMessage = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hooks.Count == 0)
        {
            throw new InvalidOperationException("The event source has not started.");
        }

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

            if (message.MessageId == WmAppRecheckWindow && message.WindowHandle == nint.Zero)
            {
                _onEvent?.Invoke(new WindowEvent(WindowEventKind.Shown, unchecked((nint)message.WParam)));
                continue;
            }

            onMessage?.Invoke(message.MessageId, message.WParam);

            _ = NativeMethods.TranslateMessage(message);
            _ = NativeMethods.DispatchMessage(message);
        }
    }

    public void ScheduleRecheck(nint windowHandle, TimeSpan delay)
    {
        var threadId = _threadId;
        if (_disposed || threadId == 0)
        {
            return;
        }

        _ = Task.Delay(delay).ContinueWith(
            _ => NativeMethods.PostThreadMessage(threadId, WmAppRecheckWindow, unchecked((nuint)windowHandle), nint.Zero),
            TaskScheduler.Default);
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
        if (_disposed)
        {
            return;
        }

        foreach (var hook in _hooks)
        {
            _ = NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
        _disposed = true;
        _threadId = 0;
        _onEvent = null;
    }

    private void RegisterHook(uint eventType)
    {
        var hook = NativeMethods.SetWinEventHook(
            eventType,
            eventType,
            nint.Zero,
            _callback,
            0,
            0,
            NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);

        if (hook == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWinEventHook failed.");
        }

        _hooks.Add(hook);
    }

    private void HandleNativeEvent(
        nint hook,
        uint eventType,
        nint windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (windowHandle == nint.Zero || objectId != NativeMethods.ObjidWindow || childId != NativeMethods.ChildidSelf)
        {
            return;
        }

        var kind = eventType switch
        {
            NativeMethods.EventObjectCreate => WindowEventKind.Created,
            NativeMethods.EventObjectDestroy => WindowEventKind.Destroyed,
            NativeMethods.EventObjectShow => WindowEventKind.Shown,
            NativeMethods.EventObjectHide => WindowEventKind.Hidden,
            NativeMethods.EventSystemForeground => WindowEventKind.Activated,
            NativeMethods.EventObjectLocationChange => WindowEventKind.LocationChanged,
            _ => (WindowEventKind?)null
        };

        if (kind is not null)
        {
            _onEvent?.Invoke(new WindowEvent(kind.Value, windowHandle));
        }
    }
}
