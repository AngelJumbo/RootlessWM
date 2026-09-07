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
            MasterRatio = 0.6
            OuterGap = 8
            InnerGap = 8
            Layout = "MasterTop"
            MasterCount = 3

            [StatusBar]
            Visible = true
            Height = 28
            Background = "#202020"

            [StatusBar.Workspaces]
            Background = "#111111"
            Foreground = "#BBBBBB"
            CurrentBackground = "#FFFFFF"
            CurrentForeground = "#000000"
            Symbols = ["A", "B", "C"]

            [StatusBar.Layout]
            Background = "#222222"
            Foreground = "#CCCCCC"

            [StatusBar.Layout.Symbols]
            MasterLeft = "L"

            [StatusBar.Title]
            Background = "#333333"
            CurrentBackground = "#444444"
            CurrentForeground = "#EEEEEE"

            [StatusBar.Widgets]
            Order = ["clock", "cpu"]

            [StatusBar.Widgets.Cpu]
            Symbol = "CPU"
            SymbolForeground = "#FF0000"

            [StatusBar.Widgets.Clock]
            Symbol = "TIME"

            [Hotkeys]
            PromoteToMaster = "Alt+Shift+M"
            FocusNext = "Alt+Ctrl+J"
            """);

        var settings = new TomlSettingsProvider(path).Load();

        Assert.Equal(0.6, settings.MasterRatio);
        Assert.Equal(8, settings.OuterGap);
        Assert.Equal(8, settings.InnerGap);
        Assert.Equal(3, settings.MasterCount);
        Assert.Equal("Alt+Shift+M", settings.Hotkeys!["PromoteToMaster"]);
        Assert.Equal("Alt+Ctrl+J", settings.Hotkeys!["FocusNext"]);

        var options = settings.ToWorkspaceBarOptions();
        Assert.True(options.Visible);
        Assert.Equal(28, options.Height);
        Assert.Equal(ColorTranslator.FromHtml("#202020"), options.Background);
        Assert.Equal(ColorTranslator.FromHtml("#111111"), options.Workspaces.Background);
        Assert.Equal(["A", "B", "C"], options.Workspaces.Symbols);
        Assert.Equal("L", options.Layout.Symbols[MasterStackLayoutMode.MasterLeft]);
        Assert.Equal(ColorTranslator.FromHtml("#444444"), options.Title.CurrentBackground);
        Assert.Equal(2, options.Widgets.Count);
        Assert.Equal("clock", options.Widgets[0].Kind);
        Assert.Equal("TIME", options.Widgets[0].Symbol);
        Assert.Equal("cpu", options.Widgets[1].Kind);
        Assert.Equal(ColorTranslator.FromHtml("#FF0000"), options.Widgets[1].SymbolForeground);
    }

    [Fact]
    public void Load_ArbitraryWidgetSubTable_IsMapped()
    {
        var dir = CreateTempDir();
        var path = Path.Combine(dir, "settings.toml");
        File.WriteAllText(path, """
            [StatusBar.Widgets]
            Order = ["network", "text"]

            [StatusBar.Widgets.Network]
            Symbol = "NET"

            [StatusBar.Widgets.Text]
            Text = "Hello"
            """);

        var settings = new TomlSettingsProvider(path).Load();
        var options = settings.ToWorkspaceBarOptions();

        Assert.Equal(2, options.Widgets.Count);
        Assert.Equal("network", options.Widgets[0].Kind);
        Assert.Equal("NET", options.Widgets[0].Symbol);
        Assert.Equal("text", options.Widgets[1].Kind);
        Assert.Equal("Hello", options.Widgets[1].Text);
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
    public void Load_MissingToml_FallsBackToJson()
    {
        var dir = CreateTempDir();
        var jsonPath = Path.Combine(dir, "settings.json");
        File.WriteAllText(jsonPath, """
            {
              "MasterRatio": 0.7,
              "OuterGap": 4,
              "InnerGap": 4
            }
            """);

        var settings = new TomlSettingsProvider(Path.Combine(dir, "settings.toml")).Load();

        Assert.Equal(0.7, settings.MasterRatio);
        Assert.Equal(4, settings.OuterGap);
        Assert.Equal(4, settings.InnerGap);
    }

    [Fact]
    public void Load_SectionsNestedStyleAndCommandWidget()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [StatusBar.Style]
            Color = "#EEEEEE"
            [StatusBar.Style.Padding]
            Left = 8

            [[StatusBar.Sections]]
            Id = "widgets"
            Align = "right"

            [StatusBar.Widgets]
            Order = ["vpn"]
            [StatusBar.Widgets.Vpn]
            Kind = "command"
            Command = "ver"
            IntervalMilliseconds = 100
            """);

        var options = new TomlSettingsProvider(path).Load().ToWorkspaceBarOptions();

        Assert.Equal(8, options.Style!.Padding.Left);
        Assert.Equal(WorkspaceBarSectionAlignment.Right, options.Sections![0].Alignment);
        Assert.Equal("command", options.Widgets[0].Kind);
        Assert.Equal(250, options.Widgets[0].IntervalMilliseconds);
    }

    [Fact]
    public void Load_RequestedDeclarativeSyntax_MapsDirectStylesAndWidgetArray()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [statusbar]
            height = 28
            background = "#1e1e2e"
            border = { width = 1, color = "#45475a" }
            radius = 6
            padding = { top = 2, right = 8, bottom = 2, left = 8 }
            spacing = 6
            font = { family = "Cascadia Mono", size = 12, weight = "bold" }

            [[statusbar.sections]]
            id = "widgets"
            align = "right"
            background = "#313244"
            padding = { left = 4, right = 4 }
            widgets = ["cpu"]

            [[statusbar.widgets]]
            id = "cpu"
            kind = "cpu"
            symbol = "CPU"
            symbol-color = "#f38ba8"
            color = "#cdd6f4"
            background = "#1e1e2e"
            """);

        var options = new TomlSettingsProvider(path).Load().ToWorkspaceBarOptions();

        Assert.Equal(28, options.Height);
        Assert.Equal(1, options.Style!.BorderWidth);
        Assert.Equal(6, options.Style.BorderRadius);
        Assert.Equal(6, options.Style.Spacing);
        Assert.Equal("Cascadia Mono", options.Style.FontFamily);
        Assert.Equal(12, options.Style.FontSize);
        var widgetsSection = options.Sections!.First(section => section.Id == "widgets");
        Assert.Equal(12, widgetsSection.Style.FontSize);
        Assert.Equal(0, widgetsSection.Style.BorderRadius);
        Assert.Equal(ColorTranslator.FromHtml("#313244"), widgetsSection.Style.Background);
        var cpu = options.Widgets.First(widget => widget.Id == "cpu");
        Assert.Equal(12, cpu.Style!.FontSize);
        Assert.Equal(0, cpu.Style.BorderRadius);
        Assert.Equal(ColorTranslator.FromHtml("#f38ba8"), cpu.SymbolForeground);
    }

    [Fact]
    public void Load_ColorNaming_MapsColorAndCurrentColor()
    {
        var path = Path.Combine(CreateTempDir(), "settings.toml");
        File.WriteAllText(path, """
            [statusbar.workspaces]
            background = "#111111"
            color = "#222222"
            current-background = "#333333"
            current-color = "#444444"

            [statusbar.layout]
            background = "#555555"
            color = "#666666"

            [statusbar.title]
            background = "#777777"
            current-background = "#888888"
            current-color = "#999999"
            """);

        var options = new TomlSettingsProvider(path).Load().ToWorkspaceBarOptions();

        Assert.Equal(ColorTranslator.FromHtml("#222222"), options.Workspaces.Foreground);
        Assert.Equal(ColorTranslator.FromHtml("#444444"), options.Workspaces.CurrentForeground);
        Assert.Equal(ColorTranslator.FromHtml("#666666"), options.Layout.Foreground);
        Assert.Equal(ColorTranslator.FromHtml("#999999"), options.Title.CurrentForeground);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RootlessWM-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
