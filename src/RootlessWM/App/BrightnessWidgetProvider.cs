using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;

namespace RootlessWM.App;

internal sealed class BrightnessWidgetProvider(
    IScreenBrightness brightness,
    ConsoleDiagnosticLog log,
    IReadOnlyDictionary<string, string>? symbols = null) : IScrollWidgetProvider
{
    private readonly IReadOnlyDictionary<string, string> _symbols = symbols is null
        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        : new Dictionary<string, string>(symbols, StringComparer.OrdinalIgnoreCase);
    private int _wheelDelta;
    private string? _lastError;

    public string Key => "brightness";

    public BrightnessWidgetProvider WithSymbols(IReadOnlyDictionary<string, string>? configuredSymbols)
        => new(brightness, log, configuredSymbols);

    public string GetText() => GetValues()["output"];

    public IReadOnlyDictionary<string, string> GetValues()
    {
        try
        {
            var percent = brightness.Sample();
            var key = percent switch
            {
                <= 33 => "brightnessLow",
                <= 66 => "brightnessMedium",
                _ => "brightnessHigh"
            };
            _lastError = null;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = percent.ToString(CultureInfo.InvariantCulture),
                ["brightness_symbol"] = _symbols.TryGetValue(key, out var symbol) ? symbol : "",
                ["output"] = $"{percent}%"
            };
        }
        catch (Exception exception) when (IsBrightnessError(exception))
        {
            ReportError(exception);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["percent"] = "N/A",
                ["brightness_symbol"] = "",
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
            brightness.Adjust(notches * 2);
            _lastError = null;
        }
        catch (Exception exception) when (IsBrightnessError(exception))
        {
            ReportError(exception);
        }
    }

    private static bool IsBrightnessError(Exception exception)
        => exception is ManagementException or COMException or UnauthorizedAccessException or InvalidOperationException;

    private void ReportError(Exception exception)
    {
        var error = $"{exception.GetType().Name}: {exception.Message}";
        if (_lastError != error)
        {
            log.Error("screen_brightness_unavailable", new { exception = error });
            _lastError = error;
        }
    }
}
