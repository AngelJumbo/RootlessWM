using System.Reflection;
using RootlessWM.App;
using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WindowManagerMemoryStateTests
{
    [Fact]
    public void Restore_RetainsOnlyRetryableBoundsInSameHostAndClearsOnSuccess()
    {
        var host = CreateHost();
        var restorer = new RecordingRestorer();
        Field("_windowRestorer").SetValue(host, new WindowRestorer(restorer));
        var original = new ManagedWindowState(1, new WindowBounds(10, 20, 300, 400));
        var retry = new ManagedWindowState(2, new WindowBounds(30, 40, 500, 600));
        Field("_managedWindowStates").SetValue(host, new ManagedWindowState[] { original, retry });
        restorer.RetryHandle = (nint)2;

        Restore(host);

        Assert.Equal([new WindowPlacement(original.Handle, original.OriginalBounds),
            new WindowPlacement(retry.Handle, retry.OriginalBounds)], restorer.Placements);
        Assert.Equal(retry, Assert.Single(States(host)));
        Assert.Empty(States(CreateHost()));
        restorer.RetryHandle = nint.Zero;
        restorer.Placements.Clear();

        Restore(host);

        Assert.Equal(new WindowPlacement(retry.Handle, retry.OriginalBounds), Assert.Single(restorer.Placements));
        Assert.Empty(States(host));
        restorer.Placements.Clear();
        Restore(host);
        Assert.Empty(restorer.Placements);
    }

    [Theory]
    [InlineData("--tile")]
    [InlineData("--untile")]
    public void Run_RemovedStandaloneCommandsFailWithoutChangingWindows(string command)
    {
        var host = CreateHost();
        var run = HostType.GetMethod("Run");
        Assert.NotNull(run);

        Assert.Equal(1, run.Invoke(host, [new[] { command, "--no-logs" }]));
        Assert.Equal(1, new WmApplication().Run([command, "--no-logs"]));
        Assert.Empty(States(host));
    }

    private static readonly Type HostType = typeof(WindowRestorer).Assembly.GetType("RootlessWM.WindowManager.WindowManagerHost")
        ?? throw new InvalidOperationException("WindowManagerHost was not found.");

    private static object CreateHost()
    {
        var host = Activator.CreateInstance(HostType)
            ?? throw new InvalidOperationException("WindowManagerHost could not be created.");
        Field("_log").SetValue(host, new ConsoleDiagnosticLog(writeToFile: false));
        return host;
    }

    private static FieldInfo Field(string name)
        => HostType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Host field {name} was not found.");

    private static IReadOnlyList<ManagedWindowState> States(object host)
        => Assert.IsAssignableFrom<IReadOnlyList<ManagedWindowState>>(Field("_managedWindowStates").GetValue(host));

    private static void Restore(object host)
    {
        var restore = HostType.GetMethod("RestoreManagedWindows", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(restore);
        restore.Invoke(host, null);
    }

    private sealed class RecordingRestorer : IWindowBoundsRestorer
    {
        public nint RetryHandle { get; set; }
        public List<WindowPlacement> Placements { get; } = [];

        public PlacementResult Restore(WindowPlacement placement)
        {
            Placements.Add(placement);
            return placement.Handle == RetryHandle
                ? PlacementResult.Skipped(placement.Handle, "restore_window_pos_failed_1400")
                : PlacementResult.Success(placement.Handle);
        }
    }
}
