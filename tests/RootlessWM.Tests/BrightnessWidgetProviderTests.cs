using RootlessWM.App;
using RootlessWM.Platform.Win32;
using Xunit;

namespace RootlessWM.Tests;

public sealed class BrightnessWidgetProviderTests
{
    [Theory]
    [InlineData(0, "L")]
    [InlineData(33, "L")]
    [InlineData(34, "M")]
    [InlineData(66, "M")]
    [InlineData(67, "H")]
    [InlineData(100, "H")]
    public void GetValues_UsesThreeSymbolRanges(int percent, string symbol)
    {
        var provider = Create(new FakeBrightness { Percent = percent });

        var values = provider.GetValues();

        Assert.Equal($"{percent}%", provider.GetText());
        Assert.Equal(symbol, values["brightness_symbol"]);
        Assert.Equal($"{symbol} {percent}%", WorkspaceBarFormat.Format("brightness", "{brightness_symbol} {percent}%", values));
    }

    [Fact]
    public void Scroll_AccumulatesDeltasClampsAndReflectsExternalChanges()
    {
        var brightness = new FakeBrightness { Percent = 50 };
        var provider = Create(brightness);
        provider.Scroll(60);
        Assert.Equal(50, brightness.Percent);
        provider.Scroll(60);
        Assert.Equal(52, brightness.Percent);
        provider.Scroll(-240);
        Assert.Equal(48, brightness.Percent);
        brightness.Percent = 99;
        provider.Scroll(120);
        Assert.Equal(100, brightness.Percent);
        brightness.Percent = 1;
        provider.Scroll(-120);
        Assert.Equal(0, brightness.Percent);
        brightness.Percent = 75;
        Assert.Equal("H", provider.GetValues()["brightness_symbol"]);
    }

    [Fact]
    public void UnavailableDisplay_IsExplicitAndRecovers()
    {
        var brightness = new FakeBrightness { Fail = true };
        var provider = Create(brightness);
        provider.Scroll(120);
        Assert.Equal("N/A", provider.GetText());
        Assert.Equal("", provider.GetValues()["brightness_symbol"]);
        brightness.Fail = false;
        brightness.Percent = 50;
        Assert.Equal("50%", provider.GetText());
        Assert.Equal("", provider.WithSymbols(null).GetValues()["brightness_symbol"]);
    }

    [Theory]
    [InlineData(50, 2, 75)]
    [InlineData(50, -2, 25)]
    [InlineData(100, 2, 100)]
    [InlineData(0, -2, 0)]
    public void AdjustLevel_UsesSupportedStepsInRequestedDirection(int current, int delta, byte expected)
        => Assert.Equal(expected, ScreenBrightnessController.AdjustLevel(current, delta, [0, 25, 50, 75, 100]));

    [Fact]
    public void Registry_ConfiguresSymbolsIndependently()
    {
        var brightness = new FakeBrightness { Percent = 50 };
        var registry = new WorkspaceBarWidgetRegistry([Create(brightness)]);
        var options = new WorkspaceBarWidgetOptions("brightness", "", default, default, default, default)
        {
            BrightnessSymbols = new Dictionary<string, string> { ["brightnessMedium"] = "A" }
        };
        var first = Assert.IsType<BrightnessWidgetProvider>(registry.Create("brightness", options));
        var second = Assert.IsType<BrightnessWidgetProvider>(registry.Create("BRIGHTNESS", options with
        {
            BrightnessSymbols = new Dictionary<string, string> { ["brightnessMedium"] = "B" }
        }));
        Assert.Equal("A", first.GetValues()["brightness_symbol"]);
        Assert.Equal("B", second.GetValues()["brightness_symbol"]);
    }

    private static BrightnessWidgetProvider Create(FakeBrightness brightness)
        => new(brightness, new ConsoleDiagnosticLog(writeToFile: false), new Dictionary<string, string>
        {
            ["brightnessLow"] = "L",
            ["BRIGHTNESSMEDIUM"] = "M",
            ["brightnessHigh"] = "H"
        });

    internal sealed class FakeBrightness : IScreenBrightness
    {
        public int Percent { get; set; }
        public bool Fail { get; set; }
        public int Sample() => Fail ? throw new InvalidOperationException("No integrated display") : Percent;
        public void Adjust(int delta)
        {
            _ = Sample();
            Percent = Math.Clamp(Percent + delta, 0, 100);
        }
    }
}
