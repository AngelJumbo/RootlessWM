using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class MouseFocusControllerTests
{
    [Fact]
    public void TryFocus_EligibleNonForegroundWindow_FocusesWindow()
    {
        var commander = new RecordingWindowCommander();
        var controller = new MouseFocusController(commander, handle => handle == (nint)2, () => (nint)1);

        var focused = controller.TryFocus((nint)2);

        Assert.True(focused);
        Assert.Equal((nint)2, commander.FocusedHandle);
    }

    [Fact]
    public void TryFocus_ForegroundOrIneligibleWindow_DoesNotFocusWindow()
    {
        var commander = new RecordingWindowCommander();
        var controller = new MouseFocusController(commander, handle => handle == (nint)2, () => (nint)2);

        Assert.False(controller.TryFocus((nint)2));
        Assert.False(controller.TryFocus((nint)3));
        Assert.Null(commander.FocusedHandle);
    }

    private sealed class RecordingWindowCommander : IWindowCommander
    {
        public nint? FocusedHandle { get; private set; }

        public bool Focus(nint handle)
        {
            FocusedHandle = handle;
            return true;
        }

        public bool Close(nint handle) => true;

        public bool Show(nint handle) => true;

        public bool Hide(nint handle) => true;
    }
}
