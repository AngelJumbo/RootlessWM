using RootlessWM.App;
using RootlessWM.Platform.Win32;

// Shows logs in the launching terminal, if any; no-op (and no window) when double-clicked or auto-started.
_ = NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);

using var singleInstance = new Mutex(initiallyOwned: true, @"Local\RootlessWM.App", out var isOnlyInstance);
if (!isOnlyInstance)
{
    return 0;
}

return new WmApplication().Run(args);
