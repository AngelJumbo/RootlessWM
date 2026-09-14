namespace RootlessWM.Domain;

public sealed class MouseFocusController(
    IWindowCommander windowCommander,
    Func<nint, bool> canFocus,
    Func<nint> getForegroundWindow,
    Func<nint, nint, bool> isOwnedBy)
{
    public bool TryFocus(nint windowHandle)
    {
        var foregroundWindow = getForegroundWindow();
        if (windowHandle == nint.Zero
            || windowHandle == foregroundWindow
            || isOwnedBy(foregroundWindow, windowHandle)
            || !canFocus(windowHandle))
        {
            return false;
        }

        return windowCommander.Focus(windowHandle);
    }
}
