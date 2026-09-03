using System.Text.Json;
using RootlessWM.Domain;

namespace RootlessWM.App;

public sealed class JsonManagedWindowStateStore : IManagedWindowStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonManagedWindowStateStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RootlessWM",
            "managed-windows.json"))
    {
    }

    public JsonManagedWindowStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<ManagedWindowState> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        using var stream = File.OpenRead(_filePath);
        return JsonSerializer.Deserialize<List<ManagedWindowState>>(stream, SerializerOptions)
            ?? throw new InvalidDataException("The managed window state file is empty.");
    }

    public void Save(IReadOnlyList<ManagedWindowState> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var directoryPath = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The managed window state path has no directory.");
        Directory.CreateDirectory(directoryPath);

        var temporaryPath = Path.Combine(directoryPath, $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, windows, SerializerOptions);
            }

            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
