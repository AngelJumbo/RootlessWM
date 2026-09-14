namespace RootlessWM.App;

public sealed record LaunchHotkeySettings(
    string Hotkey,
    string Command,
    string? Args = null,
    string? WorkingDirectory = null,
    bool RunAsAdmin = false);
