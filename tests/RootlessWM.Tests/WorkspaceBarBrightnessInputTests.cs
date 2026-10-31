using System.Drawing;
using System.Reflection;
using RootlessWM.App;
using SkiaSharp;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceBarBrightnessInputTests
{
    [Theory]
    [InlineData(false, 1F)]
    [InlineData(false, 1.5F)]
    [InlineData(true, 2F)]
    public void Brightness_WheelHitTestingAndSymbolReload(bool vertical, float scale)
    {
        var brightness = new BrightnessWidgetProviderTests.FakeBrightness { Percent = 50 };
        var log = new ConsoleDiagnosticLog(writeToFile: false);
        var registry = new WorkspaceBarWidgetRegistry([new BrightnessWidgetProvider(brightness, log)]);
        var viewType = typeof(WorkspaceBarController).GetNestedType("WorkspaceBarView", BindingFlags.NonPublic);
        Assert.NotNull(viewType);
        using var view = Assert.IsAssignableFrom<IDisposable>(Activator.CreateInstance(viewType, [9, registry, log]));
        var module = new WorkspaceBarModuleOptions("screen", "brightness", WorkspaceBarModuleMonitor.All,
            WorkspaceBarStyleOptions.Default with { Padding = new(4), MinWidth = 80 },
            Format: "{brightness_symbol} {percent}%")
        {
            BrightnessSymbols = new Dictionary<string, string> { ["brightnessMedium"] = "A" }
        };
        var options = new WorkspaceBarOptions(false, 40, Color.Transparent, ModulesLeft: [module]);
        var apply = viewType.GetMethod("ApplyOptions");
        var resolve = viewType.GetMethod("ResolveModuleText", BindingFlags.NonPublic | BindingFlags.Instance);
        var draw = viewType.GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance);
        var wheel = viewType.GetMethod("HandleMouseWheel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(apply);
        Assert.NotNull(resolve);
        Assert.NotNull(draw);
        Assert.NotNull(wheel);
        apply.Invoke(view, [options]);
        viewType.GetField("_isVertical", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, vertical);
        viewType.GetField("_scale", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, scale);
        using var bitmap = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bitmap);
        draw.Invoke(view, [canvas, vertical ? 40F : 300F, vertical ? 300F : 40F]);
        var location = new Point((int)(2 * scale), (int)(2 * scale));
        Assert.Equal(true, wheel.Invoke(view, [location, 120]));
        Assert.Equal(52, brightness.Percent);
        Assert.Equal(false, wheel.Invoke(view, [new Point(500, 500), 120]));
        Assert.Equal(52, brightness.Percent);

        var updated = module with { BrightnessSymbols = new Dictionary<string, string> { ["brightnessMedium"] = "B" } };
        apply.Invoke(view, [options with { ModulesLeft = [updated] }]);
        Assert.Equal("B 52%", resolve.Invoke(view, [updated]));
        apply.Invoke(view, [options with { ModulesLeft = [updated with { Style = updated.Style with { Visible = false } }] }]);
        draw.Invoke(view, [canvas, 300F, 300F]);
        Assert.Equal(false, wheel.Invoke(view, [location, 120]));
    }
}
