using RootlessWM.App;
using RootlessWM.Domain;
using RootlessWM.Platform.Win32;
using System.Drawing;
using System.Reflection;
using Xunit;

namespace RootlessWM.Tests;

public sealed class RootlessWMSettingsTests
{
    [Fact]
    public void ToLayoutOptions_ValidSettings_ReturnsConfiguredValues()
    {
        var settings = new RootlessWMSettings(0.6, 8, 8);

        var options = settings.ToLayoutOptions();

        Assert.Equal(0.6, options.MasterRatio);
        Assert.Equal(8, options.OuterGap);
        Assert.Equal(8, options.InnerGap);
        Assert.Equal(1, options.MasterCount);
    }

    [Fact]
    public void ToLayoutOptions_ConfiguredMasterCount_ReturnsConfiguredValue()
    {
        var options = new RootlessWMSettings(0.6, 8, 8, MasterCount: 3).ToLayoutOptions();

        Assert.Equal(3, options.MasterCount);
    }

    [Fact]
    public void ToLayoutOptions_InvalidMasterCount_Throws()
    {
        var settings = new RootlessWMSettings(0.6, 8, 8, MasterCount: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToLayoutOptions());
    }

    [Fact]
    public void ToLayoutOptions_InvalidSettings_Throws()
    {
        var settings = new RootlessWMSettings(1, -1, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToLayoutOptions());
    }

    [Fact]
    public void ToLayoutOptions_InvalidInnerGap_Throws()
    {
        var settings = new RootlessWMSettings(0.6, 0, -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToLayoutOptions());
    }

    [Fact]
    public void Constructor_Hotkeys_PreservesConfiguredOverrides()
    {
        IReadOnlyDictionary<string, string> hotkeys = new Dictionary<string, string>
        {
            ["PromoteToMaster"] = "Alt+Shift+M",
            ["FocusNext"] = "Alt+Ctrl+J"
        };

        var settings = new RootlessWMSettings(0.55, 0, 0, hotkeys);

        Assert.Equal("Alt+Shift+M", settings.Hotkeys!["PromoteToMaster"]);
        Assert.Equal("Alt+Ctrl+J", settings.Hotkeys!["FocusNext"]);
    }

    [Fact]
    public void Constructor_Runner_PreservesConfiguredValues()
    {
        var settings = new RootlessWMSettings(
            0.55,
            0,
            0,
            Runner: new RunnerSettings(
                Enabled: true,
                MaxResults: 12,
                Window: new RunnerWindowSettings(Position: "top-center", Width: 800, MaxHeight: 600),
                Style: new RunnerStyleSettings(Background: "#123456"),
                Input: new RunnerInputSettings(Prompt: "run")));

        Assert.True(settings.Runner!.Enabled);
        Assert.Equal(12, settings.Runner.MaxResults);
        Assert.Equal("top-center", settings.Runner.Window.Position);
        Assert.Equal(800, settings.Runner.Window.Width);
        Assert.Equal("#123456", settings.Runner.Style.Background);
        Assert.Equal("run", settings.Runner.Input.Prompt);
    }

    [Fact]
    public void Constructor_Launch_PreservesConfiguredValues()
    {
        var launchEntries = new List<LaunchHotkeySettings>
        {
            new("Alt+T", "pwsh.exe", "-NoLogo", "C:\\Tools")
        };

        var settings = new RootlessWMSettings(
            0.55,
            0,
            0,
            Launch: launchEntries);

        Assert.NotNull(settings.Launch);
        Assert.Single(settings.Launch);
        Assert.Equal("Alt+T", settings.Launch[0].Hotkey);
        Assert.Equal("pwsh.exe", settings.Launch[0].Command);
        Assert.Equal("-NoLogo", settings.Launch[0].Args);
        Assert.Equal("C:\\Tools", settings.Launch[0].WorkingDirectory);
    }

    [Fact]
    public void GlobalHotkeySource_TryGetLaunch_ReturnsConfiguredLaunch()
    {
        using var source = new GlobalHotkeySource();
        var launchEntries = new List<LaunchHotkeySettings>
        {
            new("Alt+T", "pwsh.exe", "-NoLogo")
        };

        _ = source.Start(null, launchEntries);

        Assert.True(source.TryGetLaunch(NativeMethods.WmHotkey, 1000, out var launch));
        Assert.NotNull(launch);
        Assert.Equal("Alt+T", launch.Hotkey);
        Assert.Equal("pwsh.exe", launch.Command);
        Assert.Equal("-NoLogo", launch.Args);

        Assert.False(source.TryGetLaunch(NativeMethods.WmHotkey, 9999, out _));
        Assert.False(source.TryGetLaunch(0x0000, 1000, out _));
    }

    [Theory]
    [InlineData("Super+Enter")]
    [InlineData("Win+Enter")]
    [InlineData("Alt+Super+Enter")]
    public void TryParseBinding_SupportsSuperAndEnter(string binding)
    {
        var method = typeof(GlobalHotkeySource).GetMethod("TryParseBinding", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var parameters = new object?[] { binding, 0u, 0u };
        var result = (bool)method!.Invoke(null, parameters)!;

        Assert.True(result);
        Assert.NotEqual((uint)0, (uint)parameters[1]!);
        Assert.Equal((uint)NativeMethods.VkEnter, (uint)parameters[2]!);
    }

    [Fact]
    public void Default_ToggleExplorerBehaviour_HidesTaskbarOnly()
    {
        Assert.Equal(ToggleExplorerBehaviour.TaskbarOnly, RootlessWMSettings.Default.ToggleExplorerBehaviour);
    }

    [Fact]
    public void Constructor_ToggleExplorerBehaviour_PreservesConfiguredValue()
    {
        var settings = new RootlessWMSettings(
            0.55,
            0,
            0,
            ToggleExplorerBehaviour: ToggleExplorerBehaviour.TaskbarAndDesktopIcons);

        Assert.Equal(ToggleExplorerBehaviour.TaskbarAndDesktopIcons, settings.ToggleExplorerBehaviour);
    }

    [Fact]
    public void Default_HideExplorerOnStart_IsFalse()
    {
        Assert.False(RootlessWMSettings.Default.HideExplorerOnStart);
    }

    [Fact]
    public void Default_FocusFollowsMouse_IsTrue()
    {
        Assert.True(RootlessWMSettings.Default.FocusFollowsMouse);
    }

    [Fact]
    public void Constructor_HideExplorerOnStart_PreservesConfiguredValue()
    {
        var settings = new RootlessWMSettings(0.55, 0, 0, HideExplorerOnStart: true);

        Assert.True(settings.HideExplorerOnStart);
    }

    [Fact]
    public void ToLayoutOptions_MasterTop_ParsesConfiguredMode()
    {
        var options = new RootlessWMSettings(0.5, 8, 8, Layout: "MasterTop").ToLayoutOptions();

        Assert.Equal(RootlessWM.Domain.MasterStackLayoutMode.MasterTop, options.Mode);
    }

    [Fact]
    public void CycleMode_AdvancesThroughMasterLeftMasterTopMonocleAndFloating()
    {
        var options = new MasterStackLayoutOptions(0.5, 8, 8);

        Assert.Equal(MasterStackLayoutMode.MasterTop, options.CycleMode().Mode);
        Assert.Equal(MasterStackLayoutMode.Monocle, options.CycleMode().CycleMode().Mode);
        Assert.Equal(MasterStackLayoutMode.Floating, options.CycleMode().CycleMode().CycleMode().Mode);
        Assert.Equal(MasterStackLayoutMode.MasterLeft, options.CycleMode().CycleMode().CycleMode().CycleMode().Mode);
    }

    [Fact]
    public void ToWorkspaceBarOptions_ValidSettings_ReturnsConfiguredValues()
    {
        var widgetSettings = new Dictionary<string, WorkspaceBarWidgetSettings>
        {
            ["cpu"] = new WorkspaceBarWidgetSettings(Symbol: "CPU", SymbolForeground: "#FF0000"),
            ["datetime"] = new WorkspaceBarWidgetSettings(Symbol: "TIME")
        };
        var settings = new RootlessWMSettings(
            0.55,
            0,
            0,
            WorkspaceBar: new WorkspaceBarSettings(
                Visible: true,
                Height: 28,
                Background: "#202020",
                Workspaces: new WorkspaceBarWorkspaceSettings(
                    Background: "#111111",
                    Foreground: "#BBBBBB",
                    CurrentBackground: "#FFFFFF",
                    CurrentForeground: "#000000",
                    Symbols: ["A", "B", "C"]),
                Layout: new WorkspaceBarLayoutSettings(
                    Background: "#222222",
                    Foreground: "#CCCCCC",
                    Symbols: new Dictionary<string, string> { ["MasterLeft"] = "L" }),
                Title: new WorkspaceBarTitleSettings(
                    Background: "#333333",
                    CurrentBackground: "#444444",
                    CurrentForeground: "#EEEEEE"),
                Widgets: new WorkspaceBarWidgetsSettings(
                    Order: ["datetime", "cpu"],
                    Widgets: widgetSettings)));

        var options = settings.ToWorkspaceBarOptions();

        Assert.True(options.Visible);
        Assert.Equal(28, options.Height);
        Assert.Equal(ColorTranslator.FromHtml("#202020"), options.Background);
        Assert.Equal(ColorTranslator.FromHtml("#111111"), options.Workspaces.Background);
        Assert.Equal(ColorTranslator.FromHtml("#BBBBBB"), options.Workspaces.Foreground);
        Assert.Equal(ColorTranslator.FromHtml("#FFFFFF"), options.Workspaces.CurrentBackground);
        Assert.Equal(ColorTranslator.FromHtml("#000000"), options.Workspaces.CurrentForeground);
        Assert.Equal(["A", "B", "C"], options.Workspaces.Symbols);
        Assert.Equal(ColorTranslator.FromHtml("#222222"), options.Layout.Background);
        Assert.Equal("L", options.Layout.Symbols[MasterStackLayoutMode.MasterLeft]);
        Assert.Equal(ColorTranslator.FromHtml("#444444"), options.Title.CurrentBackground);
        Assert.Equal(ColorTranslator.FromHtml("#EEEEEE"), options.Title.CurrentForeground);
        Assert.Equal(2, options.Widgets.Count);
        Assert.Equal("datetime", options.Widgets[0].Kind);
        Assert.Equal("TIME", options.Widgets[0].Symbol);
        Assert.Equal("cpu", options.Widgets[1].Kind);
        Assert.Equal("CPU", options.Widgets[1].Symbol);
        Assert.Equal(ColorTranslator.FromHtml("#FF0000"), options.Widgets[1].SymbolForeground);
    }

    [Fact]
    public void ToWorkspaceBarOptions_DisabledWidget_IsExcluded()
    {
        var widgetSettings = new Dictionary<string, WorkspaceBarWidgetSettings>
        {
            ["memory"] = new WorkspaceBarWidgetSettings(Enabled: false)
        };
        var settings = new RootlessWMSettings(
            0.55,
            0,
            0,
            WorkspaceBar: new WorkspaceBarSettings(
                Widgets: new WorkspaceBarWidgetsSettings(
                    Order: ["cpu", "memory", "datetime"],
                    Widgets: widgetSettings)));

        var options = settings.ToWorkspaceBarOptions();

        Assert.Equal(2, options.Widgets.Count);
        Assert.DoesNotContain(options.Widgets, widget => widget.Kind == "memory");
    }

    [Fact]
    public void ToWorkspaceBarOptions_InvalidHeight_Throws()
    {
        var settings = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Height: 8));

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToWorkspaceBarOptions());
    }

    [Fact]
    public void ToWorkspaceBarOptions_InvalidColor_Throws()
    {
        var settings = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Background: "not-a-color"));

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToWorkspaceBarOptions());
    }

    [Fact]
    public void ToWorkspaceBarOptions_RgbaColor_PreservesAlpha()
    {
        var settings = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Background: "#10203080"));

        var options = settings.ToWorkspaceBarOptions();

        Assert.Equal(Color.FromArgb(0x80, 0x10, 0x20, 0x30), options.Background);
    }

    [Fact]
    public void Reserve_VisibleBar_OffsetsAndShrinksWorkArea()
    {
        var options = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Visible: true, Height: 24))
            .ToWorkspaceBarOptions();

        var reserved = options.Reserve(new WindowBounds(100, 200, 800, 600));

        Assert.Equal(new WindowBounds(100, 224, 800, 576), reserved);
    }

    [Fact]
    public void Reserve_HiddenBar_DoesNotChangeWorkArea()
    {
        var options = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Visible: false))
            .ToWorkspaceBarOptions();
        var workArea = new WindowBounds(100, 200, 800, 600);

        var reserved = options.Reserve(workArea);

        Assert.Equal(workArea, reserved);
    }

    [Fact]
    public void Reserve_WithBarMargin_ReservesTransparentSeparation()
    {
        var options = new RootlessWMSettings(
            0.55,
            0,
            0,
            WorkspaceBar: new WorkspaceBarSettings(
                Height: 28,
                Style: new WorkspaceBarStyleSettings(MarginTop: 2, MarginBottom: 2, MarginLeft: 8, MarginRight: 8)))
            .ToWorkspaceBarOptions();

        var reserved = options.Reserve(new WindowBounds(100, 200, 800, 600));

        Assert.Equal(new WindowBounds(100, 232, 800, 568), reserved);
    }

    [Theory]
    [InlineData("left")]
    [InlineData("Left")]
    public void Reserve_LeftPosition_OffsetsAndShrinksWorkArea(string position)
    {
        var options = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Visible: true, Height: 24, Position: position))
            .ToWorkspaceBarOptions();

        var reserved = options.Reserve(new WindowBounds(100, 200, 800, 600));

        Assert.Equal(new WindowBounds(124, 200, 776, 600), reserved);
        Assert.True(options.IsVertical);
    }

    [Fact]
    public void Reserve_RightPosition_OffsetsAndShrinksWorkArea()
    {
        var options = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Visible: true, Height: 24, Position: "right"))
            .ToWorkspaceBarOptions();

        var reserved = options.Reserve(new WindowBounds(100, 200, 800, 600));

        Assert.Equal(new WindowBounds(100, 200, 776, 600), reserved);
    }

    [Fact]
    public void Reserve_BottomPosition_OffsetsAndShrinksWorkArea()
    {
        var options = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Visible: true, Height: 24, Position: "bottom"))
            .ToWorkspaceBarOptions();

        var reserved = options.Reserve(new WindowBounds(100, 200, 800, 600));

        Assert.Equal(new WindowBounds(100, 200, 800, 576), reserved);
        Assert.False(options.IsVertical);
    }

    [Fact]
    public void ToWorkspaceBarOptions_InvalidPosition_Throws()
    {
        var settings = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Position: "diagonal"));

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToWorkspaceBarOptions());
    }

    [Fact]
    public void ToWorkspaceBarOptions_ThicknessAlias_OverridesHeight()
    {
        var settings = new RootlessWMSettings(0.55, 0, 0, WorkspaceBar: new WorkspaceBarSettings(Height: 24, Thickness: 40));

        var options = settings.ToWorkspaceBarOptions();

        Assert.Equal(40, options.Height);
    }

    [Fact]
    public void WorkspaceBar_DefaultsToVisible()
    {
        var options = RootlessWMSettings.Default.ToWorkspaceBarOptions();

        Assert.True(options.Visible);
    }

    [Fact]
    public void ToWorkspaceBarOptions_MapsSectionsAndCommandWidget()
    {
        var settings = new RootlessWMSettings(
            0.55,
            0,
            0,
            WorkspaceBar: new WorkspaceBarSettings(
                Style: new WorkspaceBarStyleSettings(PaddingLeft: 8, FontSize: 11),
                Sections: [new WorkspaceBarSectionSettings("widgets", "right")],
                Widgets: new WorkspaceBarWidgetsSettings(
                    Order: ["vpn"],
                    Widgets: new Dictionary<string, WorkspaceBarWidgetSettings>
                    {
                        ["vpn"] = new(Kind: "command", Command: "ver", IntervalMilliseconds: 100)
                    })));

        var options = settings.ToWorkspaceBarOptions();

        Assert.Equal(8, options.Style!.Padding.Left);
        Assert.Equal(11, options.Style.FontSize);
        Assert.Single(options.Sections!);
        Assert.Equal(WorkspaceBarSectionAlignment.Right, options.Sections!.Single().Alignment);
        Assert.Equal("command", options.Widgets[0].Kind);
        Assert.Equal(250, options.Widgets[0].IntervalMilliseconds);
    }
}
