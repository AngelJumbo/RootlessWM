namespace RootlessWM.App;

/// <summary>
/// Launches processes through explorer.exe's Shell COM server so they inherit the
/// interactive user's normal-integrity token instead of RootlessWM's elevated one.
/// </summary>
internal static class ShellProcessLauncher
{
    public static void LaunchDeElevated(string fileName, string? arguments, string? workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new InvalidOperationException("Shell.Application COM type is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Failed to activate Shell.Application.");
        try
        {
            shell.ShellExecute(fileName, arguments ?? string.Empty, workingDirectory ?? string.Empty, "open", 1);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
        }
    }
}
