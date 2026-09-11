using System.Drawing;
using SkiaSharp;

namespace RootlessWM.App;

public sealed record StyledSpan(
    string Text,
    Color? Foreground = null,
    float? FontSize = null,
    SKFontStyleWeight? FontWeight = null,
    SKFontStyleSlant? FontSlant = null,
    string? FontFamily = null);
