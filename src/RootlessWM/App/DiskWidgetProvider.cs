namespace RootlessWM.App;

internal sealed class DiskWidgetProvider : IWidgetProvider
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, string> _empty = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, string>? _cachedValues;
    private DateTimeOffset _lastSampleTime;

    public string Key => "disk";

    public string GetText() => GetValues().TryGetValue("output", out var output) ? output : string.Empty;

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedValues is not null && now - _lastSampleTime < SampleInterval)
        {
            return _cachedValues;
        }

        _lastSampleTime = now;
        _cachedValues = Sample();
        return _cachedValues;
    }

    private IReadOnlyDictionary<string, string> Sample()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (root is null)
        {
            return _empty;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize == 0)
            {
                return _empty;
            }

            var label = root.TrimEnd('\\');
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            var percent = used * 100.0 / drive.TotalSize;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["root"] = label,
                ["used_percent"] = percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                ["used_bytes"] = used.ToString(),
                ["free_bytes"] = drive.AvailableFreeSpace.ToString(),
                ["total_bytes"] = drive.TotalSize.ToString(),
                ["output"] = $" {label} {percent:0}%"
            };
        }
        catch (IOException)
        {
            return _empty;
        }
        catch (UnauthorizedAccessException)
        {
            return _empty;
        }
    }
}
