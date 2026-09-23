using RootlessWM.App;
using RootlessWM.Domain;
using System.Drawing;
using Tomlyn;
using Xunit;

namespace RootlessWM.Tests;

public sealed class TomlSettingsProviderTests
{
    [Fact]
    public void Load_ValidToml_ReturnsConfiguredValues()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "settings.toml");
        File.WriteAllText(path, """
            OuterGap = 8
            InnerGap = 8

            [Layouts]
            MasterRatio = 0.6
            Default = "MasterTop"
            MasterCount = 3

            [StatusBar]
            Visible = true
            Height = 28
            Background = "#202020"

            modules-left = ["workspaces", "cpu"]

            [StatusBar.Style]
            BorderWidth = 1

            [module.workspaces]
            Type = "workspaces"

            [module.cpu]
            Type = "cpu"
            Symbol = "CPU"

            [Hotkeys]
            PromoteToMaster = "Alt+Shift+M"
            FocusNext = "Alt+Ctrl+J"
            """);

        var settings = new TomlSettingsProvider(path).Load();

        var layoutOptions = settings.ToLayoutOptions();
        Assert.Equal(0.6, layoutOptions.MasterRatio);
        Assert.Equal(8, settings.OuterGap);
        Assert.Equal(8, settings.InnerGap);
        Assert.Equal(3, layoutOptions.MasterCount);
        Assert.Equal("Alt+Shift+M", settings.Hotkeys!["PromoteToMaster"]);
        Assert.Equal("Alt+Ctrl+J", settings.Hotkeys!["FocusNext"]);

        var options = settings.ToWorkspaceBarOptions();
        Assert.True(options.Visible);
        Assert.Equal(28, options.Height);
        Assert.Equal(ColorTranslator.FromHtml("#202020"), options.Background);
        Assert.Equal(1, options.Style!.BorderWidth);
        Assert.Equal(2, options.ModulesLeft!.Count);
        Assert.Equal("workspaces", options.ModulesLeft[0].Type);
        Assert.Equal("cpu", options.ModulesLeft[1].Type);
        Assert.Equal("CPU", options.ModulesLeft[1].Symbol);
        Assert.Empty(options.ModulesCenter!);
        Assert.Empty(options.ModulesRight!);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefault()
    {
        var path = Path.Combine(CreateTempDir(), "does-not-exist.toml");

        var settings = new TomlSettingsProvider(path).Load();

        Assert.Equal(RootlessWMSettings.Default, settings);
    }

    [Fact]
    public void Load_ToggleExplorerBehaviour_MapsConfiguredValue()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "toggle-explorer-behaviour = \"TaskbarAndDesktopIcons\"");

        var settings = new TomlSettingsProvider(path).Load();

        Assert.Equal(ToggleExplorerBehaviour.TaskbarAndDesktopIcons, settings.ToggleExplorerBehaviour);
    }

    [Fact]
    public void Load_HideExplorerOnStart_MapsConfiguredValue()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "hide-explorer-on-start = true");

        var settings = new TomlSettingsProvider(path).Load();

        Assert.True(settings.HideExplorerOnStart);
    }

    [Fact]
    public void Load_FocusFollowsMouse_MapsConfiguredValue()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "focus-follows-mouse = false");

        var settings = new TomlSettingsProvider(path).Load();

        Assert.False(settings.FocusFollowsMouse);
    }

    [Fact]
    public void Load_RunnerConfig_MapsConfiguredValuesAndHotkey()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [runner]
            enabled = true
            max-results = 12
            sources = ["start-menu", "app-paths"]

            [runner.window]
            position = "top-center"
            width = 800
            max-height = 600

            [runner.style]
            background = "#123456"

            [runner.input]
            prompt = "run"

            [Hotkeys]
            OpenRunner = "Alt+Space"
            """);

        var settings = new TomlSettingsProvider(path).Load();

        Assert.True(settings.Runner!.Enabled);
        Assert.Equal(12, settings.Runner.MaxResults);
        Assert.Equal("top-center", settings.Runner.Window.Position);
        Assert.Equal(800, settings.Runner.Window.Width);
        Assert.Equal(600, settings.Runner.Window.MaxHeight);
        Assert.Equal("#123456", settings.Runner.Style.Background);
        Assert.Equal("run", settings.Runner.Input.Prompt);
        Assert.Equal("Alt+Space", settings.Hotkeys!["OpenRunner"]);
    }

    [Fact]
    public void Load_LaunchTableArray_MapsLaunchEntries()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [[launch]]
            hotkey = "Alt+T"
            command = "pwsh.exe"
            args = "-NoLogo"
            working-directory = "C:\\Tools"
            admin = true

            [[launch]]
            hotkey = "Alt+Return"
            command = "wt.exe"
            """);

        var settings = new TomlSettingsProvider(path).Load();

        Assert.NotNull(settings.Launch);
        Assert.Equal(2, settings.Launch!.Count);
        Assert.Equal("Alt+T", settings.Launch[0].Hotkey);
        Assert.Equal("pwsh.exe", settings.Launch[0].Command);
        Assert.Equal("-NoLogo", settings.Launch[0].Args);
        Assert.Equal("C:\\Tools", settings.Launch[0].WorkingDirectory);
        Assert.True(settings.Launch[0].RunAsAdmin);
        Assert.Equal("Alt+Return", settings.Launch[1].Hotkey);
        Assert.Equal("wt.exe", settings.Launch[1].Command);
        Assert.Null(settings.Launch[1].Args);
        Assert.False(settings.Launch[1].RunAsAdmin);
    }

    [Fact]
    public void Load_LaunchTableDictionary_MapsLaunchEntries()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [launch]
            "Alt+T" = "pwsh.exe"
            "Alt+B" = "chrome.exe"
            """);

        var settings = new TomlSettingsProvider(path).Load();

        Assert.NotNull(settings.Launch);
        Assert.Equal(2, settings.Launch!.Count);
        Assert.Contains(settings.Launch, l => l.Hotkey == "Alt+T" && l.Command == "pwsh.exe");
        Assert.Contains(settings.Launch, l => l.Hotkey == "Alt+B" && l.Command == "chrome.exe");
    }

    [Fact]
    public void Load_InvalidToggleExplorerBehaviour_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "toggle-explorer-behaviour = \"EverythingEverywhere\"");

        Assert.Throws<ArgumentOutOfRangeException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidToml_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "MasterRatio = = 0.6");

        Assert.Throws<TomlException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidHeight_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [StatusBar]
            Height = 8
            """);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidLayoutName_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "[Layouts]\nDefault = \"NotARealLayout\"");

        Assert.Throws<ArgumentOutOfRangeException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidWorkspaceColor_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [StatusBar]
            Background = "not-a-color"
            """);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidRunnerColor_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [runner.style]
            background = "not-a-color"
            """);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidHotkeyBinding_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [Hotkeys]
            PromoteToMaster = "justpressm"
            """);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidLaunchHotkey_Throws()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [[launch]]
            hotkey = "NotAHotkey"
            command = "pwsh.exe"
            """);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TomlSettingsProvider(path).Load());
    }

    [Fact]
    public void Load_InvalidToml_MessageIncludesFilePath()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, "MasterRatio = = 0.6");

        var exception = Assert.Throws<TomlException>(() => new TomlSettingsProvider(path).Load());

        Assert.Contains(path, exception.Message);
    }

    [Fact]
    public void Load_ModuleSyntax_MapsListsMonitorAndIndependentDefaults()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [statusbar]
            background = "#101010"
            color = "#D0D0D0"
            modules-left = ["layout"]
            modules-right = ["cpu", "battery"]

            [module.layout]
            type = "layout"
            symbols = { MasterLeft = "ML" }

            [module.cpu]
            type = "cpu"
            monitor = "primary"
            format = "CPU {percent}%"

            [module.battery]
            format = "{battery_symbol} {charging}"
            symbols = { charging = "CHG", batteryEmpty = "E", batteryQuarter = "Q", batteryHalf = "H", batteryThreeQuarters = "TQ", batteryFull = "F" }
            """);

        var options = new TomlSettingsProvider(path).Load().ToWorkspaceBarOptions();

        Assert.Equal("layout", options.ModulesLeft!.Single().Type);
        Assert.Equal("cpu", options.ModulesRight!.First().Type);
        Assert.Equal(WorkspaceBarModuleMonitor.Primary, options.ModulesRight!.First().Monitor);
        Assert.Equal("CPU {percent}%", options.ModulesRight!.First().Format);
        var battery = options.ModulesRight!.Single(module => module.Type == "battery");
        Assert.Equal("{battery_symbol} {charging}", battery.Format);
        Assert.Equal("CHG", battery.BatterySymbols!["charging"]);
        Assert.Equal("F", battery.BatterySymbols["batteryFull"]);
        Assert.Equal(Color.FromArgb(0, 0, 0, 0).ToArgb(), options.ModulesLeft!.Single().Style.Background.ToArgb());
        Assert.Equal(0, options.ModulesLeft!.Single().Style.Padding.All);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RootlessWM-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
