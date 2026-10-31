using System.Runtime.InteropServices;
using RootlessWM.App;
using RootlessWM.Platform.Win32;
using Xunit;

namespace RootlessWM.Tests;

public sealed class VolumeWidgetProviderTests
{
    [Theory]
    [InlineData(0, false, "Z")]
    [InlineData(1, false, "L")]
    [InlineData(33, false, "L")]
    [InlineData(34, false, "M")]
    [InlineData(66, false, "M")]
    [InlineData(67, false, "H")]
    [InlineData(100, false, "H")]
    [InlineData(0, true, "X")]
    [InlineData(33, true, "X")]
    [InlineData(66, true, "X")]
    [InlineData(100, true, "X")]
    public void GetValues_SelectsSymbolForPercentageAndMute(int percent, bool muted, string expected)
    {
        var volume = new FakeVolume { State = new(percent / 100F, muted) };
        var provider = new VolumeWidgetProvider(volume, new ConsoleDiagnosticLog(writeToFile: false),
            new Dictionary<string, string>
            {
                ["volumeZero"] = "Z",
                ["volumeLow"] = "L",
                ["volumeMedium"] = "M",
                ["volumeHigh"] = "H",
                ["MUTED"] = "X"
            });

        var values = provider.GetValues();

        Assert.Equal(expected, values["volume_symbol"]);
        Assert.Equal($"{expected} {percent}%", WorkspaceBarFormat.Format("volume", "{volume_symbol} {percent}%", values));
    }

    [Fact]
    public void GetValues_UsesRoundedPercentageAndUpdatesAfterInteractions()
    {
        var volume = new FakeVolume { State = new(0.334F, false) };
        var provider = CreateProvider(volume).WithSymbols(new Dictionary<string, string>
        {
            ["volumeLow"] = "L",
            ["volumeMedium"] = "M",
            ["muted"] = "X"
        });

        Assert.Equal("L", provider.GetValues()["volume_symbol"]);
        volume.State = new(0.336F, false);
        Assert.Equal("34", provider.GetValues()["percent"]);
        Assert.Equal("M", provider.GetValues()["volume_symbol"]);
        provider.Scroll(-120);
        Assert.Equal("L", provider.GetValues()["volume_symbol"]);
        provider.ToggleMute();
        Assert.Equal("X", provider.GetValues()["volume_symbol"]);
        provider.ToggleMute();
        Assert.Equal("L", provider.GetValues()["volume_symbol"]);
    }

    [Fact]
    public void GetValues_MissingSymbolsAndUnavailableDeviceReturnEmptySymbol()
    {
        var volume = new FakeVolume { State = new(0.5F, false) };
        var provider = CreateProvider(volume);
        Assert.Equal("", provider.GetValues()["volume_symbol"]);
        provider = provider.WithSymbols(new Dictionary<string, string> { ["volumeHigh"] = "H" });
        Assert.Equal("", provider.GetValues()["volume_symbol"]);
        volume.State = new(1F, true);
        Assert.Equal("", provider.GetValues()["volume_symbol"]);
        volume.Fail = true;
        Assert.Equal("", provider.GetValues()["volume_symbol"]);
    }

    [Theory]
    [InlineData(0F, false, "0%", "0", "")]
    [InlineData(0.42F, false, "42%", "42", "")]
    [InlineData(1F, true, "100% (muted)", "100", "muted")]
    public void GetValues_ReportsVolumeAndMute(float scalar, bool muted, string output, string percent, string status)
    {
        var provider = CreateProvider(new FakeVolume { State = new(scalar, muted) });

        var values = provider.GetValues();

        Assert.Equal(output, values["output"]);
        Assert.Equal(percent, values["percent"]);
        Assert.Equal(muted ? "true" : "false", values["muted"]);
        Assert.Equal(status, values["status"]);
        Assert.Equal(output, provider.GetText());
    }

    [Fact]
    public void GetValues_ReflectsExternalChanges()
    {
        var volume = new FakeVolume { State = new(0.2F, false) };
        var provider = CreateProvider(volume);
        Assert.Equal("20%", provider.GetText());

        volume.State = new(0.6F, true);

        Assert.Equal("60% (muted)", provider.GetText());
    }

    [Theory]
    [InlineData(120, 0.52F)]
    [InlineData(-120, 0.48F)]
    [InlineData(240, 0.54F)]
    [InlineData(-360, 0.44F)]
    public void Scroll_AdjustsTwoPercentPerNotchAndPreservesMute(int delta, float expected)
    {
        var volume = new FakeVolume { State = new(0.5F, true) };
        var provider = CreateProvider(volume);

        provider.Scroll(delta);

        Assert.Equal(expected, volume.State.Scalar, 5);
        Assert.True(volume.State.Muted);
        Assert.Equal(1, volume.Adjustments);
    }

    [Theory]
    [InlineData(0.99F, 0.02F, 1F)]
    [InlineData(0.01F, -0.02F, 0F)]
    [InlineData(0.5F, 4F, 1F)]
    [InlineData(0.5F, -4F, 0F)]
    public void AdjustScalar_ClampsToValidVolume(float scalar, float delta, float expected)
        => Assert.Equal(expected, SystemVolumeController.AdjustScalar(scalar, delta));

    [Fact]
    public void Scroll_AccumulatesPartialWheelDeltas()
    {
        var volume = new FakeVolume { State = new(0.5F, false) };
        var provider = CreateProvider(volume);

        provider.Scroll(60);
        Assert.Equal(0, volume.Adjustments);
        provider.Scroll(90);
        Assert.Equal(0.52F, volume.State.Scalar, 5);
        provider.Scroll(90);
        Assert.Equal(0.54F, volume.State.Scalar, 5);
        Assert.Equal(2, volume.Adjustments);
    }

    [Fact]
    public void AudioFailure_IsExplicitAndRecoversOnNextSample()
    {
        var volume = new FakeVolume { Fail = true };
        var provider = CreateProvider(volume);

        provider.Scroll(120);
        var values = provider.GetValues();

        Assert.Equal("N/A", values["output"]);
        Assert.Equal("N/A", values["percent"]);
        Assert.Equal("unknown", values["muted"]);
        Assert.Equal("unavailable", values["status"]);
        volume.Fail = false;
        volume.State = new(0.3F, false);
        Assert.Equal("30%", provider.GetText());
    }

    [Fact]
    public void ToggleMute_TogglesCurrentStateAndPreservesVolume()
    {
        var volume = new FakeVolume { State = new(0.42F, false) };
        var provider = CreateProvider(volume);

        provider.ToggleMute();

        Assert.True(volume.State.Muted);
        Assert.Equal("42% (muted)", provider.GetText());
        provider.ToggleMute();
        Assert.False(volume.State.Muted);
        Assert.Equal("42%", provider.GetText());
        Assert.Equal(0.42F, volume.State.Scalar);

        volume.State = new(0.7F, true);
        provider.ToggleMute();
        Assert.False(volume.State.Muted);
        Assert.Equal(0.7F, volume.State.Scalar);
    }

    [Fact]
    public void ToggleMute_AudioFailurePreservesStateAndRecovers()
    {
        var volume = new FakeVolume { State = new(0.42F, false), Fail = true };
        var provider = CreateProvider(volume);

        provider.ToggleMute();

        Assert.False(volume.State.Muted);
        Assert.Equal("N/A", provider.GetText());
        volume.Fail = false;
        provider.ToggleMute();
        Assert.Equal("42% (muted)", provider.GetText());
    }

    [Fact]
    public void SystemVolumeController_SamplesActualEndpointWithoutChangingVolume()
    {
        try
        {
            var state = new SystemVolumeController().Sample();
            Assert.InRange(state.Scalar, 0F, 1F);
        }
        catch (COMException exception) when (exception.HResult == unchecked((int)0x80070490))
        {
            // Windows reports ERROR_NOT_FOUND when there is no default playback device.
        }
    }

    [Fact]
    public void Settings_CustomModuleIdUsesVolumeDefaultFormat()
    {
        var settings = new RootlessWMSettings(0, 0, WorkspaceBar: new WorkspaceBarSettings(
            ModulesRight: ["sound"],
            Modules: new Dictionary<string, WorkspaceBarModuleSettings>
            {
                ["sound"] = new(Type: "volume")
            }));

        var module = Assert.Single(settings.ToWorkspaceBarOptions().ModulesRight!);

        Assert.Equal("volume", module.Type);
        Assert.Equal("{output}", module.Format);
        Assert.Equal("VOL 50% muted", WorkspaceBarFormat.Format("volume", "VOL {percent}% {status}",
            CreateProvider(new FakeVolume { State = new(0.5F, true) }).GetValues()));
    }

    private static VolumeWidgetProvider CreateProvider(FakeVolume volume)
        => new(volume, new ConsoleDiagnosticLog(writeToFile: false));

    internal sealed class FakeVolume : ISystemVolume
    {
        public SystemVolumeState State { get; set; }
        public bool Fail { get; set; }
        public int Adjustments { get; private set; }

        public SystemVolumeState Sample()
            => Fail ? throw new COMException("No audio device", unchecked((int)0x80070490)) : State;

        public void Adjust(float delta)
        {
            _ = Sample();
            Adjustments++;
            State = State with { Scalar = SystemVolumeController.AdjustScalar(State.Scalar, delta) };
        }

        public void ToggleMute()
        {
            _ = Sample();
            State = State with { Muted = !State.Muted };
        }
    }
}
