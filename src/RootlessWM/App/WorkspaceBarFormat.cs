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
        if (!format.StartsWith(prefix, StringComparison.Ordinal) || !format.EndsWith('}'))
        {
            throw new FormatException("Date and clock formats must use the {:%...} syntax.");
        }

            var pattern = format[2..^1]
                .Replace("%Y", "yyyy", StringComparison.Ordinal)
                .Replace("%m", "MM", StringComparison.Ordinal)
                .Replace("%d", "dd", StringComparison.Ordinal)
                .Replace("%H", "HH", StringComparison.Ordinal)
                .Replace("%M", "mm", StringComparison.Ordinal)
                .Replace("%S", "ss", StringComparison.Ordinal);
        return DateTime.Now.ToString(pattern, CultureInfo.InvariantCulture);
    }
}
