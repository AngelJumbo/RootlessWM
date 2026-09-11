using System.Globalization;

namespace RootlessWM.App;

internal static class WorkspaceBarFormat
{
    public static string Format(string type, string? format, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(format))
        {
            return values.TryGetValue("output", out var output) ? output : string.Empty;
        }

        if (string.Equals(type, "clock", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "date", StringComparison.OrdinalIgnoreCase))
        {
            return FormatDateTime(format);
        }

        var result = format;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return result;
    }

    private static string FormatDateTime(string format)
    {
        const string prefix = "{:%";
        var idx = format.IndexOf(prefix, StringComparison.Ordinal);
        if (idx == -1)
        {
            throw new FormatException("Date and clock formats must contain the {:%...} syntax.");
        }

        var result = format;
        var now = DateTime.Now;
        while ((idx = result.IndexOf(prefix, StringComparison.Ordinal)) != -1)
        {
            var endIdx = result.IndexOf('}', idx + prefix.Length);
            if (endIdx == -1)
            {
                throw new FormatException("Date and clock formats must contain a closing '}' for {:%...}.");
            }

            var pattern = result.Substring(idx + 2, endIdx - (idx + 2))
                .Replace("%Y", "yyyy", StringComparison.Ordinal)
                .Replace("%m", "MM", StringComparison.Ordinal)
                .Replace("%d", "dd", StringComparison.Ordinal)
                .Replace("%H", "HH", StringComparison.Ordinal)
                .Replace("%M", "mm", StringComparison.Ordinal)
                .Replace("%S", "ss", StringComparison.Ordinal);

            var formatted = now.ToString(pattern, CultureInfo.InvariantCulture);
            result = string.Concat(result.AsSpan(0, idx), formatted, result.AsSpan(endIdx + 1));
        }

        return result;
    }
}
