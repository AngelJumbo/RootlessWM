using RootlessWM.Protocol;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementOptionsPayloadTests
{
    [Fact]
    public void Construction_ExposesGivenValues()
    {
        var payload = new ManagementOptionsPayload(
            0.5, 8, 4, "MasterStack", 2, new[] { "explorer.exe" }, new[] { "Shell_TrayWnd" }, true, "HideWhenTiled");

        Assert.Equal(0.5, payload.MasterRatio);
        Assert.Equal(8, payload.OuterGap);
        Assert.Equal(4, payload.InnerGap);
        Assert.Equal("MasterStack", payload.LayoutMode);
        Assert.Equal(2, payload.MasterCount);
        Assert.Equal(new[] { "explorer.exe" }, payload.ExcludedExecutables);
        Assert.Equal(new[] { "Shell_TrayWnd" }, payload.ExcludedWindowClasses);
        Assert.True(payload.FocusFollowsMouse);
        Assert.Equal("HideWhenTiled", payload.ToggleExplorerBehaviour);
    }
}
