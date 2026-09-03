namespace RootlessWM.Domain;

public sealed class MouseFocusController(
    IWindowCommander windowCommander,
    Func<nint, bool> canFocus,
    Func<nint> getForegroundWindow)
{
    public bool TryFocus(nint windowHandle)
    {
        if (windowHandle == nint.Zero
            || windowHandle == getForegroundWindow()
            || !canFocus(windowHandle))
        {
            return false;
        }

        return windowCommander.Focus(windowHandle);
    }
}
