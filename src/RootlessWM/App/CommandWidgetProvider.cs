using System.Diagnostics;

namespace RootlessWM.App;

internal sealed class CommandWidgetProvider : IWidgetProvider, IDisposable
{
    public const string Key = "command";

    private readonly string _command;
    private readonly int _intervalMilliseconds;
    private readonly object _sync = new();
    private string _text = string.Empty;
    private DateTime _nextRefresh;
    private bool _running;

    public CommandWidgetProvider(string command, int intervalMilliseconds)
    {
        _command = command;
        _intervalMilliseconds = Math.Max(250, intervalMilliseconds);
    }

    string IWidgetProvider.Key => Key;

    public string GetText()
    {
        lock (_sync)
        {
            if (!_running && DateTime.UtcNow >= _nextRefresh)
            {
                _running = true;
                _nextRefresh = DateTime.UtcNow.AddMilliseconds(_intervalMilliseconds);
                _ = RefreshAsync();
            }

            return _text;
        }
    }

    private async Task RefreshAsync()
    {
        string result = string.Empty;
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/d /s /c \"{_command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            result = (await process.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(2))).Trim();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            result = string.Empty;
        }

        lock (_sync)
        {
            _text = result;
            _running = false;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _running = true;
        }
    }
}
