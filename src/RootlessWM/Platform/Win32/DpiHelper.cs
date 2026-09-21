using System.Windows.Forms;

namespace RootlessWM.Platform.Win32;

// Single source of truth for the DPI model: configuration and layout are expressed in logical
// units (96-DPI pixels) and converted to physical pixels only at the window/bitmap boundary.
// The process runs Per-Monitor V2, so window bounds, the Skia bitmap and UpdateLayeredWindow
// all work in physical pixels; the Skia canvas maps logical drawing coordinates via the scale.
internal static class DpiHelper
{
    private const float StandardDpi = 96F;

    public static float GetScale(Control control) => GetScale(control.DeviceDpi);

    public static float GetScale(int dpi) => dpi / StandardDpi;

    // The scale of a specific monitor, used before a form exists on it (e.g. first placement of
    // the bar on a secondary monitor, when DeviceDpi still reflects the creating monitor).
    public static float GetMonitorScale(nint monitorHandle)
        => NativeMethods.GetDpiForMonitor(monitorHandle, NativeMethods.MonitorDpiTypeEffective, out var dpiX, out _) == 0
            ? GetScale((int)dpiX)
            : 1F;

    public static int LogicalToPixel(float value, float scale) => (int)MathF.Round(value * scale);
}
