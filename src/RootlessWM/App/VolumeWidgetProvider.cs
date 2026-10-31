using System.Globalization;
using System.Runtime.InteropServices;

namespace RootlessWM.App;

internal sealed class VolumeWidgetProvider(ISystemVolume volume, ConsoleDiagnosticLog log) : IWidgetProvider
{
    private int _wheelDelta;
    private int? _lastError;

    public string Key => "volume";

    public string GetText() => GetValues()["output"];

    public IReadOnlyDictionary<string, string> GetValues()
    {
        try
        {
            var state = volume.Sample();
            _lastError = null;
            var percent = Math.Round(state.Scalar * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = percent,
                ["muted"] = state.Muted ? "true" : "false",
                ["status"] = state.Muted ? "muted" : "",
                ["output"] = state.Muted ? $"{percent}% (muted)" : $"{percent}%"
            };
        }
        catch (COMException exception)
        {
            ReportError(exception);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = "N/A",
                ["muted"] = "unknown",
                ["status"] = "unavailable",
                ["output"] = "N/A"
            };
        }
    }

    public void Scroll(int delta)
    {
        _wheelDelta += delta;
        var notches = _wheelDelta / 120;
        _wheelDelta %= 120;
        if (notches == 0)
        {
            return;
        }

        try
        {
            volume.Adjust(notches * 0.02F);
            _lastError = null;
        }
        catch (COMException exception)
        {
            ReportError(exception);
        }
    }

    public void ToggleMute()
    {
        try
        {
            volume.ToggleMute();
            _lastError = null;
        }
        catch (COMException exception)
        {
            ReportError(exception);
        }
    }

    private void ReportError(COMException exception)
    {
        if (_lastError != exception.HResult)
        {
            log.Error("system_volume_unavailable", new { exception = exception.HResult });
            _lastError = exception.HResult;
        }
    }
}
