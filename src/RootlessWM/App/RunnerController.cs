using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using RootlessWM.Platform.Win32;
using SkiaSharp;
using System.Security;
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

        _form.ResetQuery();
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

        public void ResetQuery()
        {
            _query = string.Empty;
            _selectedIndex = 0;
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
                    ResetQuery();
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
                ResetQuery();
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
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                var imageInfo = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(imageInfo, bitmapData.Scan0, bitmapData.Stride);
                Draw(surface.Canvas, bitmap.Width, bitmap.Height);
                surface.Flush();
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            UpdateLayer(bitmap);
        }

        private void Draw(SKCanvas canvas, int width, int height)
        {
            var style = _settings.Style;
            var results = _settings.Results;
            var input = _settings.Input;
            var outer = new SKRect(0, 0, width, height);
            var inputRect = new SKRect(20, 20, width - 20, 20 + input.Height);
            var resultsTop = inputRect.Bottom + 12;
            var rowHeight = Math.Max(28, results.RowHeight);

            canvas.Clear(SKColors.Transparent);
            using var panelPaint = new SKPaint { Color = ParseColor(style.Background), IsAntialias = true };
            canvas.DrawRoundRect(outer, Math.Max(0, style.Radius), Math.Max(0, style.Radius), panelPaint);
            using var borderPaint = new SKPaint { Color = ParseColor(style.BorderColor), Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, style.BorderWidth), IsAntialias = true };
            canvas.DrawRoundRect(new SKRect(1, 1, width - 1, height - 1), Math.Max(0, style.Radius), Math.Max(0, style.Radius), borderPaint);

            using var inputPaint = new SKPaint { Color = ParseColor(input.Background), IsAntialias = true };
            canvas.DrawRoundRect(inputRect, 4, 4, inputPaint);
            using var inputText = CreateTextPaint(input.Color, style, style.FontSize);
            var prompt = string.IsNullOrEmpty(_query) ? input.Prompt : _query;
            canvas.DrawText(prompt, inputRect.Left + 12, inputRect.MidY - (inputText.FontMetrics.Ascent + inputText.FontMetrics.Descent) / 2, inputText);

            for (var index = 0; index < _items.Count; index++)
            {
                var top = resultsTop + index * rowHeight;
                var row = new SKRect(20, top, width - 20, top + rowHeight);
                var selected = index == _selectedIndex;
                using var rowPaint = new SKPaint { Color = ParseColor(selected ? results.SelectedBackground : results.Background), IsAntialias = true };
                canvas.DrawRoundRect(row, 4, 4, rowPaint);
                var textLeft = row.Left + 12;
                if (_settings.Icons.Visible)
                {
                    DrawIcon(canvas, _items[index].Target, row, out var iconRight);
                    textLeft = iconRight + _settings.Icons.Padding;
                }
                using var namePaint = CreateTextPaint(selected ? results.SelectedColor : results.Color, style, style.FontSize);
                using var secondaryPaint = CreateTextPaint(results.SecondaryColor, style, Math.Max(9, style.FontSize - 2));
                canvas.DrawText(_items[index].Name, textLeft, row.Top + 17, namePaint);
                canvas.DrawText(_items[index].Source, textLeft, row.Top + 34, secondaryPaint);
            }

            using var statusPaint = CreateTextPaint(results.SecondaryColor, style, Math.Max(9, style.FontSize - 2));
            canvas.DrawText(_items.Count == 0 ? "No applications found" : $"{_items.Count} applications", 20, height - 9, statusPaint);
        }

        private static SKPaint CreateTextPaint(string color, RunnerStyleSettings style, float size)
        {
            return new SKPaint
            {
                Color = ParseColor(color),
                TextSize = size,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(style.FontFamily, style.Italic == true ? SKFontStyle.Italic : SKFontStyle.Normal)
            };
        }

        private void DrawIcon(SKCanvas canvas, string target, SKRect row, out float iconRight)
        {
            var size = Math.Clamp(_settings.Icons.Size, 16, 64);
            var left = row.Left + 12;
            var top = row.Top + Math.Max(0, (row.Height - size) / 2F);
            iconRight = left + size;
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(target);
                if (icon is null)
                {
                    return;
                }

                using var bitmap = icon.ToBitmap();
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                using var skBitmap = SKBitmap.Decode(stream);
                if (skBitmap is not null)
                {
                    canvas.DrawBitmap(skBitmap, new SKRect(left, top, iconRight, top + size));
                }
            }
            catch (Exception exception) when (exception is ArgumentException or System.Runtime.InteropServices.ExternalException or IOException)
            {
                _log.Info("runner_icon_skipped", new { target, reason = exception.GetType().Name });
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

        private static SKColor ParseColor(string value)
        {
            if (value.Length == 9 && value[0] == '#'
                && byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red)
                && byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green)
                && byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue)
                && byte.TryParse(value.AsSpan(7, 2), System.Globalization.NumberStyles.HexNumber, null, out var alpha))
            {
                return new SKColor(red, green, blue, alpha);
            }

            return SKColor.TryParse(value, out var color) ? color : SKColors.Transparent;
        }

        private sealed record RunnerItem(string Name, string Target, string Arguments, string WorkingDirectory, string Source);
    }
}
