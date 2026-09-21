using RootlessWM.Platform.Win32;

namespace RootlessWM.WindowManager;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Must run before any monitor/window geometry is queried: without it the process is
        // DPI-unaware, so Windows hands it virtualized (96-DPI) rectangles from GetWindowRect /
        // GetMonitorInfo while the low-level mouse hook still reports real physical coordinates.
        // On any monitor above 100% scaling that mismatch shifts the tiling math and mouse hit
        // testing apart, so hovering the right side of a tile can resolve to the wrong window.
        _ = NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorAwareV2);

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
