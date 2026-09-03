using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class StatusTextTests
{
    [Fact]
    public void Format_ReportsManagementWorkspaceAndLayoutState()
    {
        var text = StatusText.Format(new StatusSnapshot(true, 1, 9, 0.55, 8, 4, 3, 1));

        Assert.Equal("RootlessWM | ON | WS 2/9 | 3 tiled | 55 % master | 1 master win | outer 8 | inner 4", text);
    }
}
