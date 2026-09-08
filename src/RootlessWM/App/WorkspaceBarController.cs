using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using RootlessWM.Domain;
using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class WorkspaceBarController : IDisposable
{
    private readonly int _workspaceCount;
    private readonly Dictionary<nint, WorkspaceBarView> _bars = [];
    private readonly Func<nint> _getFocusedMonitorHandle;
    private readonly Func<string> _getFocusedWindowTitle;
    private readonly ConsoleDiagnosticLog _log;
    private readonly WorkspaceBarWidgetRegistry _widgetRegistry = WorkspaceBarWidgetRegistry.CreateDefault();
    private readonly System.Windows.Forms.Timer _widgetsTimer;
    private readonly HashSet<nint> _fullscreenMonitors = [];
    private WorkspaceBarOptions _options = WorkspaceBarOptionsDefaults.Create();
    private bool _configuredVisible;
    private bool _globallyVisible = true;

    public WorkspaceBarController(int workspaceCount, Func<nint> getFocusedMonitorHandle, Func<string> getFocusedWindowTitle, ConsoleDiagnosticLog? log = null)
    {
        if (workspaceCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(workspaceCount));
        }

        _workspaceCount = workspaceCount;
        _getFocusedMonitorHandle = getFocusedMonitorHandle;
        _getFocusedWindowTitle = getFocusedWindowTitle;
        _log = log ?? new ConsoleDiagnosticLog();
        _widgetsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _widgetsTimer.Tick += (_, _) => RefreshWidgets();
        _widgetsTimer.Start();
    }

    public void ApplyOptions(WorkspaceBarOptions options)
    {
        _options = options;
        _configuredVisible = options.Visible;
        ApplyVisibility();
    }

    public void SetVisible(bool visible)
    {
        _globallyVisible = visible;
        ApplyVisibility();
    }

    // A fullscreen window owns the whole monitor, so that monitor's bar must step aside.
    public void SetFullscreenMonitors(IReadOnlyCollection<nint> monitorHandles)
    {
        ArgumentNullException.ThrowIfNull(monitorHandles);
        if (_fullscreenMonitors.SetEquals(monitorHandles))
        {
            return;
        }

        _fullscreenMonitors.Clear();
        _fullscreenMonitors.UnionWith(monitorHandles);
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        foreach (var (handle, bar) in _bars)
        {
            bar.ApplyOptions(GetOptionsFor(handle));
        }
    }

    private WorkspaceBarOptions GetOptionsFor(nint monitorHandle)
    {
        return _options with
        {
            Visible = _configuredVisible && _globallyVisible && !_fullscreenMonitors.Contains(monitorHandle)
        };
    }

    public void Update(
        IReadOnlyList<MonitorWorkArea> monitors,
        Func<nint, int> getCurrentWorkspace,
        Func<nint, MasterStackLayoutMode> getLayoutMode)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(getCurrentWorkspace);
        ArgumentNullException.ThrowIfNull(getLayoutMode);
        var monitorHandles = monitors.Select(monitor => monitor.Handle).ToHashSet();
        foreach (var staleHandle in _bars.Keys.Where(handle => !monitorHandles.Contains(handle)).ToArray())
        {
            _bars[staleHandle].Dispose();
            _bars.Remove(staleHandle);
        }

        foreach (var monitor in monitors.Where(monitor => monitor.IsUsable))
        {
            if (!_bars.TryGetValue(monitor.Handle, out var bar))
            {
                bar = new WorkspaceBarView(_workspaceCount, _widgetRegistry, _log);
                _bars.Add(monitor.Handle, bar);
            }

            bar.ApplyOptions(GetOptionsFor(monitor.Handle));
            bar.Update(
                getCurrentWorkspace(monitor.Handle),
                getLayoutMode(monitor.Handle),
                monitor.Bounds,
                _options.Height);
        }

        RefreshWidgets();
    }

    private void RefreshWidgets()
    {
        if (_bars.Count == 0)
        {
            return;
        }

        var focusedMonitorHandle = _getFocusedMonitorHandle();
        var focusedWindowTitle = _getFocusedWindowTitle();
        foreach (var (handle, bar) in _bars)
        {
            bar.UpdateWidgets(handle == focusedMonitorHandle, focusedWindowTitle);
        }
    }

    public void Dispose()
    {
        _widgetsTimer.Stop();
        _widgetsTimer.Dispose();
        foreach (var bar in _bars.Values)
        {
            bar.Dispose();
        }

        _bars.Clear();
    }

    private sealed class WorkspaceBarView : IDisposable
    {
        private readonly WorkspaceBarForm _form;
        private readonly List<(string Signature, IWidgetProvider Provider)> _widgetProviders = [];
        private readonly WorkspaceBarWidgetRegistry _widgetRegistry;
        private readonly ConsoleDiagnosticLog _log;
        private readonly Direct2DRenderer _renderer = new();
        private Rectangle _lastBounds = Rectangle.Empty;
        private WorkspaceBarStyleOptions _barStyle = WorkspaceBarStyleOptions.Default;
        private WorkspaceBarOptions _options = WorkspaceBarOptionsDefaults.Create();
        private int _currentWorkspace;
        private MasterStackLayoutMode _layoutMode;
        private bool _isFocused;
        private string _focusedWindowTitle = string.Empty;

        public WorkspaceBarView(int workspaceCount, WorkspaceBarWidgetRegistry widgetRegistry, ConsoleDiagnosticLog log)
        {
            _widgetRegistry = widgetRegistry;
            _log = log;
            _form = new WorkspaceBarForm
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                AutoScaleMode = AutoScaleMode.None,
                TopMost = true
            };
            WorkspaceCount = workspaceCount;
        }

        private int WorkspaceCount { get; }

        public void ApplyOptions(WorkspaceBarOptions options)
        {
            _options = options;
            _barStyle = options.Style ?? WorkspaceBarStyleOptions.Default with { Background = options.Background };
            _form.Visible = options.Visible;
            Redraw();
        }

        private static Padding AddSpacing(Padding padding, int spacing)
            => new(padding.Left + spacing, padding.Top, padding.Right + spacing, padding.Bottom);

        private static Padding AddBorder(Padding padding, int borderWidth)
            => new(
                padding.Left + borderWidth,
                padding.Top + borderWidth,
                padding.Right + borderWidth,
                padding.Bottom + borderWidth);

        public void Update(
            int currentWorkspace,
            MasterStackLayoutMode layoutMode,
            WindowBounds workArea,
            int height)
        {
            var margin = new Padding(
                Math.Max(0, _barStyle.Margin.Left),
                Math.Max(0, _barStyle.Margin.Top),
                Math.Max(0, _barStyle.Margin.Right),
                Math.Max(0, _barStyle.Margin.Bottom));
            var left = workArea.Left + margin.Left;
            var right = workArea.Left + workArea.Width - margin.Right;
            var width = Math.Max(1, right - left);
            var barHeight = Math.Max(1, height);
            var top = workArea.Top + margin.Top;
            _form.SetBounds(left, top, width, barHeight);
            if (_form.IsHandleCreated)
            {
                _form.ApplyCornerPreference(_barStyle.BorderRadius);
                NativeMethods.SetWindowPos(
                    _form.Handle,
                    nint.Zero,
                    left,
                    top,
                    width,
                    barHeight,
                    NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder);
            }
            var bounds = new Rectangle(left, top, width, barHeight);
            if (bounds != _lastBounds)
            {
                _lastBounds = bounds;
                _log.Info("workspace_bar_bounds", new
                {
                    workArea,
                    margin,
                    bounds,
                    expectedRightGap = margin.Right,
                    actualRightGap = workArea.Left + workArea.Width - bounds.Right
                });
            }
            _currentWorkspace = currentWorkspace;
            _layoutMode = layoutMode;
            Redraw();
        }

        public void UpdateWidgets(bool isFocused, string focusedWindowTitle)
        {
            _isFocused = isFocused;
            _focusedWindowTitle = focusedWindowTitle;
            Redraw();
        }

        private void Redraw()
        {
            if (!_form.IsHandleCreated || _form.Width <= 0 || _form.Height <= 0)
            {
                return;
            }

            using var bitmap = new Bitmap(_form.Width, _form.Height, PixelFormat.Format32bppPArgb);
            _renderer.Dpi = _form.DeviceDpi;
            _renderer.Render(bitmap, canvas => Draw(canvas, bitmap.Width, bitmap.Height));

            _form.UpdateLayer(bitmap);
        }

        private void Draw(Direct2DRenderer.Direct2DCanvas canvas, int width, int height)
        {
            var barRect = new RectangleF(0, 0, width, height);
            DrawBox(canvas, barRect, _barStyle);
            var content = Inset(barRect, _barStyle.Padding, _barStyle.BorderWidth);
            var sections = (_options.Sections ?? []).Where(section => section.Style.Visible).ToList();
            DrawSections(canvas, content, sections.Where(section => section.Alignment == WorkspaceBarSectionAlignment.Left).ToList(), WorkspaceBarSectionAlignment.Left);
            DrawSections(canvas, content, sections.Where(section => section.Alignment == WorkspaceBarSectionAlignment.Right).ToList(), WorkspaceBarSectionAlignment.Right);
            DrawSections(canvas, content, sections.Where(section => section.Alignment == WorkspaceBarSectionAlignment.Center).ToList(), WorkspaceBarSectionAlignment.Center);
        }

        private void DrawSections(Direct2DRenderer.Direct2DCanvas canvas, RectangleF content, IReadOnlyList<WorkspaceBarSectionOptions> sections, WorkspaceBarSectionAlignment alignment)
        {
            var metrics = sections.Select(section => (Section: section, Width: MeasureSection(section))).ToList();
            var totalWidth = metrics.Sum(item => item.Width) + Math.Max(0, metrics.Count - 1) * _barStyle.Spacing;
            var x = alignment switch
            {
                WorkspaceBarSectionAlignment.Right => content.Right - totalWidth,
                WorkspaceBarSectionAlignment.Center => content.Left + Math.Max(0, (content.Width - totalWidth) / 2F),
                _ => content.Left
            };

            foreach (var (section, measuredWidth) in metrics)
            {
                var width = Math.Min(measuredWidth, content.Width);
                var rect = new RectangleF(x, content.Top, width, content.Height);
                DrawSection(canvas, section, rect);
                x += width + _barStyle.Spacing;
            }
        }

        private float MeasureSection(WorkspaceBarSectionOptions section)
        {
            var contentWidth = section.Id.ToLowerInvariant() switch
            {
                "workspaces" => MeasureWorkspaces(section.Style),
                "layout" => MeasureText(GetLayoutText(), section.Style),
                "title" => MeasureText(_focusedWindowTitle, section.Style),
                _ => MeasureWidgets(section, section.Style)
            };
            var width = contentWidth + section.Style.Padding.Left + section.Style.Padding.Right + (section.Style.BorderWidth * 2);
            if (section.Style.MinWidth is int minWidth)
            {
                width = Math.Max(width, minWidth);
            }

            if (section.Style.MaxWidth is int maxWidth)
            {
                width = Math.Min(width, maxWidth);
            }

            return width;
        }

        private void DrawSection(Direct2DRenderer.Direct2DCanvas canvas, WorkspaceBarSectionOptions section, RectangleF rect)
        {
            DrawBox(canvas, rect, section.Style);
            var content = Inset(rect, section.Style.Padding, section.Style.BorderWidth);
            switch (section.Id.ToLowerInvariant())
            {
                case "workspaces":
                    DrawWorkspaces(canvas, content, section.Style);
                    break;
                case "layout":
                    DrawText(canvas, GetLayoutText(), content, _options.Layout.Foreground, section.Style, Direct2DRenderer.Direct2DCanvas.TextAlignment.Center);
                    break;
                case "title":
                    if (_isFocused)
                    {
                        DrawText(canvas, _focusedWindowTitle, content, _options.Title.CurrentForeground, section.Style, Direct2DRenderer.Direct2DCanvas.TextAlignment.Center, ellipsis: true);
                    }
                    break;
                default:
                    DrawWidgets(canvas, content, section, section.Style);
                    break;
            }
        }

        private void DrawWorkspaces(Direct2DRenderer.Direct2DCanvas canvas, RectangleF rect, WorkspaceBarStyleOptions style)
        {
            var x = rect.Left;
            for (var index = 0; index < WorkspaceCount; index++)
            {
                var text = _options.Workspaces.Symbols.Count > index ? _options.Workspaces.Symbols[index] : (index + 1).ToString();
                var textWidth = MeasureText(text, style);
                var itemRect = new RectangleF(x, rect.Top, textWidth + 8, rect.Height);
                var active = index == _currentWorkspace;
                DrawFill(canvas, itemRect, active ? _options.Workspaces.CurrentBackground : _options.Workspaces.Background, 0);
                DrawText(canvas, text, itemRect, active ? _options.Workspaces.CurrentForeground : _options.Workspaces.Foreground, style, Direct2DRenderer.Direct2DCanvas.TextAlignment.Center);
                x = itemRect.Right + 4;
            }
        }

        private void DrawWidgets(Direct2DRenderer.Direct2DCanvas canvas, RectangleF rect, WorkspaceBarSectionOptions section, WorkspaceBarStyleOptions sectionStyle)
        {
            if (!_isFocused)
            {
                return;
            }

            var widgets = GetSectionWidgets(section);
            var widgetWidths = widgets.Select(widget => MeasureWidget(widget, sectionStyle)).ToList();
            var x = section.Alignment == WorkspaceBarSectionAlignment.Right
                ? rect.Right - widgetWidths.Sum() - Math.Max(0, widgetWidths.Count - 1) * _barStyle.Spacing
                : rect.Left;
            for (var index = 0; index < widgets.Count; index++)
            {
                var widget = widgets[index];
                var style = widget.Style ?? sectionStyle;
                var text = GetOrCreateProvider(GetWidgetIndex(widget), widget)?.GetText() ?? string.Empty;
                var symbolWidth = string.IsNullOrEmpty(widget.Symbol) ? 0 : MeasureText(widget.Symbol, style) + 6;
                var width = widgetWidths[index];
                var widgetRect = new RectangleF(x, rect.Top, width, rect.Height);
                DrawFill(canvas, widgetRect, widget.ResultBackground, style.BorderRadius);
                var content = Inset(widgetRect, style.Padding, style.BorderWidth);
                if (!string.IsNullOrEmpty(widget.Symbol))
                {
                    DrawText(canvas, widget.Symbol, new RectangleF(content.Left, content.Top, symbolWidth, content.Height), widget.SymbolForeground, style, Direct2DRenderer.Direct2DCanvas.TextAlignment.Left);
                }

                DrawText(canvas, text, new RectangleF(content.Left + symbolWidth, content.Top, content.Right - content.Left - symbolWidth, content.Height), widget.ResultForeground, style, Direct2DRenderer.Direct2DCanvas.TextAlignment.Left);
                x = widgetRect.Right + _barStyle.Spacing;
            }
        }

        private IReadOnlyList<WorkspaceBarWidgetOptions> GetSectionWidgets(WorkspaceBarSectionOptions section)
            => section.Widgets.Count == 0
                ? _options.Widgets
                : _options.Widgets.Where(widget => section.Widgets.Contains(widget.Id, StringComparer.OrdinalIgnoreCase) || section.Widgets.Contains(widget.Kind, StringComparer.OrdinalIgnoreCase)).ToList();

        private float MeasureWorkspaces(WorkspaceBarStyleOptions style)
            => Enumerable.Range(0, WorkspaceCount)
                .Sum(index => MeasureText(_options.Workspaces.Symbols.Count > index ? _options.Workspaces.Symbols[index] : (index + 1).ToString(), style) + 12);

        private float MeasureWidgets(WorkspaceBarSectionOptions section, WorkspaceBarStyleOptions sectionStyle)
            => GetSectionWidgets(section).Sum(widget => MeasureWidget(widget, sectionStyle) + _barStyle.Spacing);

        private float MeasureWidget(WorkspaceBarWidgetOptions widget, WorkspaceBarStyleOptions sectionStyle)
        {
            var style = widget.Style ?? sectionStyle;
            var text = GetOrCreateProvider(GetWidgetIndex(widget), widget)?.GetText() ?? string.Empty;
            return MeasureText(widget.Symbol, style) + MeasureText(text, style) + style.Padding.Left + style.Padding.Right + 6;
        }

        private string GetLayoutText()
            => _options.Layout.Symbols.TryGetValue(_layoutMode, out var symbol) ? symbol : FormatLayoutMode(_layoutMode);

        private static void DrawBox(Direct2DRenderer.Direct2DCanvas canvas, RectangleF rect, WorkspaceBarStyleOptions style)
        {
            DrawFill(canvas, rect, style.Background, style.BorderRadius);
            if (style.BorderWidth <= 0)
            {
                return;
            }

            var inset = style.BorderWidth / 2F;
            canvas.DrawRoundedRectangle(new RectangleF(rect.Left + inset, rect.Top + inset, rect.Width - style.BorderWidth, rect.Height - style.BorderWidth), style.BorderRadius, style.BorderColor, style.BorderWidth);
        }

        private static void DrawFill(Direct2DRenderer.Direct2DCanvas canvas, RectangleF rect, Color color, int radius)
        {
            canvas.FillRoundedRectangle(rect, radius, color);
        }

        private void DrawText(Direct2DRenderer.Direct2DCanvas canvas, string text, RectangleF rect, Color color, WorkspaceBarStyleOptions style, Direct2DRenderer.Direct2DCanvas.TextAlignment align, bool ellipsis = false)
        {
            if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            var pixelSize = style.FontSize * _form.DeviceDpi / 72F;
            var displayText = ellipsis ? Ellipsize(text, rect.Width, style, pixelSize) : text;
            canvas.DrawText(displayText, rect, color, style.FontFamily, pixelSize, style.FontStyle, align);
        }

        private float MeasureText(string text, WorkspaceBarStyleOptions style)
        {
            return Direct2DRenderer.MeasureText(text ?? string.Empty, style.FontFamily, style.FontSize * _form.DeviceDpi / 72F, style.FontStyle);
        }

        private float MeasureText(string text, WorkspaceBarStyleOptions style, float pixelSize)
            => Direct2DRenderer.MeasureText(text ?? string.Empty, style.FontFamily, pixelSize, style.FontStyle);

        private string Ellipsize(string text, float maxWidth, WorkspaceBarStyleOptions style, float pixelSize)
        {
            const string suffix = "...";
            if (MeasureText(text, style, pixelSize) <= maxWidth)
            {
                return text;
            }

            for (var length = text.Length - 1; length > 0; length--)
            {
                var candidate = text[..length] + suffix;
                if (MeasureText(candidate, style, pixelSize) <= maxWidth)
                {
                    return candidate;
                }
            }

            return suffix;
        }

        private static RectangleF Inset(RectangleF rect, Padding padding, int borderWidth)
            => new(rect.Left + padding.Left + borderWidth, rect.Top + padding.Top + borderWidth,
                rect.Width - padding.Horizontal - borderWidth * 2, rect.Height - padding.Vertical - borderWidth * 2);

        private IWidgetProvider? GetOrCreateProvider(int index, WorkspaceBarWidgetOptions widget)
        {
            var signature = $"{widget.Kind}\n{widget.Text}\n{widget.Command}\n{widget.IntervalMilliseconds}";
            while (_widgetProviders.Count <= index)
            {
                _widgetProviders.Add((string.Empty, null!));
            }

            if (_widgetProviders[index].Provider is null || _widgetProviders[index].Signature != signature)
            {
                if (_widgetProviders[index].Provider is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _widgetProviders[index] = (signature, _widgetRegistry.Create(widget.Kind, widget)!);
            }

            return _widgetProviders[index].Provider;
        }

        private bool IsSectionVisible(string id)
            => (_options.Sections ?? []).FirstOrDefault(section =>
                string.Equals(section.Id, id, StringComparison.OrdinalIgnoreCase))?.Style.Visible ?? true;

        private WorkspaceBarStyleOptions GetSectionStyle(string id)
            => (_options.Sections ?? []).FirstOrDefault(section =>
                string.Equals(section.Id, id, StringComparison.OrdinalIgnoreCase))?.Style ?? _barStyle;

        private bool IsWidgetShown(WorkspaceBarWidgetOptions widget)
        {
            var section = (_options.Sections ?? []).FirstOrDefault(candidate =>
                string.Equals(candidate.Id, "widgets", StringComparison.OrdinalIgnoreCase) ||
                candidate.Widgets.Contains(widget.Id, StringComparer.OrdinalIgnoreCase) ||
                candidate.Widgets.Contains(widget.Kind, StringComparer.OrdinalIgnoreCase));
            return section is null || section.Widgets.Count == 0 ||
                section.Widgets.Contains(widget.Id, StringComparer.OrdinalIgnoreCase) ||
                section.Widgets.Contains(widget.Kind, StringComparer.OrdinalIgnoreCase);
        }

        private int GetWidgetIndex(WorkspaceBarWidgetOptions widget)
        {
            for (var index = 0; index < _options.Widgets.Count; index++)
            {
                if (ReferenceEquals(_options.Widgets[index], widget) || _options.Widgets[index].Id == widget.Id)
                {
                    return index;
                }
            }

            return 0;
        }

        private static string FormatLayoutMode(MasterStackLayoutMode mode)
        {
            return mode switch
            {
                MasterStackLayoutMode.MasterLeft => "ML",
                MasterStackLayoutMode.MasterTop => "MT",
                MasterStackLayoutMode.Monocle => "MON",
                MasterStackLayoutMode.Floating => "FLT",
                _ => "?"
            };
        }

        public void Dispose()
        {
            foreach (var (_, provider) in _widgetProviders)
            {
                (provider as IDisposable)?.Dispose();
            }

            _form.Dispose();
            _renderer.Dispose();
        }

        private sealed class WorkspaceBarForm : Form
        {
            protected override bool ShowWithoutActivation => true;

            public void ApplyCornerPreference(int radius)
            {
                if (!IsHandleCreated)
                {
                    return;
                }

                uint preference = radius > 0 ? 2U : 1U;
                _ = NativeMethods.DwmSetWindowAttribute(
                    Handle,
                    NativeMethods.DwmwaWindowCornerPreference,
                    ref preference,
                    sizeof(uint));
            }

            public void UpdateLayer(Bitmap bitmap)
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
                    _ = NativeMethods.UpdateLayeredWindow(
                        Handle,
                        screenDeviceContext,
                        ref destination,
                        ref size,
                        memoryDeviceContext,
                        ref source,
                        0,
                        ref blend,
                        NativeMethods.UlwAlpha);
                }
                finally
                {
                    _ = NativeMethods.SelectObject(memoryDeviceContext, previousBitmap);
                    _ = NativeMethods.DeleteObject(bitmapHandle);
                    _ = NativeMethods.DeleteDC(memoryDeviceContext);
                    screenGraphics.ReleaseHdc(screenDeviceContext);
                }
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    const int wsExNoActivate = 0x08000000;
                    const int wsExToolWindow = 0x00000080;
                    var createParams = base.CreateParams;
                    createParams.ExStyle |= wsExNoActivate | wsExToolWindow | NativeMethods.WsExLayered;
                    return createParams;
                }
            }
        }
    }
}

internal static class WorkspaceBarOptionsDefaults
{
    public static WorkspaceBarOptions Create()
    {
        return new WorkspaceBarOptions(
            true,
            24,
            ColorTranslator.FromHtml("#101010"),
            new WorkspaceBarWorkspaceOptions(
                ColorTranslator.FromHtml("#101010"),
                ColorTranslator.FromHtml("#D0D0D0"),
                ColorTranslator.FromHtml("#FFFFFF"),
                ColorTranslator.FromHtml("#101010"),
                []),
            new WorkspaceBarLayoutOptions(
                ColorTranslator.FromHtml("#101010"),
                ColorTranslator.FromHtml("#D0D0D0"),
                new Dictionary<MasterStackLayoutMode, string>()),
            new WorkspaceBarTitleOptions(
                ColorTranslator.FromHtml("#101010"),
                ColorTranslator.FromHtml("#101010"),
                ColorTranslator.FromHtml("#FFFFFF")),
            []);
    }
}
