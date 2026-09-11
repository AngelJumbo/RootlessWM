using RootlessWM.App;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceBarFormatTests
{
    [Fact]
    public void Format_PlaceholderValues_ReplacesCorrectly()
    {
        var values = new Dictionary<string, string>
        {
            ["used_percent"] = "45",
            ["total"] = "16G"
        };

        var formatted = WorkspaceBarFormat.Format("memory", "[c=#89b4fa s=14 w=bold]MEM[/] {used_percent}%", values);

        Assert.Equal("[c=#89b4fa s=14 w=bold]MEM[/] 45%", formatted);
    }

    [Fact]
    public void Format_ClockWithInlineStyles_ReplacesDateTimePlaceholders()
    {
        var formatted = WorkspaceBarFormat.Format("clock", "[c=#fab387 w=bold]{:%H:%M}[/]", new Dictionary<string, string>());

        var expectedTime = DateTime.Now.ToString("HH:mm");
        Assert.Equal($"[c=#fab387 w=bold]{expectedTime}[/]", formatted);
    }

    [Fact]
    public void Format_MissingFormat_ReturnsOutputValueOrEmpty()
    {
        var formattedWithOutput = WorkspaceBarFormat.Format("custom", null, new Dictionary<string, string> { ["output"] = "hello" });
        Assert.Equal("hello", formattedWithOutput);

        var formattedEmpty = WorkspaceBarFormat.Format("custom", null, new Dictionary<string, string>());
        Assert.Equal(string.Empty, formattedEmpty);
    }
}
