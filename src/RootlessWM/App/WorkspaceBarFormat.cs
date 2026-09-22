namespace RootlessWM.App;

internal static class WorkspaceBarFormat
{
    public static string Format(string type, string? format, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(format))
        {
            return values.TryGetValue("output", out var output) ? output : string.Empty;
        }

        var result = format;
        foreach (var (key, value) in values)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return result;
    }
}
