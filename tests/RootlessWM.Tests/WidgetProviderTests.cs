using RootlessWM.App;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WidgetProviderTests
{
    [Fact]
    public void ClockWidgetProvider_GetText_ContainsTime()
    {
        var provider = new ClockWidgetProvider();

        var text = provider.GetText();

        Assert.Contains(DateTime.Now.ToString("HH:mm"), text);
    }

    [Fact]
    public void DateWidgetProvider_GetText_ContainsDate()
    {
        var provider = new DateWidgetProvider();

        var text = provider.GetText();

        Assert.Contains(DateTime.Now.ToString("yyyy-MM-dd"), text);
    }

    [Fact]
    public void UptimeWidgetProvider_GetText_IsNonEmpty()
    {
        var provider = new UptimeWidgetProvider();

        var text = provider.GetText();

        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void DiskWidgetProvider_GetText_IsNonEmpty()
    {
        var provider = new DiskWidgetProvider();

        var text = provider.GetText();

        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void TextWidgetProvider_GetText_ReturnsLiteral()
    {
        var provider = new TextWidgetProvider("Hello");

        Assert.Equal("Hello", provider.GetText());
    }

    [Fact]
    public void CpuWidgetProvider_GetText_EndsWithPercent()
    {
        var provider = new CpuWidgetProvider(new RootlessWM.Platform.Win32.SystemMetricsSampler());

        var text = provider.GetText();

        Assert.EndsWith("%", text);
    }

    [Fact]
    public void MemoryWidgetProvider_GetText_EndsWithPercent()
    {
        var provider = new MemoryWidgetProvider(new RootlessWM.Platform.Win32.SystemMetricsSampler());

        var text = provider.GetText();

        Assert.EndsWith("%", text);
    }

    [Fact]
    public void BatteryWidgetProvider_GetText_IsEmptyOrPercent()
    {
        var provider = new BatteryWidgetProvider(new RootlessWM.Platform.Win32.BatteryMetricsSampler());

        var text = provider.GetText();

        Assert.True(string.IsNullOrEmpty(text) || text.Contains('%'));
    }

    [Fact]
    public void NetworkWidgetProvider_GetText_IsEmptyOrContainsRate()
    {
        var provider = new NetworkWidgetProvider(new RootlessWM.Platform.Win32.NetworkMetricsSampler());

        var text = provider.GetText();

        Assert.True(string.IsNullOrEmpty(text) || text.Contains("↓"));
    }
}
