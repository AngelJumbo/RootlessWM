namespace RootlessWM.Platform.Win32;

internal sealed class WindowEnumerator
{
    public IReadOnlyList<nint> GetTopLevelWindowHandles()
    {
        var handles = new List<nint>();
        var enumerated = NativeMethods.EnumWindows(
            (windowHandle, _) =>
            {
                handles.Add(windowHandle);
                return true;
            },
            nint.Zero);

        if (!enumerated)
        {
            throw new InvalidOperationException("EnumWindows failed.");
        }

        return handles;
    }
}
