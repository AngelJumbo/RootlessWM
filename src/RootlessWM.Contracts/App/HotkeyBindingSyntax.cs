namespace RootlessWM.App;

internal static class HotkeyBindingSyntax
{
    private static readonly string[] KnownModifiers = ["ALT", "CTRL", "CONTROL", "SHIFT", "SUPER", "WIN"];

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var modifier in parts[..^1])
        {
            if (!KnownModifiers.Contains(modifier.ToUpperInvariant()))
            {
                return false;
            }
        }

        var key = parts[^1];
        if (key.Length == 1 && (char.IsLetter(key[0]) || char.IsDigit(key[0])))
        {
            return true;
        }

        return key.ToUpperInvariant() is "ENTER" or "RETURN" or "ESCAPE" or "SPACE";
    }
}
