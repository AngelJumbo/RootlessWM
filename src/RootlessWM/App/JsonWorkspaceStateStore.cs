using System.Text.Json;
using RootlessWM.Domain;

namespace RootlessWM.App;

public sealed class JsonWorkspaceStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonWorkspaceStateStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RootlessWM",
            "workspaces.json"))
    {
    }

    public JsonWorkspaceStateStore(string filePath)
    {
        _filePath = filePath;
    }

    public WorkspaceStateData? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        using var stream = File.OpenRead(_filePath);
        return JsonSerializer.Deserialize<WorkspaceStateData>(stream, SerializerOptions)
            ?? throw new InvalidDataException("The workspace state file is empty.");
    }

    public void Save(WorkspaceStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var directoryPath = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("The workspace state path has no directory.");
        Directory.CreateDirectory(directoryPath);

        var temporaryPath = Path.Combine(directoryPath, $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, state, SerializerOptions);
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
}
