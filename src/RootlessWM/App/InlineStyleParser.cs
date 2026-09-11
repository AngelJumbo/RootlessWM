using System.Drawing;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace RootlessWM.App;

public static class InlineStyleParser
{
    private readonly record struct StyleState(
        Color? Foreground,
        float? FontSize,
        SKFontStyleWeight? FontWeight,
        SKFontStyleSlant? FontSlant,
        string? FontFamily);

    public static StyledText Parse(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return StyledText.Empty;
        }

        if (!input.Contains('['))
        {
            return StyledText.Plain(input);
        }

        var spans = new List<StyledSpan>();
        var buffer = new StringBuilder();
        var styleStack = new Stack<StyleState>();
        var currentStyle = new StyleState();

        void Flush()
        {
            if (buffer.Length > 0)
            {
                spans.Add(new StyledSpan(
                    buffer.ToString(),
                    currentStyle.Foreground,
                    currentStyle.FontSize,
                    currentStyle.FontWeight,
                    currentStyle.FontSlant,
                    currentStyle.FontFamily));
                buffer.Clear();
            }
        }

        var i = 0;
        while (i < input.Length)
        {
            if (input[i] == '[')
            {
                // Check for escaped [[
                if (i + 1 < input.Length && input[i + 1] == '[')
                {
                    buffer.Append('[');
                    i += 2;
                    continue;
                }

                // Check for closing tag [/] or [/...]
                if (i + 1 < input.Length && input[i + 1] == '/')
                {
                    var closeIdx = input.IndexOf(']', i + 2);
                    if (closeIdx != -1)
                    {
                        Flush();
                        currentStyle = styleStack.Count > 0 ? styleStack.Pop() : new StyleState();
                        i = closeIdx + 1;
                        continue;
                    }
                }

                // Look for matching ']'
                var tagEnd = FindTagEnd(input, i + 1);
                if (tagEnd != -1)
                {
                    var tagContent = input.Substring(i + 1, tagEnd - (i + 1)).Trim();
                    if (TryParseAttributes(tagContent, currentStyle, out var newStyle))
                    {
                        Flush();
                        styleStack.Push(currentStyle);
                        currentStyle = newStyle;
                        i = tagEnd + 1;
                        continue;
                    }
                }
            }
            else if (input[i] == ']' && i + 1 < input.Length && input[i + 1] == ']')
            {
                buffer.Append(']');
                i += 2;
                continue;
            }

            buffer.Append(input[i]);
            i++;
        }

        Flush();

        return spans.Count == 0 ? StyledText.Empty : new StyledText(spans);
    }

    private static int FindTagEnd(string input, int start)
    {
        var inQuotes = false;
        var quoteChar = '\0';
        for (var i = start; i < input.Length; i++)
        {
            var ch = input[i];
            if (inQuotes)
            {
                if (ch == quoteChar)
                {
                    inQuotes = false;
                }
            }
            else if (ch is '"' or '\'')
            {
                inQuotes = true;
                quoteChar = ch;
            }
            else if (ch == ']')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseAttributes(string tagContent, StyleState current, out StyleState newStyle)
    {
        newStyle = current;
        if (string.IsNullOrWhiteSpace(tagContent))
        {
            return false;
        }

        var hasValidAttribute = false;
        var i = 0;
        while (i < tagContent.Length)
        {
            while (i < tagContent.Length && char.IsWhiteSpace(tagContent[i]))
            {
                i++;
            }

            if (i >= tagContent.Length)
            {
                break;
            }

            var keyStart = i;
            while (i < tagContent.Length && tagContent[i] != '=' && !char.IsWhiteSpace(tagContent[i]))
            {
                i++;
            }

            var key = tagContent[keyStart..i].Trim().ToLowerInvariant();
            if (i >= tagContent.Length || tagContent[i] != '=')
            {
                return false;
            }

            i++; // skip '='

            while (i < tagContent.Length && char.IsWhiteSpace(tagContent[i]))
            {
                i++;
            }

            if (i >= tagContent.Length)
            {
                return false;
            }

            string value;
            if (tagContent[i] is '"' or '\'')
            {
                var quote = tagContent[i];
                i++;
                var valStart = i;
                while (i < tagContent.Length && tagContent[i] != quote)
                {
                    i++;
                }

                value = tagContent[valStart..i];
                if (i < tagContent.Length)
                {
                    i++; // skip closing quote
                }
            }
            else
            {
                var valStart = i;
                while (i < tagContent.Length && !char.IsWhiteSpace(tagContent[i]))
                {
                    i++;
                }

                value = tagContent[valStart..i];
            }

            switch (key)
            {
                case "c" or "color":
                    if (TryParseColor(value, out var color))
                    {
                        newStyle = newStyle with { Foreground = color };
                        hasValidAttribute = true;
                    }
                    break;
                case "s" or "size":
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size > 0)
                    {
                        newStyle = newStyle with { FontSize = size };
                        hasValidAttribute = true;
                    }
                    break;
                case "w" or "weight":
                    if (TryParseWeight(value, out var weight))
                    {
                        newStyle = newStyle with { FontWeight = weight };
                        hasValidAttribute = true;
                    }
                    break;
                case "f" or "family":
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        newStyle = newStyle with { FontFamily = value };
                        hasValidAttribute = true;
                    }
                    break;
            }
        }

        return hasValidAttribute;
    }

    public static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3 &&
                byte.TryParse(new string(hex[0], 2), NumberStyles.HexNumber, null, out var r3) &&
                byte.TryParse(new string(hex[1], 2), NumberStyles.HexNumber, null, out var g3) &&
                byte.TryParse(new string(hex[2], 2), NumberStyles.HexNumber, null, out var b3))
            {
                color = Color.FromArgb(255, r3, g3, b3);
                return true;
            }

            if (hex.Length == 4 &&
                byte.TryParse(new string(hex[0], 2), NumberStyles.HexNumber, null, out var r4) &&
                byte.TryParse(new string(hex[1], 2), NumberStyles.HexNumber, null, out var g4) &&
                byte.TryParse(new string(hex[2], 2), NumberStyles.HexNumber, null, out var b4) &&
                byte.TryParse(new string(hex[3], 2), NumberStyles.HexNumber, null, out var a4))
            {
                color = Color.FromArgb(a4, r4, g4, b4);
                return true;
            }

            if (hex.Length == 6 &&
                byte.TryParse(hex[..2], NumberStyles.HexNumber, null, out var r6) &&
                byte.TryParse(hex[2..4], NumberStyles.HexNumber, null, out var g6) &&
                byte.TryParse(hex[4..6], NumberStyles.HexNumber, null, out var b6))
            {
                color = Color.FromArgb(255, r6, g6, b6);
                return true;
            }

            if (hex.Length == 8 &&
                byte.TryParse(hex[..2], NumberStyles.HexNumber, null, out var r8) &&
                byte.TryParse(hex[2..4], NumberStyles.HexNumber, null, out var g8) &&
                byte.TryParse(hex[4..6], NumberStyles.HexNumber, null, out var b8) &&
                byte.TryParse(hex[6..8], NumberStyles.HexNumber, null, out var a8))
            {
                color = Color.FromArgb(a8, r8, g8, b8);
                return true;
            }
        }

        try
        {
            var parsed = ColorTranslator.FromHtml(value);
            if (!parsed.IsEmpty)
            {
                color = parsed;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public static bool TryParseWeight(string value, out SKFontStyleWeight weight)
    {
        weight = SKFontStyleWeight.Normal;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "thin" or "100":
                weight = SKFontStyleWeight.Thin;
                return true;
            case "extralight" or "extra-light" or "ultralight" or "ultra-light" or "200":
                weight = SKFontStyleWeight.ExtraLight;
                return true;
            case "light" or "300":
                weight = SKFontStyleWeight.Light;
                return true;
            case "normal" or "regular" or "400":
                weight = SKFontStyleWeight.Normal;
                return true;
            case "medium" or "500":
                weight = SKFontStyleWeight.Medium;
                return true;
            case "semibold" or "semi-bold" or "demibold" or "demi-bold" or "600":
                weight = SKFontStyleWeight.SemiBold;
                return true;
            case "bold" or "700":
                weight = SKFontStyleWeight.Bold;
                return true;
            case "extrabold" or "extra-bold" or "ultrabold" or "ultra-bold" or "800":
                weight = SKFontStyleWeight.ExtraBold;
                return true;
            case "black" or "heavy" or "900":
                weight = SKFontStyleWeight.Black;
                return true;
            default:
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) && num >= 100 && num <= 1000)
                {
                    weight = (SKFontStyleWeight)num;
                    return true;
                }

                return false;
        }
    }
}
