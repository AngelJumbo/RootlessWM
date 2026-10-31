using System.Drawing;
using System.Reflection;
using RootlessWM.App;
using SkiaSharp;
using Xunit;

namespace RootlessWM.Tests;

public sealed class WorkspaceBarTextRenderingTests
{
    [Theory]
    [InlineData("HHHH", 1)]
    [InlineData("HHHH\nHHHHHHHHHH", 2)]
    [InlineData("[c=red]HH[/]HH\nHHHHHHHHHH", 2)]
    public void DrawText_CentersEveryLineWithinModule(string text, int lineCount)
    {
        using var bitmap = new SKBitmap(300, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var rect = new SKRect(20, 10, 280, 90);
        var style = WorkspaceBarStyleOptions.Default with
        {
            FontFamily = "Cascadia Mono",
            FontSize = 18
        };
        var viewType = typeof(WorkspaceBarController).GetNestedType("WorkspaceBarView", BindingFlags.NonPublic);
        Assert.NotNull(viewType);
        var drawText = viewType.GetMethod("DrawText", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(drawText);

        drawText.Invoke(null, [canvas, text, rect, Color.White, style, false]);
        canvas.Flush();

        for (var line = 0; line < lineCount; line++)
        {
            var top = (int)rect.Top + line * (int)rect.Height / lineCount;
            var bottom = (int)rect.Top + (line + 1) * (int)rect.Height / lineCount;
            var left = bitmap.Width;
            var right = -1;
            for (var y = top; y < bottom; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).Alpha > 0)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
                }
            }

            Assert.True(right >= left, $"Line {line} was not rendered.");
            Assert.InRange(Math.Abs((left + right) / 2F - rect.MidX), 0F, 2F);
        }
    }
}
