using System.Management;
using RootlessWM.App;

namespace RootlessWM.Platform.Win32;

internal sealed class ScreenBrightnessController : IScreenBrightness
{
    public int Sample() => ReadDisplay().Percent;

    public void Adjust(int delta)
    {
        var display = ReadDisplay();
        var target = AdjustLevel(display.Percent, delta, display.Levels);
        using var searcher = new ManagementObjectSearcher(@"root\wmi",
            "SELECT * FROM WmiMonitorBrightnessMethods");
        using var methods = searcher.Get();
        foreach (ManagementObject method in methods)
        {
            using (method)
            {
                if (!string.Equals(method["InstanceName"] as string, display.InstanceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var parameters = method.GetMethodParameters("WmiSetBrightness");
                parameters["Timeout"] = 0U;
                parameters["Brightness"] = target;
                using var result = method.InvokeMethod("WmiSetBrightness", parameters, null);
                return;
            }
        }

        throw new InvalidOperationException("No brightness control method is available for the integrated display.");
    }

    internal static byte AdjustLevel(int percent, int delta, IReadOnlyList<byte> levels)
    {
        var target = Math.Clamp(percent + delta, 0, 100);
        if (levels.Count == 0)
        {
            throw new InvalidOperationException("The display did not report supported brightness levels.");
        }

        // Some panels expose only discrete steps; always advance in the requested direction.
        var ordered = levels.Order().ToArray();
        return delta >= 0
            ? ordered.FirstOrDefault(level => level >= target, ordered[^1])
            : ordered.LastOrDefault(level => level <= target, ordered[0]);
    }

    private static (string InstanceName, int Percent, byte[] Levels) ReadDisplay()
    {
        using var searcher = new ManagementObjectSearcher(@"root\wmi",
            "SELECT InstanceName, CurrentBrightness, Level FROM WmiMonitorBrightness WHERE Active = TRUE");
        using var displays = searcher.Get();
        foreach (ManagementObject display in displays)
        {
            using (display)
            {
                if (display["InstanceName"] is not string instanceName
                    || display["CurrentBrightness"] is not byte percent
                    || display["Level"] is not byte[] levels)
                {
                    throw new InvalidOperationException("The integrated display returned invalid brightness data.");
                }

                return (instanceName, percent, levels);
            }
        }

        throw new InvalidOperationException("No integrated display with WMI brightness support is available.");
    }
}
