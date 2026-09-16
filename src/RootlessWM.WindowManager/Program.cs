namespace RootlessWM.WindowManager;

internal static class Program
{
    private static int Main(string[] args)
    {
        return new WindowManagerHost().Run(args);
    }
}
