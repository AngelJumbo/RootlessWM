using System.Text.Json;

namespace RootlessWM.App;

internal sealed class ConsoleDiagnosticLog
{
    private const long MaxLogFileBytes = 5 * 1024 * 1024;

    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RootlessWM",
        "logs",
        "RootlessWM.log");

    private static readonly object FileGate = new();

    private readonly bool _writeToFile;

    public ConsoleDiagnosticLog(bool writeToFile = true)
    {
        _writeToFile = writeToFile;
    }

    public void Info(string eventName, object? properties = null)
    {
        Write("information", eventName, properties);
    }

    public void Error(string eventName, object? properties = null)
    {
        Write("error", eventName, properties);
    }

    private void Write(string level, string eventName, object? properties)
    {
        var entry = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level,
            eventName,
            properties
        };

        var line = JsonSerializer.Serialize(entry);

        // Only visible when launched from an existing console (for example, dotnet run).
        Console.WriteLine(line);
        if (_writeToFile)
        {
            AppendToFile(line);
        }
    }

    private static void AppendToFile(string line)
    {
        try
        {
            lock (FileGate)
            {
                var directoryPath = Path.GetDirectoryName(LogFilePath)!;
                Directory.CreateDirectory(directoryPath);

                if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > MaxLogFileBytes)
                {
                    File.Delete(LogFilePath);
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // Diagnostics logging must never crash the manager.
        }
        catch (UnauthorizedAccessException)
        {
            // Diagnostics logging must never crash the manager.
        }
    }
}
