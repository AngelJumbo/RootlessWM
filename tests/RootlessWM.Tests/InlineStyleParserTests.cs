using System.Drawing;
using RootlessWM.App;
using SkiaSharp;
using Xunit;

namespace RootlessWM.Tests;

public sealed class InlineStyleParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_NullOrEmpty_ReturnsEmpty(string? input)
    {
        var result = InlineStyleParser.Parse(input);
        Assert.True(result.IsEmpty);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Parse_PlainText_ReturnsSingleUnstyledSpan()
    {
        var result = InlineStyleParser.Parse("Hello World");
        Assert.Single(result.Spans);
        Assert.Equal("Hello World", result.Spans[0].Text);
        Assert.Null(result.Spans[0].Foreground);
        Assert.Null(result.Spans[0].FontSize);
        Assert.Null(result.Spans[0].FontWeight);
        Assert.Null(result.Spans[0].FontFamily);
    }

    [Fact]
    public void Parse_SingleColorTag_ParsesSpanWithColor()
    {
        var result = InlineStyleParser.Parse("[c=#89b4fa]MEM[/] 45%");
        Assert.Equal(2, result.Spans.Count);

        Assert.Equal("MEM", result.Spans[0].Text);
        Assert.Equal(Color.FromArgb(255, 137, 180, 250), result.Spans[0].Foreground);

        Assert.Equal(" 45%", result.Spans[1].Text);
        Assert.Null(result.Spans[1].Foreground);
    }

    [Fact]
    public void Parse_MultiAttributeTag_ParsesAllAttributes()
    {
        var result = InlineStyleParser.Parse("[c=#f38ba8 f=\"Cascadia Code\" w=bold s=14]CPU[/]");
        Assert.Single(result.Spans);
        var span = result.Spans[0];

        Assert.Equal("CPU", span.Text);
        Assert.Equal(Color.FromArgb(255, 243, 139, 168), span.Foreground);
        Assert.Equal("Cascadia Code", span.FontFamily);
        Assert.Equal(SKFontStyleWeight.Bold, span.FontWeight);
        Assert.Equal(14f, span.FontSize);
    }

    [Fact]
    public void Parse_EscapedBracket_ProducesLiteralBracket()
    {
        var result = InlineStyleParser.Parse("[[c=red]] normal");
        Assert.Single(result.Spans);
        Assert.Equal("[c=red] normal", result.Spans[0].Text);
        Assert.Null(result.Spans[0].Foreground);
    }

    [Fact]
    public void Parse_NestedTags_PopsCorrectly()
    {
        var result = InlineStyleParser.Parse("[c=red]Red [w=bold]BoldRed[/] BackToRed[/] Plain");
        Assert.Equal(4, result.Spans.Count);

        Assert.Equal("Red ", result.Spans[0].Text);
        Assert.Equal(Color.Red.ToArgb(), result.Spans[0].Foreground?.ToArgb());
        Assert.Null(result.Spans[0].FontWeight);

        Assert.Equal("BoldRed", result.Spans[1].Text);
        Assert.Equal(Color.Red.ToArgb(), result.Spans[1].Foreground?.ToArgb());
        Assert.Equal(SKFontStyleWeight.Bold, result.Spans[1].FontWeight);

        Assert.Equal(" BackToRed", result.Spans[2].Text);
        Assert.Equal(Color.Red.ToArgb(), result.Spans[2].Foreground?.ToArgb());
        Assert.Null(result.Spans[2].FontWeight);

        Assert.Equal(" Plain", result.Spans[3].Text);
        Assert.Null(result.Spans[3].Foreground);
    }

    [Fact]
    public void Parse_UnclosedTag_GracefullyAppliesStyleToEnd()
    {
        var result = InlineStyleParser.Parse("[c=#123456]Unclosed");
        Assert.Single(result.Spans);
        Assert.Equal("Unclosed", result.Spans[0].Text);
        Assert.Equal(Color.FromArgb(255, 0x12, 0x34, 0x56), result.Spans[0].Foreground);
    }

    [Fact]
    public void Parse_HexColorVariations_ParsedCorrectly()
    {
        Assert.True(InlineStyleParser.TryParseColor("#f00", out var c1));
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), c1);

        Assert.True(InlineStyleParser.TryParseColor("#ff0000", out var c2));
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), c2);

        Assert.True(InlineStyleParser.TryParseColor("#ff000080", out var c3));
        Assert.Equal(Color.FromArgb(0x80, 0xff, 0, 0), c3);

        Assert.True(InlineStyleParser.TryParseColor("#f008", out var c4));
        Assert.Equal(Color.FromArgb(0x88, 0xff, 0, 0), c4);
    }

    [Fact]
    public void Parse_Weights_ParsedCorrectly()
    {
        Assert.True(InlineStyleParser.TryParseWeight("bold", out var w1));
        Assert.Equal(SKFontStyleWeight.Bold, w1);

        Assert.True(InlineStyleParser.TryParseWeight("semibold", out var w2));
        Assert.Equal(SKFontStyleWeight.SemiBold, w2);

        Assert.True(InlineStyleParser.TryParseWeight("600", out var w3));
        Assert.Equal(SKFontStyleWeight.SemiBold, w3);

        Assert.True(InlineStyleParser.TryParseWeight("light", out var w4));
        Assert.Equal(SKFontStyleWeight.Light, w4);
    }

    [Fact]
    public void StyledText_PlainText_CombinesSpans()
    {
        var result = InlineStyleParser.Parse("[c=red]Hello[/] [w=bold]World[/]");
        Assert.Equal("Hello World", result.PlainText);
        Assert.Equal(3, result.Spans.Count);
    }

    [Fact]
    public void Parse_MultipleFormattedTagsInSequence_ParsesCorrectly()
    {
        var result = InlineStyleParser.Parse("[c=#f38ba8 f=\"Cascadia Code\" w=bold]CPU[/] [c=#a6e3a1 s=11]45%[/]");
        Assert.Equal(3, result.Spans.Count);

        Assert.Equal("CPU", result.Spans[0].Text);
        Assert.Equal(Color.FromArgb(255, 243, 139, 168), result.Spans[0].Foreground);
        Assert.Equal("Cascadia Code", result.Spans[0].FontFamily);
        Assert.Equal(SKFontStyleWeight.Bold, result.Spans[0].FontWeight);

        Assert.Equal(" ", result.Spans[1].Text);
        Assert.Null(result.Spans[1].Foreground);

        Assert.Equal("45%", result.Spans[2].Text);
        Assert.Equal(Color.FromArgb(255, 166, 227, 161), result.Spans[2].Foreground);
        Assert.Equal(11f, result.Spans[2].FontSize);
    }
}
