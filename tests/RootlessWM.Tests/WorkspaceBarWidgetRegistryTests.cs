using RootlessWM.App;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceBarWidgetRegistryTests
{
    [Fact]
    public void CreateDefault_ContainsCpuMemoryClock()
    {
        var registry = WorkspaceBarWidgetRegistry.CreateDefault();

        Assert.True(registry.TryGet("cpu", out _));
        Assert.True(registry.TryGet("memory", out _));
        Assert.True(registry.TryGet("clock", out _));
    }

    [Fact]
    public void CreateDefault_ContainsTier1Widgets()
    {
        var registry = WorkspaceBarWidgetRegistry.CreateDefault();

        Assert.True(registry.TryGet("date", out _));
        Assert.True(registry.TryGet("uptime", out _));
        Assert.True(registry.TryGet("battery", out _));
        Assert.True(registry.TryGet("disk", out _));
        Assert.True(registry.TryGet("network", out _));
    }

    [Fact]
    public void TryGet_IsCaseInsensitive()
    {
        var registry = WorkspaceBarWidgetRegistry.CreateDefault();

        Assert.True(registry.TryGet("CPU", out var provider));
        Assert.Equal("cpu", provider.Key);
    }

    [Fact]
    public void TryGet_UnknownKey_ReturnsFalse()
    {
        var registry = WorkspaceBarWidgetRegistry.CreateDefault();

        Assert.False(registry.TryGet("bogus", out _));
    }

    [Fact]
    public void Create_TextWidget_UsesConfiguredText()
    {
        var registry = WorkspaceBarWidgetRegistry.CreateDefault();
        var options = new WorkspaceBarWidgetOptions("text", "", default, default, default, default, "Hello");

        var provider = registry.Create("text", options);

        Assert.NotNull(provider);
        Assert.Equal("Hello", provider.GetText());
    }

    [Fact]
    public void Create_UnknownKey_ReturnsNull()
    {
        var registry = WorkspaceBarWidgetRegistry.CreateDefault();
        var options = new WorkspaceBarWidgetOptions("bogus", "", default, default, default, default);

        Assert.Null(registry.Create("bogus", options));
    }

    [Fact]
    public void Create_CommandWidget_ReturnsCommandProvider()
    {
        var registry = WorkspaceBarWidgetRegistry.CreateDefault();
        var options = new WorkspaceBarWidgetOptions("command", "", default, default, default, default, Command: "ver");

        var provider = registry.Create("command", options);

        Assert.IsType<CommandWidgetProvider>(provider);
        (provider as IDisposable)?.Dispose();
    }
}
