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
}
