namespace RootlessWM.App;

internal sealed class DiskWidgetProvider : IWidgetProvider
{
    public string Key => "disk";

    public string GetText()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (root is null)
        {
            return string.Empty;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize == 0)
            {
                return string.Empty;
            }

            var usedPercent = (drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize;
            return $" {root.TrimEnd('\\')} {usedPercent:0}%";
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public IReadOnlyDictionary<string, string> GetValues()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (root is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            var percent = used * 100.0 / drive.TotalSize;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["root"] = root.TrimEnd('\\'),
                ["used_percent"] = percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
                ["used_bytes"] = used.ToString(),
                ["free_bytes"] = drive.AvailableFreeSpace.ToString(),
                ["total_bytes"] = drive.TotalSize.ToString(),
                ["output"] = $" {root.TrimEnd('\\')} {percent:0}%"
            };
        }
        catch (IOException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (UnauthorizedAccessException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
