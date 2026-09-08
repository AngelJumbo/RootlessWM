using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using RootlessWM.Platform.Win32;
using System.Security;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RootlessWM.App;

internal sealed class RunnerController : IDisposable
{
    private readonly ConsoleDiagnosticLog _log;
    private RunnerForm? _form;
    private RunnerSettings _settings = RunnerSettings.Default;
    private bool _disposed;

    public RunnerController(ConsoleDiagnosticLog log)
    {
        _log = log;
    }

    public void ApplySettings(RunnerSettings? settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _settings = settings ?? RunnerSettings.Default;
        _form?.ApplySettings(_settings);
    }

    public void Toggle()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_settings.Enabled)
        {
            _log.Info("runner_ignored", new { reason = "disabled" });
            return;
        }

        _form ??= new RunnerForm(_settings, _log);
        _form.ApplySettings(_settings);
        if (_form.Visible)
        {
            _form.Hide();
            return;
        }

        _form.ShowOnFocusedMonitor();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _form?.Dispose();
        _form = null;
        _disposed = true;
    }

    private sealed class RunnerForm : Form
    {
        private readonly ConsoleDiagnosticLog _log;
        private readonly Direct2DRenderer _renderer = new();
        private RunnerSettings _settings;
        private List<RunnerItem> _items = [];
        private string _query = string.Empty;
        private int _selectedIndex;

        public RunnerForm(RunnerSettings settings, ConsoleDiagnosticLog log)
        {
            _settings = settings;
            _log = log;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = false;
        }

        public void ApplySettings(RunnerSettings settings)
        {
            _settings = settings;
            RefreshResults();
            Redraw();
        }

        public void ShowOnFocusedMonitor()
        {
            var monitor = Screen.FromPoint(Cursor.Position).WorkingArea;
            var width = Math.Clamp(_settings.Window.Width, 320, monitor.Width);
            Width = width;
            RefreshResults();
            Height = CalculateHeight(monitor.Height);
            Location = _settings.Window.Position.ToLowerInvariant() switch
            {
                "top-left" => new Point(monitor.Left + _settings.Window.OffsetX, monitor.Top + _settings.Window.OffsetY),
                "top-right" => new Point(monitor.Right - width - _settings.Window.OffsetX, monitor.Top + _settings.Window.OffsetY),
                "center" => new Point(monitor.Left + (monitor.Width - width) / 2 + _settings.Window.OffsetX, monitor.Top + (monitor.Height - Height) / 2 + _settings.Window.OffsetY),
                _ => new Point(monitor.Left + (monitor.Width - width) / 2 + _settings.Window.OffsetX, monitor.Top + _settings.Window.OffsetY)
            };
            Show();
            Activate();
            Focus();
            Redraw();
        }

        protected override bool ShowWithoutActivation => false;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= unchecked((int)(NativeMethods.WsExToolWindow | NativeMethods.WsExLayered));
                return parameters;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs eventArgs)
        {
            base.OnKeyPress(eventArgs);
            if (!char.IsControl(eventArgs.KeyChar))
            {
                _query += eventArgs.KeyChar;
                RefreshResults();
                Redraw();
                eventArgs.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            switch (eventArgs.KeyCode)
            {
                case Keys.Escape:
                    Hide();
                    break;
                case Keys.Back:
                    if (_query.Length > 0)
                    {
                        _query = _query[..^1];
                        RefreshResults();
                        Redraw();
                    }
                    break;
                case Keys.Down:
                    MoveSelection(1);
                    break;
                case Keys.Up:
                    MoveSelection(-1);
                    break;
                case Keys.Enter:
                    LaunchSelected();
                    break;
                default:
                    return;
            }

            eventArgs.SuppressKeyPress = true;
        }

        private void RefreshResults()
        {
            _items = Discover(_settings)
                .Where(item => Matches(item, _query))
                .Take(Math.Clamp(_settings.MaxResults, 1, 20))
                .ToList();
            _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _items.Count - 1));
            Height = CalculateHeight(Screen.FromPoint(Cursor.Position).WorkingArea.Height);
        }

        private int CalculateHeight(int workAreaHeight)
        {
            var inputHeight = Math.Max(32, _settings.Input.Height);
            var rowHeight = Math.Max(28, _settings.Results.RowHeight);
            var resultHeight = _items.Count * rowHeight;
            var desired = 20 + inputHeight + 12 + resultHeight + 28;
            return Math.Min(Math.Clamp(_settings.Window.MaxHeight, 160, workAreaHeight), desired);
        }

        private void MoveSelection(int delta)
        {
            if (_items.Count == 0)
            {
                return;
            }

            _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _items.Count - 1);
            Redraw();
        }

        private void LaunchSelected()
        {
            var selected = _items.Count > 0 ? _items[_selectedIndex] : null;
            var target = selected?.Target ?? _query.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = selected?.Target ?? target,
                    Arguments = selected?.Arguments ?? string.Empty,
                    WorkingDirectory = selected?.WorkingDirectory ?? string.Empty,
                    UseShellExecute = true
                });
                Hide();
                _log.Info("runner_launched", new { name = selected?.Name ?? target, source = selected?.Source ?? "command" });
            }
            catch (Win32Exception exception)
            {
                _log.Error("runner_launch_failed", new { target, error = exception.NativeErrorCode });
            }
            catch (InvalidOperationException exception)
            {
                _log.Error("runner_launch_failed", new { target, error = exception.Message });
            }
        }

        private void Redraw()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0)
            {
                return;
            }

            using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            _renderer.Dpi = DeviceDpi;
            _renderer.Render(bitmap, canvas => Draw(canvas, bitmap.Width, bitmap.Height));
            DrawIcons(bitmap);

            UpdateLayer(bitmap);
        }

        private void Draw(Direct2DRenderer.Direct2DCanvas canvas, int width, int height)
        {
            var style = _settings.Style;
            var results = _settings.Results;
            var input = _settings.Input;
            var outer = new RectangleF(0, 0, width, height);
            var inputRect = new RectangleF(20, 20, width - 40, input.Height);
            var resultsTop = inputRect.Bottom + 12;
            var rowHeight = Math.Max(28, results.RowHeight);

            canvas.FillRoundedRectangle(outer, Math.Max(0, style.Radius), ParseColor(style.Background));
            canvas.DrawRoundedRectangle(new RectangleF(1, 1, width - 2, height - 2), Math.Max(0, style.Radius), ParseColor(style.BorderColor), Math.Max(1, style.BorderWidth));
            canvas.FillRoundedRectangle(inputRect, 4, ParseColor(input.Background));
            var prompt = string.IsNullOrEmpty(_query) ? input.Prompt : _query;
            canvas.DrawText(prompt, new RectangleF(inputRect.Left + 12, inputRect.Top, inputRect.Width - 24, inputRect.Height), ParseColor(input.Color), style.FontFamily, style.FontSize * DeviceDpi / 96F, style.Italic == true ? FontStyle.Italic : FontStyle.Regular, Direct2DRenderer.Direct2DCanvas.TextAlignment.Left);

            for (var index = 0; index < _items.Count; index++)
            {
                var top = resultsTop + index * rowHeight;
                var row = new RectangleF(20, top, width - 40, rowHeight);
                var selected = index == _selectedIndex;
                canvas.FillRoundedRectangle(row, 4, ParseColor(selected ? results.SelectedBackground : results.Background));
                var textLeft = row.Left + 12;
                if (_settings.Icons.Visible)
                {
                    textLeft += Math.Clamp(_settings.Icons.Size, 16, 64) + _settings.Icons.Padding;
                }
                var textWidth = Math.Max(1, row.Right - textLeft - 12);
                canvas.DrawText(_items[index].Name, new RectangleF(textLeft, row.Top, textWidth, row.Height / 2), ParseColor(selected ? results.SelectedColor : results.Color), style.FontFamily, style.FontSize * DeviceDpi / 96F, style.Italic == true ? FontStyle.Italic : FontStyle.Regular, Direct2DRenderer.Direct2DCanvas.TextAlignment.Left);
                canvas.DrawText(_items[index].Source, new RectangleF(textLeft, row.Top + row.Height / 2, textWidth, row.Height / 2), ParseColor(results.SecondaryColor), style.FontFamily, Math.Max(9, style.FontSize - 2) * DeviceDpi / 96F, style.Italic == true ? FontStyle.Italic : FontStyle.Regular, Direct2DRenderer.Direct2DCanvas.TextAlignment.Left);
            }

            canvas.DrawText(_items.Count == 0 ? "No applications found" : $"{_items.Count} applications", new RectangleF(20, height - 24, width - 40, 20), ParseColor(results.SecondaryColor), style.FontFamily, Math.Max(9, style.FontSize - 2) * DeviceDpi / 96F, style.Italic == true ? FontStyle.Italic : FontStyle.Regular, Direct2DRenderer.Direct2DCanvas.TextAlignment.Left);
        }

        private void DrawIcons(Bitmap bitmap)
        {
            if (!_settings.Icons.Visible)
            {
                return;
            }

            using var graphics = Graphics.FromImage(bitmap);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            var size = Math.Clamp(_settings.Icons.Size, 16, 64);
            var rowHeight = Math.Max(28, _settings.Results.RowHeight);
            var resultsTop = 20 + Math.Max(32, _settings.Input.Height) + 12;
            for (var index = 0; index < _items.Count; index++)
            {
                var top = resultsTop + index * rowHeight;
                try
                {
                    using var icon = Icon.ExtractAssociatedIcon(_items[index].Target);
                    if (icon is not null)
                    {
                        using var iconBitmap = icon.ToBitmap();
                        graphics.DrawImage(iconBitmap, new Rectangle(32, top + Math.Max(0, (rowHeight - size) / 2), size, size));
                    }
                }
                catch (Exception exception) when (exception is ArgumentException or ExternalException or IOException)
                {
                    _log.Info("runner_icon_skipped", new { target = _items[index].Target, reason = exception.GetType().Name });
                }
            }
        }

        private void UpdateLayer(Bitmap bitmap)
        {
            using var screenGraphics = Graphics.FromHwnd(nint.Zero);
            var screenDeviceContext = screenGraphics.GetHdc();
            var memoryDeviceContext = NativeMethods.CreateCompatibleDC(screenDeviceContext);
            var bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
            var previousBitmap = NativeMethods.SelectObject(memoryDeviceContext, bitmapHandle);
            try
            {
                var destination = new NativeMethods.Point { X = Left, Y = Top };
                var size = new NativeMethods.Size { Width = bitmap.Width, Height = bitmap.Height };
                var source = new NativeMethods.Point();
                var blend = new NativeMethods.BlendFunction
                {
                    BlendOp = NativeMethods.AcSrcOver,
                    SourceConstantAlpha = byte.MaxValue,
                    AlphaFormat = NativeMethods.AcSrcAlpha
                };
                _ = NativeMethods.UpdateLayeredWindow(Handle, screenDeviceContext, ref destination, ref size, memoryDeviceContext, ref source, 0, ref blend, NativeMethods.UlwAlpha);
            }
            finally
            {
                _ = NativeMethods.SelectObject(memoryDeviceContext, previousBitmap);
                _ = NativeMethods.DeleteObject(bitmapHandle);
                _ = NativeMethods.DeleteDC(memoryDeviceContext);
                screenGraphics.ReleaseHdc(screenDeviceContext);
            }
        }

        private static List<RunnerItem> Discover(RunnerSettings settings)
        {
            var sources = settings.Sources is { Count: > 0 } configured ? configured : ["start-menu", "app-paths", "path"];
            var items = new Dictionary<string, RunnerItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                if (source.Equals("path", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in DiscoverPath()) items.TryAdd(item.Target, item);
                }
                else if (source.Equals("start-menu", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in DiscoverStartMenu()) items.TryAdd(item.Target, item);
                }
                else if (source.Equals("app-paths", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var item in DiscoverAppPaths()) items.TryAdd(item.Target, item);
                }
            }

            return items.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<RunnerItem> DiscoverPath()
        {
            var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var items = new List<RunnerItem>();
                try
                {
                    foreach (var path in Directory.EnumerateFiles(directory))
                    {
                        if (IsExecutable(path)) items.Add(new RunnerItem(Path.GetFileNameWithoutExtension(path), path, string.Empty, directory, "PATH"));
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                foreach (var item in items) yield return item;
            }
        }

        private static IEnumerable<RunnerItem> DiscoverStartMenu()
        {
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) }
                .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                foreach (var path in CollectShortcutFiles(root))
                {
                    yield return new RunnerItem(Path.GetFileNameWithoutExtension(path), path, string.Empty, Path.GetDirectoryName(path) ?? root, "Start Menu");
                }
            }
        }

        private static IEnumerable<RunnerItem> DiscoverAppPaths()
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var appPaths = baseKey.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\App Paths");
                if (appPaths is null) continue;
                var items = new List<RunnerItem>();
                foreach (var name in appPaths.GetSubKeyNames())
                {
                    try
                    {
                        using var key = appPaths.OpenSubKey(name);
                        var target = key?.GetValue(null) as string;
                        if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
                            items.Add(new RunnerItem(Path.GetFileNameWithoutExtension(target), target, string.Empty, Path.GetDirectoryName(target) ?? string.Empty, "App Paths"));
                    }
                    catch (SecurityException) { }
                }
                foreach (var item in items) yield return item;
            }
        }

        private static List<string> CollectShortcutFiles(string root)
        {
            var files = new List<string>();
            var pending = new Queue<string>([root]);
            while (pending.Count > 0)
            {
                var directory = pending.Dequeue();
                try { files.AddRange(Directory.EnumerateFiles(directory).Where(path => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                try { foreach (var child in Directory.EnumerateDirectories(directory)) pending.Enqueue(child); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
            return files;
        }

        private static bool Matches(RunnerItem item, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var normalized = query.Trim();
            return item.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(item.Target).Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || item.Target.Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExecutable(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) || extension.Equals(".com", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
        }

        private static Color ParseColor(string value)
        {
            if (value.Length == 9 && value[0] == '#'
                && byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red)
                && byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green)
                && byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue)
                && byte.TryParse(value.AsSpan(7, 2), System.Globalization.NumberStyles.HexNumber, null, out var alpha))
            {
                return Color.FromArgb(alpha, red, green, blue);
            }

            try
            {
                return ColorTranslator.FromHtml(value);
            }
            catch (Exception) when (value is not null)
            {
                return Color.Transparent;
            }
        }

        private sealed record RunnerItem(string Name, string Target, string Arguments, string WorkingDirectory, string Source);
    }
}
