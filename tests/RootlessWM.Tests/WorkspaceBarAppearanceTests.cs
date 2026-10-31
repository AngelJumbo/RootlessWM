using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using RootlessWM.App;
using RootlessWM.Domain;
using RootlessWM.Platform.Win32;
using SkiaSharp;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceBarAppearanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentModules_HidesGlobalPixelsAndPreservesModuleStylesAndLayout(bool vertical)
    {
        var viewType = typeof(WorkspaceBarController).GetNestedType("WorkspaceBarView", BindingFlags.NonPublic);
        Assert.NotNull(viewType);
        var log = new ConsoleDiagnosticLog(writeToFile: false);
        using var view = Assert.IsAssignableFrom<IDisposable>(Activator.CreateInstance(viewType,
            [9, new WorkspaceBarWidgetRegistry([new TextWidgetProvider("")]), log]));
        var barStyle = WorkspaceBarStyleOptions.Default with
        {
            Background = Color.Red,
            BorderColor = Color.Blue,
            BorderWidth = 2,
            BorderRadius = 8,
            Padding = new(4),
            Margin = new(3)
        };
        var moduleStyle = WorkspaceBarStyleOptions.Default with
        {
            Background = Color.Green,
            BorderWidth = 1,
            BorderColor = Color.Yellow,
            BorderRadius = 3,
            MinWidth = 40
        };
        var options = new WorkspaceBarOptions(false, 40, Color.Red, barStyle,
            ModulesLeft: [new("item", "text", WorkspaceBarModuleMonitor.All, moduleStyle)],
            Position: vertical ? WorkspaceBarPosition.Left : WorkspaceBarPosition.Top);
        var apply = viewType.GetMethod("ApplyOptions");
        var draw = viewType.GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance);
        var verticalField = viewType.GetField("_isVertical", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(apply);
        Assert.NotNull(draw);
        Assert.NotNull(verticalField);
        verticalField.SetValue(view, vertical);
        using var normal = new SKBitmap(vertical ? 40 : 300, vertical ? 300 : 40);
        using var independent = new SKBitmap(normal.Width, normal.Height);

        Render(options, normal);
        Render(options with { IndependentModules = true }, independent);

        var gapX = vertical ? 20 : 200;
        var gapY = vertical ? 200 : 20;
        Assert.Equal(SKColors.Red, normal.GetPixel(gapX, gapY));
        Assert.Equal((byte)0, independent.GetPixel(gapX, gapY).Alpha);
        var modulePixels = 0;
        for (var y = 0; y < independent.Height; y++)
        {
            for (var x = 0; x < independent.Width; x++)
            {
                var pixel = independent.GetPixel(x, y);
                if (pixel == SKColors.Green || pixel == SKColors.Yellow)
                {
                    modulePixels++;
                    Assert.Equal(pixel, normal.GetPixel(x, y));
                }
            }
        }

        Assert.True(modulePixels > 0);
        var workArea = new WindowBounds(0, 0, 1000, 800);
        Assert.Equal((options with { Visible = true }).Reserve(workArea),
            (options with { Visible = true, IndependentModules = true }).Reserve(workArea));
        Render(options, independent);
        Assert.Equal(SKColors.Red, independent.GetPixel(gapX, gapY));

        void Render(WorkspaceBarOptions configured, SKBitmap bitmap)
        {
            apply.Invoke(view, [configured]);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            draw.Invoke(view, [canvas, (float)bitmap.Width, (float)bitmap.Height]);
            canvas.Flush();
        }
    }

    [Fact]
    public void IndependentModules_DisablesNativeFrameAndRestoresItOnReload()
    {
        var viewType = typeof(WorkspaceBarController).GetNestedType("WorkspaceBarView", BindingFlags.NonPublic);
        Assert.NotNull(viewType);
        using var view = Assert.IsAssignableFrom<IDisposable>(Activator.CreateInstance(viewType,
            [9, new WorkspaceBarWidgetRegistry([]), new ConsoleDiagnosticLog(writeToFile: false)]));
        var apply = viewType.GetMethod("ApplyOptions");
        var field = viewType.GetField("_form", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(apply);
        Assert.NotNull(field);
        var form = Assert.IsAssignableFrom<Form>(field.GetValue(view));
        var options = new WorkspaceBarOptions(false, 40, Color.Transparent,
            WorkspaceBarStyleOptions.Default with { BorderRadius = 8 }, IndependentModules: true);
        apply.Invoke(view, [options with { IndependentModules = false }]);
        _ = form.Handle;
        Assert.Equal(0, NativeMethods.DwmGetWindowAttribute(form.Handle, 1, out var originalRendering, sizeof(uint)));
        apply.Invoke(view, [options]);

        AssertAttribute(1, 0U); // DWMWA_NCRENDERING_ENABLED

        apply.Invoke(view, [options with { IndependentModules = false }]);
        AssertAttribute(1, originalRendering);

        void AssertAttribute(uint attribute, uint expected)
        {
            var result = NativeMethods.DwmGetWindowAttribute(form.Handle, attribute, out var value, sizeof(uint));
            Assert.Equal(0, result);
            Assert.Equal(expected, value);
        }
    }
}
