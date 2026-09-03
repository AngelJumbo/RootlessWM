using RootlessWM.Domain;
using Xunit;

namespace RootlessWM.Tests;

public sealed class ManagementStateTests
{
    [Fact]
    public void EnableAndDisable_ChangesStateOnlyOncePerTransition()
    {
        var state = new ManagementState();

        Assert.True(state.Enable());
        Assert.True(state.IsEnabled);
        Assert.False(state.Enable());
        Assert.True(state.Disable());
        Assert.False(state.IsEnabled);
        Assert.False(state.Disable());
    }
}
