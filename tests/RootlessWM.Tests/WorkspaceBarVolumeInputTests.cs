using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using RootlessWM.App;
using RootlessWM.Platform.Win32;
using SkiaSharp;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceBarVolumeInputTests
{
    [Fact]
    public void VolumeSymbols_ArePassedToProviderAndReloadWhenOnlyGlyphChanges()
    {
        var volume = new VolumeWidgetProviderTests.FakeVolume { State = new(0.5F, false) };
        var log = new ConsoleDiagnosticLog(writeToFile: false);
        var registry = new WorkspaceBarWidgetRegistry([new VolumeWidgetProvider(volume, log)]);
        var viewType = typeof(WorkspaceBarController).GetNestedType("WorkspaceBarView", BindingFlags.NonPublic);
        Assert.NotNull(viewType);
        using var view = Assert.IsAssignableFrom<IDisposable>(Activator.CreateInstance(viewType, [9, registry, log]));
        var module = new WorkspaceBarModuleOptions("sound", "volume", WorkspaceBarModuleMonitor.All,
            WorkspaceBarStyleOptions.Default, Format: "{volume_symbol} {percent}%")
        {
            VolumeSymbols = new Dictionary<string, string> { ["volumeMedium"] = "A" }
        };
        var options = new WorkspaceBarOptions(false, 40, Color.Transparent, ModulesRight: [module]);
        var apply = viewType.GetMethod("ApplyOptions");
        var resolve = viewType.GetMethod("ResolveModuleText", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(apply);
        Assert.NotNull(resolve);
        apply.Invoke(view, [options]);
        Assert.Equal("A 50%", resolve.Invoke(view, [module]));

        var updated = module with
        {
            VolumeSymbols = new Dictionary<string, string> { ["volumeMedium"] = "B" }
        };
        apply.Invoke(view, [options with { ModulesRight = [updated] }]);

        Assert.Equal("B 50%", resolve.Invoke(view, [updated]));
    }

    [Theory]
    [InlineData(false, 1F)]
    [InlineData(false, 1.5F)]
    [InlineData(true, 1F)]
    [InlineData(true, 2F)]
    public void Wheel_OnlyAdjustsHoveredVolumeModuleWithDpiScaling(bool vertical, float scale)
    {
        var volume = new VolumeWidgetProviderTests.FakeVolume { State = new(0.5F, false) };
        var log = new ConsoleDiagnosticLog(writeToFile: false);
        var registry = new WorkspaceBarWidgetRegistry([new VolumeWidgetProvider(volume, log), new TextWidgetProvider("Other")]);
        var viewType = typeof(WorkspaceBarController).GetNestedType("WorkspaceBarView", BindingFlags.NonPublic);
        Assert.NotNull(viewType);
        using var view = Assert.IsAssignableFrom<IDisposable>(Activator.CreateInstance(viewType, [9, registry, log]));
        var style = WorkspaceBarStyleOptions.Default with { Padding = new(4, 4, 4, 4), MinWidth = 60 };
        var options = new WorkspaceBarOptions(false, 40, Color.Transparent,
            ModulesLeft:
            [
                new("sound", "volume", WorkspaceBarModuleMonitor.All, style),
                new("other", "text", WorkspaceBarModuleMonitor.All, style, Text: "Other")
            ]);
        var apply = viewType.GetMethod("ApplyOptions");
        Assert.NotNull(apply);
        apply.Invoke(view, [options]);
        var verticalField = viewType.GetField("_isVertical", BindingFlags.NonPublic | BindingFlags.Instance);
        var scaleField = viewType.GetField("_scale", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(verticalField);
        Assert.NotNull(scaleField);
        verticalField.SetValue(view, vertical);
        scaleField.SetValue(view, scale);
        using var bitmap = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bitmap);
        var draw = viewType.GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance);
        var wheel = viewType.GetMethod("HandleMouseWheel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(draw);
        Assert.NotNull(wheel);
        draw.Invoke(view, [canvas, vertical ? 40F : 300F, vertical ? 300F : 40F]);

        // The module's padding is interactive, not just its glyphs.
        var inside = new Point((int)(2 * scale), (int)(2 * scale));
        Assert.Equal(true, wheel.Invoke(view, [inside, 120]));
        Assert.Equal(0.52F, volume.State.Scalar, 5);
        var other = vertical ? new Point(10, 80) : new Point(80, 10);
        other = new Point((int)(other.X * scale), (int)(other.Y * scale));
        Assert.Equal(false, wheel.Invoke(view, [other, -120]));
        Assert.Equal(false, wheel.Invoke(view, [new Point(500, 500), 120]));
        Assert.Equal(1, volume.Adjustments);

        var formField = viewType.GetField("_form", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(formField);
        var form = Assert.IsAssignableFrom<Form>(formField.GetValue(view));
        form.Location = new Point(-300, -200);
        var screenPoint = form.PointToScreen(inside);
        var packedPoint = (uint)(ushort)screenPoint.X | ((uint)(ushort)screenPoint.Y << 16);
        var message = Message.Create(form.Handle, 0x020A, (nint)(-120 << 16), unchecked((nint)(int)packedPoint));
        var windowProcedure = form.GetType().GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(windowProcedure);
        var foreground = NativeMethods.GetForegroundWindow();

        windowProcedure.Invoke(form, [message]);

        Assert.Equal(0.5F, volume.State.Scalar, 5);
        Assert.Equal(2, volume.Adjustments);
        Assert.Equal(foreground, NativeMethods.GetForegroundWindow());

        var mouseDown = viewType.GetMethod("HandleMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(mouseDown);
        mouseDown.Invoke(view, [form, new MouseEventArgs(MouseButtons.Right, 1, inside.X, inside.Y, 0)]);
        Assert.False(volume.State.Muted);
        mouseDown.Invoke(view, [form, new MouseEventArgs(MouseButtons.Left, 1, other.X, other.Y, 0)]);
        Assert.False(volume.State.Muted);
        mouseDown.Invoke(view, [form, new MouseEventArgs(MouseButtons.Left, 1, inside.X, inside.Y, 0)]);
        Assert.True(volume.State.Muted);
        Assert.Equal(0.5F, volume.State.Scalar, 5);
        mouseDown.Invoke(view, [form, new MouseEventArgs(MouseButtons.Left, 1, inside.X, inside.Y, 0)]);
        Assert.False(volume.State.Muted);
        Assert.Equal(foreground, NativeMethods.GetForegroundWindow());

        apply.Invoke(view, [options with
        {
            ModulesLeft = [new("sound", "volume", WorkspaceBarModuleMonitor.All, style with { Visible = false })]
        }]);
        draw.Invoke(view, [canvas, 300F, 300F]);
        Assert.Equal(false, wheel.Invoke(view, [inside, 120]));
        mouseDown.Invoke(view, [form, new MouseEventArgs(MouseButtons.Left, 1, inside.X, inside.Y, 0)]);
        Assert.False(volume.State.Muted);
        Assert.Equal(2, volume.Adjustments);
    }
}
