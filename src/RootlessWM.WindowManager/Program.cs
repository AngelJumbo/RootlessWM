using RootlessWM.Platform.Win32;

namespace RootlessWM.WindowManager;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Shows logs in the launching terminal, if any; no-op (and no window) when started by the scheduled task.
        _ = NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);

        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\RootlessWM.WindowManager", out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            return 0;
        }

        return new WindowManagerHost().Run(args);
    }
}
