using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using RootlessWM.Domain;
using RootlessWM.Platform.Win32;
using SkiaSharp;

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
    private readonly Dictionary<nint, HashSet<int>> _hiddenWorkspaces = [];
    private WorkspaceBarOptions _options = WorkspaceBarOptionsDefaults.Create();
    private bool _configuredVisible;
    private bool _globallyVisible = true;
    private IReadOnlyList<MonitorWorkArea>? _lastMonitors;
    private Func<nint, int>? _lastGetWorkspace;
    private Func<nint, MasterStackLayoutMode>? _lastGetLayout;
    private bool _reapplyingForScaleChange;

    // Raised when a clickable module item (e.g. a workspace label) is clicked, carrying the
    // module's configured action name (see `on-click` in settings) and the item index within it.
    public event Action<string, int>? ActionInvoked;

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

    // Toggles the bar for the given monitor's currently active workspace only, so the
    // hidden/shown state persists per monitor+workspace pair across workspace switches.
    public bool ToggleStatusBar(nint monitorHandle, int workspaceIndex)
    {
        if (!_hiddenWorkspaces.TryGetValue(monitorHandle, out var hidden))
        {
            hidden = [];
            _hiddenWorkspaces[monitorHandle] = hidden;
        }

        if (!hidden.Remove(workspaceIndex))
        {
            hidden.Add(workspaceIndex);
        }

        ApplyVisibility();
        return hidden.Contains(workspaceIndex);
    }

    // Lets tiling know whether it should reserve space for this monitor+workspace's bar.
    public bool IsHiddenForWorkspace(nint monitorHandle, int workspaceIndex)
        => _hiddenWorkspaces.TryGetValue(monitorHandle, out var hidden) && hidden.Contains(workspaceIndex);

    private void ApplyVisibility()
    {
        foreach (var (handle, bar) in _bars)
        {
            bar.ApplyOptions(GetOptionsFor(handle, bar.CurrentWorkspace));
        }
    }

    private WorkspaceBarOptions GetOptionsFor(nint monitorHandle, int workspaceIndex)
    {
        var hiddenByToggle = _hiddenWorkspaces.TryGetValue(monitorHandle, out var hidden) && hidden.Contains(workspaceIndex);
        return _options with
        {
            Visible = _configuredVisible && _globallyVisible && !_fullscreenMonitors.Contains(monitorHandle) && !hiddenByToggle
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
        _lastMonitors = monitors;
        _lastGetWorkspace = getCurrentWorkspace;
        _lastGetLayout = getLayoutMode;
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
                bar.ScaleChanged += HandleBarScaleChanged;
                bar.ItemClicked += (action, index) => ActionInvoked?.Invoke(action, index);
                _bars.Add(monitor.Handle, bar);
            }

            bar.ApplyOptions(GetOptionsFor(monitor.Handle, getCurrentWorkspace(monitor.Handle)));
            bar.Update(
                getCurrentWorkspace(monitor.Handle),
                getLayoutMode(monitor.Handle),
                monitor.Handle,
                monitor.Bounds,
                _options.Height,
                _options.Position,
                monitor.IsPrimary);
        }

        RefreshWidgets();
    }

    // A monitor DPI change invalidates the bar's physical bounds and bitmap; re-running the last
    // Update recomputes both from the new scale without waiting for the next status refresh.
    private void HandleBarScaleChanged(object? sender, EventArgs e)
    {
        if (_reapplyingForScaleChange || _lastMonitors is null || _lastGetWorkspace is null || _lastGetLayout is null)
        {
            return;
        }

        _reapplyingForScaleChange = true;
        try
        {
            Update(_lastMonitors, _lastGetWorkspace, _lastGetLayout);
        }
        finally
        {
            _reapplyingForScaleChange = false;
        }
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
            if (!bar.IsVisible)
            {
                continue;
            }

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
        // Cached for the process lifetime: Skia typeface resolution is expensive and the set of
        // font/size/colour combinations a bar uses is bounded by the configuration.
        private static readonly Dictionary<(string FontFamily, SKFontStyleWeight Weight, SKFontStyleSlant Slant), SKTypeface> Typefaces = [];
        private static readonly Dictionary<TextPaintKey, SKPaint> TextPaints = [];

        private readonly WorkspaceBarForm _form;
        private readonly List<(string Signature, IWidgetProvider Provider)> _widgetProviders = [];
        private readonly WorkspaceBarWidgetRegistry _widgetRegistry;
        private readonly ConsoleDiagnosticLog _log;
        private readonly System.Text.StringBuilder _signatureBuilder = new();
        private readonly Dictionary<string, ModuleBinding> _moduleBindings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _widgetIndexes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _moduleTextCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _widgetTextCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<WorkspaceBarWidgetOptions>> _sectionWidgets = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<WorkspaceBarModuleOptions> _visibleModules = [];
        private readonly List<float> _moduleWidths = [];
        private readonly List<float> _sectionWidths = [];
        private List<WorkspaceBarSectionOptions> _visibleSections = [];
        private List<WorkspaceBarSectionOptions> _sectionsLeft = [];
        private List<WorkspaceBarSectionOptions> _sectionsCenter = [];
        private List<WorkspaceBarSectionOptions> _sectionsRight = [];
        private Bitmap? _renderBitmap;
        private string? _lastRenderSignature;
        private int _optionsRevision;
        private int _appliedCornerRadius = -1;
        private Rectangle _lastBounds = Rectangle.Empty;
        private WorkspaceBarStyleOptions _barStyle = WorkspaceBarStyleOptions.Default;
        private WorkspaceBarOptions _options = WorkspaceBarOptionsDefaults.Create();
        private int _currentWorkspace;
        private MasterStackLayoutMode _layoutMode;
        private bool _isFocused;
        private bool _isPrimary;
        private bool _isVertical;
        private string _focusedWindowTitle = string.Empty;
        private float _scale = 1F;
        private readonly List<(SKRect Rect, string Action, int Index)> _clickTargets = [];

        // Raised when the bar's monitor DPI changes so the controller can recompute the
        // physical bounds and bitmap immediately instead of waiting for the next status refresh.
        public event EventHandler? ScaleChanged;

        // Raised when a clickable region drawn during the last frame (e.g. a workspace label) is
        // clicked, carrying the module's configured action name and the item index within it.
        public event Action<string, int>? ItemClicked;

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
            _form.DpiChanged += (_, _) =>
            {
                if (SetScale(DpiHelper.GetScale(_form)))
                {
                    ScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            _form.MouseDown += HandleMouseDown;
        }

        // Mouse coordinates arrive in physical pixels; the click targets recorded during Draw()
        // are in logical (pre-DPI-scale) coordinates, matching the canvas.Scale(_scale, _scale)
        // transform applied in Redraw(). Dividing by _scale keeps hit-testing correct across DPIs.
        private void HandleMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _clickTargets.Count == 0)
            {
                return;
            }

            var x = e.X / _scale;
            var y = e.Y / _scale;
            foreach (var target in _clickTargets)
            {
                if (target.Rect.Contains(x, y))
                {
                    ItemClicked?.Invoke(target.Action, target.Index);
                    return;
                }
            }
        }

        private bool SetScale(float scale)
        {
            if (scale == _scale)
            {
                return false;
            }

            _log.Info("workspace_bar_dpi_changed", new { oldScale = _scale, newScale = scale });
            _scale = scale;
            return true;
        }

        private int WorkspaceCount { get; }

        public int CurrentWorkspace => _currentWorkspace;

        public bool IsVisible => _form.Visible;

        public void ApplyOptions(WorkspaceBarOptions options)
        {
            var changed = !Equals(_options, options);
            if (changed)
            {
                _optionsRevision++;
            }

            _options = options;
            _barStyle = options.Style ?? WorkspaceBarStyleOptions.Default with { Background = options.Background };
            if (changed)
            {
                RebuildOptionCaches();
            }

            _form.Visible = options.Visible;
            Redraw();
        }

        // Everything derived purely from the configuration, resolved once per options change instead of per frame.
        private void RebuildOptionCaches()
        {
            _moduleBindings.Clear();
            var index = 0;
            foreach (var module in EnumerateModules())
            {
                _moduleBindings[module.Id] = new ModuleBinding(index, CreateModuleWidgetOptions(module));
                index++;
            }

            _widgetIndexes.Clear();
            for (var widgetIndex = 0; widgetIndex < _options.Widgets.Count; widgetIndex++)
            {
                _widgetIndexes[_options.Widgets[widgetIndex].Id] = widgetIndex;
            }

            var sections = (_options.Sections ?? []).Where(section => section.Style.Visible).ToList();
            _visibleSections = sections;
            _sectionsLeft = sections.Where(section => section.Alignment == WorkspaceBarSectionAlignment.Left).ToList();
            _sectionsCenter = sections.Where(section => section.Alignment == WorkspaceBarSectionAlignment.Center).ToList();
            _sectionsRight = sections.Where(section => section.Alignment == WorkspaceBarSectionAlignment.Right).ToList();

            _sectionWidgets.Clear();
            foreach (var section in sections)
            {
                _sectionWidgets[section.Id] = section.Widgets.Count == 0
                    ? _options.Widgets
                    : _options.Widgets
                        .Where(widget => section.Widgets.Contains(widget.Id, StringComparer.OrdinalIgnoreCase)
                            || section.Widgets.Contains(widget.Kind, StringComparer.OrdinalIgnoreCase))
                        .ToList();
            }
        }

        private IEnumerable<WorkspaceBarModuleOptions> EnumerateModules()
            => (_options.ModulesLeft ?? []).Concat(_options.ModulesCenter ?? []).Concat(_options.ModulesRight ?? []);

        private static WorkspaceBarWidgetOptions CreateModuleWidgetOptions(WorkspaceBarModuleOptions module)
            => new(
                module.Type,
                module.Symbol ?? "",
                Color.Transparent,
                module.Style.Foreground,
                module.Style.Background,
                module.Style.Foreground,
                module.Text ?? "",
                module.Command ?? "",
                module.IntervalMilliseconds,
                module.Style)
            {
                Id = module.Id,
                BatterySymbols = module.BatterySymbols
            };

        private sealed record ModuleBinding(int Index, WorkspaceBarWidgetOptions Widget);

        public void Update(
            int currentWorkspace,
            MasterStackLayoutMode layoutMode,
            nint monitorHandle,
            WindowBounds workArea,
            int thickness,
            WorkspaceBarPosition position,
            bool isPrimary)
        {
            SetScale(DpiHelper.GetMonitorScale(monitorHandle));
            var margin = new Padding(
                DpiHelper.LogicalToPixel(Math.Max(0, _barStyle.Margin.Left), _scale),
                DpiHelper.LogicalToPixel(Math.Max(0, _barStyle.Margin.Top), _scale),
                DpiHelper.LogicalToPixel(Math.Max(0, _barStyle.Margin.Right), _scale),
                DpiHelper.LogicalToPixel(Math.Max(0, _barStyle.Margin.Bottom), _scale));
            var barThickness = DpiHelper.LogicalToPixel(Math.Max(1, thickness), _scale);
            int left, top, width, barHeight;
            switch (position)
            {
                case WorkspaceBarPosition.Left:
                    left = workArea.Left + margin.Left;
                    top = workArea.Top + margin.Top;
                    width = barThickness;
                    barHeight = Math.Max(1, workArea.Height - margin.Top - margin.Bottom);
                    break;
                case WorkspaceBarPosition.Right:
                    width = barThickness;
                    left = workArea.Left + workArea.Width - width - margin.Right;
                    top = workArea.Top + margin.Top;
                    barHeight = Math.Max(1, workArea.Height - margin.Top - margin.Bottom);
                    break;
                case WorkspaceBarPosition.Bottom:
                    left = workArea.Left + margin.Left;
                    width = Math.Max(1, workArea.Width - margin.Left - margin.Right);
                    barHeight = barThickness;
                    top = workArea.Top + workArea.Height - barHeight - margin.Bottom;
                    break;
                default:
                    left = workArea.Left + margin.Left;
                    width = Math.Max(1, workArea.Width - margin.Left - margin.Right);
                    barHeight = barThickness;
                    top = workArea.Top + margin.Top;
                    break;
            }

            _isVertical = position is WorkspaceBarPosition.Left or WorkspaceBarPosition.Right;
            var bounds = new Rectangle(left, top, width, barHeight);
            if (bounds != _lastBounds)
            {
                _lastBounds = bounds;
                _form.SetBounds(left, top, width, barHeight);
                if (_form.IsHandleCreated)
                {
                    NativeMethods.SetWindowPos(
                        _form.Handle,
                        nint.Zero,
                        left,
                        top,
                        width,
                        barHeight,
                        NativeMethods.SwpNoActivate | NativeMethods.SwpNoZOrder);
                }

                _log.Info("workspace_bar_bounds", new
                {
                    workArea,
                    margin,
                    bounds,
                    expectedRightGap = margin.Right,
                    actualRightGap = workArea.Left + workArea.Width - bounds.Right
                });
            }

            if (_form.IsHandleCreated && _appliedCornerRadius != _barStyle.BorderRadius)
            {
                _appliedCornerRadius = _barStyle.BorderRadius;
                _form.ApplyCornerPreference(_barStyle.BorderRadius);
            }

            _currentWorkspace = currentWorkspace;
            _layoutMode = layoutMode;
            _isPrimary = isPrimary;
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
            if (!_form.Visible)
            {
                _lastRenderSignature = null;
                return;
            }

            if (!_form.IsHandleCreated || _form.Width <= 0 || _form.Height <= 0)
            {
                return;
            }

            var signature = BuildRenderSignature();
            if (string.Equals(signature, _lastRenderSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastRenderSignature = signature;

            var bitmap = GetRenderBitmap(_form.Width, _form.Height);
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                var imageInfo = new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(imageInfo, bitmapData.Scan0, bitmapData.Stride);
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                // The bitmap is physical-resolution; one canvas transform maps the logical drawing
                // coordinates used everywhere below onto it. Do not scale coordinates by hand.
                canvas.Scale(_scale, _scale);
                Draw(canvas, bitmap.Width / _scale, bitmap.Height / _scale);
                surface.Flush();
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            _form.UpdateLayer(bitmap);
        }

        private Bitmap GetRenderBitmap(int width, int height)
        {
            if (_renderBitmap is { } bitmap && bitmap.Width == width && bitmap.Height == height)
            {
                return bitmap;
            }

            _renderBitmap?.Dispose();
            _renderBitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            return _renderBitmap;
        }

        // Everything that can change what the bar looks like, flattened into one comparable string so an
        // unchanged tick can skip the Skia draw and the layered-window round trip entirely. Widget values are
        // resolved here once per tick and reused by the measure and draw passes.
        private string BuildRenderSignature()
        {
            _moduleTextCache.Clear();
            _widgetTextCache.Clear();
            var builder = _signatureBuilder;
            builder.Clear();
            builder.Append(_optionsRevision).Append('|')
                .Append(_currentWorkspace).Append('|')
                .Append((int)_layoutMode).Append('|')
                .Append(_isFocused ? '1' : '0')
                .Append(_isPrimary ? '1' : '0').Append('|')
                .Append(_form.Width).Append('x').Append(_form.Height).Append('@').Append(_scale).Append('|');

            if (_options.ModulesLeft is not null || _options.ModulesCenter is not null || _options.ModulesRight is not null)
            {
                AppendModuleSignature(builder, _options.ModulesLeft);
                AppendModuleSignature(builder, _options.ModulesCenter);
                AppendModuleSignature(builder, _options.ModulesRight);
                return builder.ToString();
            }

            foreach (var section in _visibleSections)
            {
                builder.Append(section.Id).Append(':');
                switch (section.Id.ToLowerInvariant())
                {
                    case "workspaces":
                    case "layout":
                        break;
                    case "title":
                        if (_isFocused)
                        {
                            builder.Append(_focusedWindowTitle);
                        }

                        break;
                    default:
                        if (_isFocused)
                        {
                            foreach (var widget in GetSectionWidgets(section))
                            {
                                builder.Append(ResolveWidgetText(widget)).Append('\u001f');
                            }
                        }

                        break;
                }

                builder.Append('|');
            }

            return builder.ToString();
        }

        private void AppendModuleSignature(System.Text.StringBuilder builder, IReadOnlyList<WorkspaceBarModuleOptions>? modules)
        {
            foreach (var module in modules ?? [])
            {
                if (!module.Style.Visible || !module.IsShownOn(_isPrimary, _isFocused))
                {
                    continue;
                }

                builder.Append(module.Id).Append(':');
                if (string.Equals(module.Type, "workspaces", StringComparison.OrdinalIgnoreCase))
                {
                    // Covered by the workspace index already in the signature.
                }
                else if (string.Equals(module.Type, "layout", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Append(GetLayoutText(module));
                }
                else
                {
                    builder.Append(ResolveModuleText(module));
                }

                builder.Append('|');
            }
        }

        private void Draw(SKCanvas canvas, float width, float height)
        {
            _clickTargets.Clear();
            var barRect = new SKRect(0, 0, width, height);
            DrawBox(canvas, barRect, _barStyle);
            var content = Inset(barRect, _barStyle.Padding, _barStyle.BorderWidth);
            if (_options.ModulesLeft is not null || _options.ModulesCenter is not null || _options.ModulesRight is not null)
            {
                DrawModuleGroup(canvas, content, _options.ModulesLeft ?? [], WorkspaceBarSectionAlignment.Left);
                DrawModuleGroup(canvas, content, _options.ModulesRight ?? [], WorkspaceBarSectionAlignment.Right);
                DrawModuleGroup(canvas, content, _options.ModulesCenter ?? [], WorkspaceBarSectionAlignment.Center);
                return;
            }

            DrawSections(canvas, content, _sectionsLeft, WorkspaceBarSectionAlignment.Left);
            DrawSections(canvas, content, _sectionsRight, WorkspaceBarSectionAlignment.Right);
            DrawSections(canvas, content, _sectionsCenter, WorkspaceBarSectionAlignment.Center);
        }

        private SKRect MakePrimaryRect(SKRect content, float start, float length)
            => _isVertical
                ? new SKRect(content.Left, start, content.Right, start + length)
                : new SKRect(start, content.Top, start + length, content.Bottom);

        private float PrimaryStart(SKRect rect) => _isVertical ? rect.Top : rect.Left;

        private float PrimaryEnd(SKRect rect) => _isVertical ? rect.Bottom : rect.Right;

        private float PrimarySize(SKRect rect) => _isVertical ? rect.Height : rect.Width;

        private void DrawModuleGroup(SKCanvas canvas, SKRect content, IReadOnlyList<WorkspaceBarModuleOptions> modules, WorkspaceBarSectionAlignment alignment)
        {
            _visibleModules.Clear();
            _moduleWidths.Clear();
            var totalLength = 0F;
            foreach (var module in modules)
            {
                if (!module.Style.Visible || !module.IsShownOn(_isPrimary, _isFocused))
                {
                    continue;
                }

                var moduleLength = MeasureModule(module);
                _visibleModules.Add(module);
                _moduleWidths.Add(moduleLength);
                totalLength += moduleLength;
            }

            totalLength += Math.Max(0, _moduleWidths.Count - 1) * _barStyle.Spacing;
            var start = alignment switch
            {
                WorkspaceBarSectionAlignment.Right => PrimaryEnd(content) - totalLength,
                WorkspaceBarSectionAlignment.Center => PrimaryStart(content) + Math.Max(0, (PrimarySize(content) - totalLength) / 2F),
                _ => PrimaryStart(content)
            };

            for (var index = 0; index < _visibleModules.Count; index++)
            {
                var module = _visibleModules[index];
                var length = Math.Min(_moduleWidths[index], Math.Max(0, PrimaryEnd(content) - start));
                var rect = MakePrimaryRect(content, start, length);
                DrawModule(canvas, module, rect, alignment);
                start += length + _barStyle.Spacing;
            }
        }

        private float MeasureModule(WorkspaceBarModuleOptions module)
        {
            float length;
            if (string.Equals(module.Type, "workspaces", StringComparison.OrdinalIgnoreCase))
            {
                length = MeasureWorkspaces(module);
            }
            else if (string.Equals(module.Type, "window-title", StringComparison.OrdinalIgnoreCase) && _isVertical)
            {
                length = MeasureStyledTextHeight(VerticalizeWindowTitle(_isFocused ? _focusedWindowTitle : string.Empty, module.Style), module.Style);
            }
            else
            {
                var text = string.Equals(module.Type, "layout", StringComparison.OrdinalIgnoreCase)
                    ? GetLayoutText(module)
                    : GetModuleText(module);
                length = _isVertical ? MeasureTextHeight(text, module.Style) : MeasureText(text, module.Style);
            }

            length += _isVertical
                ? module.Style.Padding.Top + module.Style.Padding.Bottom + module.Style.Margin.Top + module.Style.Margin.Bottom
                : module.Style.Padding.Left + module.Style.Padding.Right + module.Style.Margin.Left + module.Style.Margin.Right;
            length += module.Style.BorderWidth * 2;
            if (module.Style.MinWidth is int minWidth)
            {
                length = Math.Max(length, minWidth);
            }

            return module.Style.MaxWidth is int maxWidth ? Math.Min(length, maxWidth) : length;
        }

        private void DrawModule(SKCanvas canvas, WorkspaceBarModuleOptions module, SKRect rect, WorkspaceBarSectionAlignment alignment)
        {
            var adjusted = _isVertical
                ? new SKRect(rect.Left, rect.Top + module.Style.Margin.Top, rect.Right, rect.Bottom - module.Style.Margin.Bottom)
                : new SKRect(rect.Left + module.Style.Margin.Left, rect.Top, rect.Right - module.Style.Margin.Right, rect.Bottom);
            DrawBox(canvas, adjusted, module.Style);
            var content = Inset(adjusted, module.Style.Padding, module.Style.BorderWidth);
            switch (module.Type.ToLowerInvariant())
            {
                case "workspaces":
                    DrawWorkspaces(canvas, content, module);
                    break;
                case "layout":
                    DrawText(canvas, GetLayoutText(module), content, module.Style.Foreground, module.Style, SKTextAlign.Center);
                    break;
                case "window-title":
                    if (_isFocused)
                    {
                        if (_isVertical)
                        {
                            DrawStyledText(canvas, VerticalizeWindowTitle(_focusedWindowTitle, module.Style), content, module.Style.Foreground, module.Style, SKTextAlign.Center);
                        }
                        else
                        {
                            DrawText(canvas, GetModuleText(module), content, module.Style.Foreground, module.Style, alignment == WorkspaceBarSectionAlignment.Right ? SKTextAlign.Right : SKTextAlign.Left, ellipsis: true);
                        }
                    }
                    break;
                default:
                    DrawText(canvas, GetModuleText(module), content, module.Style.Foreground, module.Style, _isVertical ? SKTextAlign.Center : SKTextAlign.Left);
                    break;
            }
        }

        private void DrawWorkspaces(SKCanvas canvas, SKRect rect, WorkspaceBarModuleOptions module)
        {
            var labels = module.Labels ?? [];
            var spacing = Math.Max(0, module.Style.Spacing);
            var cursor = PrimaryStart(rect);
            for (var index = 0; index < WorkspaceCount; index++)
            {
                var text = labels.Count > index ? labels[index] : (index + 1).ToString();
                var itemLength = (_isVertical ? LineHeight(module.Style) : MeasureText(text, module.Style)) + 8;
                var itemRect = MakePrimaryRect(rect, cursor, itemLength);
                var active = index == _currentWorkspace;
                DrawFill(canvas, itemRect, active ? module.ActiveBackground ?? module.Style.Background : module.Style.Background, module.Style.BorderRadius);
                DrawText(canvas, text, itemRect, active ? module.ActiveForeground ?? module.Style.Foreground : module.Style.Foreground, module.Style, SKTextAlign.Center);
                if (!string.IsNullOrEmpty(module.OnClick))
                {
                    _clickTargets.Add((itemRect, module.OnClick, index));
                }

                cursor = PrimaryStart(itemRect) + itemLength + spacing;
            }
        }

        private float MeasureWorkspaces(WorkspaceBarModuleOptions module)
        {
            var labels = module.Labels ?? [];
            var spacing = Math.Max(0, module.Style.Spacing);
            var total = 0F;
            for (var index = 0; index < WorkspaceCount; index++)
            {
                var text = labels.Count > index ? labels[index] : (index + 1).ToString();
                total += (_isVertical ? LineHeight(module.Style) : MeasureText(text, module.Style)) + 8 + spacing;
            }

            return total;
        }

        private string GetModuleText(WorkspaceBarModuleOptions module)
            => _moduleTextCache.TryGetValue(module.Id, out var cached) ? cached : ResolveModuleText(module);

        private string ResolveModuleText(WorkspaceBarModuleOptions module)
        {
            if (string.Equals(module.Type, "window-title", StringComparison.OrdinalIgnoreCase))
            {
                return _isFocused ? TruncateTitle(_focusedWindowTitle, module.Style.MaxLength) : string.Empty;
            }

            if (!_moduleBindings.TryGetValue(module.Id, out var binding))
            {
                return string.Empty;
            }

            var provider = GetOrCreateProvider(binding.Index, binding.Widget);
            var values = provider?.GetValues() ?? new Dictionary<string, string>();
            var text = WorkspaceBarFormat.Format(module.Type, module.Format, values);
            _moduleTextCache[module.Id] = text;
            return text;
        }

        private string GetWidgetText(WorkspaceBarWidgetOptions widget)
            => _widgetTextCache.TryGetValue(widget.Id, out var cached) ? cached : ResolveWidgetText(widget);

        private string ResolveWidgetText(WorkspaceBarWidgetOptions widget)
        {
            var text = GetOrCreateProvider(GetWidgetIndex(widget), widget)?.GetText() ?? string.Empty;
            _widgetTextCache[widget.Id] = text;
            return text;
        }

        private string GetLayoutText(WorkspaceBarModuleOptions module)
            => module.Symbols?.TryGetValue(_layoutMode, out var symbol) == true ? symbol : FormatLayoutMode(_layoutMode);

        private void DrawSections(SKCanvas canvas, SKRect content, IReadOnlyList<WorkspaceBarSectionOptions> sections, WorkspaceBarSectionAlignment alignment)
        {
            _sectionWidths.Clear();
            var totalLength = 0F;
            foreach (var section in sections)
            {
                var sectionLength = MeasureSection(section);
                _sectionWidths.Add(sectionLength);
                totalLength += sectionLength;
            }

            totalLength += Math.Max(0, _sectionWidths.Count - 1) * _barStyle.Spacing;
            var start = alignment switch
            {
                WorkspaceBarSectionAlignment.Right => PrimaryEnd(content) - totalLength,
                WorkspaceBarSectionAlignment.Center => PrimaryStart(content) + Math.Max(0, (PrimarySize(content) - totalLength) / 2F),
                _ => PrimaryStart(content)
            };

            for (var index = 0; index < sections.Count; index++)
            {
                var length = Math.Min(_sectionWidths[index], PrimarySize(content));
                var rect = MakePrimaryRect(content, start, length);
                DrawSection(canvas, sections[index], rect);
                start += length + _barStyle.Spacing;
            }
        }

        private float MeasureSection(WorkspaceBarSectionOptions section)
        {
            float contentLength;
            if (string.Equals(section.Id, "workspaces", StringComparison.OrdinalIgnoreCase))
            {
                contentLength = MeasureWorkspaces(section.Style);
            }
            else if (string.Equals(section.Id, "layout", StringComparison.OrdinalIgnoreCase))
            {
                contentLength = _isVertical ? MeasureTextHeight(GetLayoutText(), section.Style) : MeasureText(GetLayoutText(), section.Style);
            }
            else if (string.Equals(section.Id, "title", StringComparison.OrdinalIgnoreCase))
            {
                contentLength = _isVertical
                    ? MeasureStyledTextHeight(VerticalizeWindowTitle(_focusedWindowTitle, section.Style), section.Style)
                    : MeasureText(_focusedWindowTitle, section.Style);
            }
            else
            {
                contentLength = MeasureWidgets(section, section.Style);
            }

            var length = contentLength + (section.Style.BorderWidth * 2);
            length += _isVertical
                ? section.Style.Padding.Top + section.Style.Padding.Bottom
                : section.Style.Padding.Left + section.Style.Padding.Right;
            if (section.Style.MinWidth is int minWidth)
            {
                length = Math.Max(length, minWidth);
            }

            if (section.Style.MaxWidth is int maxWidth)
            {
                length = Math.Min(length, maxWidth);
            }

            return length;
        }

        private void DrawSection(SKCanvas canvas, WorkspaceBarSectionOptions section, SKRect rect)
        {
            DrawBox(canvas, rect, section.Style);
            var content = Inset(rect, section.Style.Padding, section.Style.BorderWidth);
            switch (section.Id.ToLowerInvariant())
            {
                case "workspaces":
                    DrawWorkspaces(canvas, content, section.Style);
                    break;
                case "layout":
                    DrawText(canvas, GetLayoutText(), content, _options.Layout.Foreground, section.Style, SKTextAlign.Center);
                    break;
                case "title":
                    if (_isFocused)
                    {
                        if (_isVertical)
                        {
                            DrawStyledText(canvas, VerticalizeWindowTitle(_focusedWindowTitle, section.Style), content, _options.Title.CurrentForeground, section.Style, SKTextAlign.Center);
                        }
                        else
                        {
                            DrawText(canvas, _focusedWindowTitle, content, _options.Title.CurrentForeground, section.Style, SKTextAlign.Center, ellipsis: true);
                        }
                    }
                    break;
                default:
                    DrawWidgets(canvas, content, section, section.Style);
                    break;
            }
        }

        private void DrawWorkspaces(SKCanvas canvas, SKRect rect, WorkspaceBarStyleOptions style)
        {
            var cursor = PrimaryStart(rect);
            for (var index = 0; index < WorkspaceCount; index++)
            {
                var text = _options.Workspaces.Symbols.Count > index ? _options.Workspaces.Symbols[index] : (index + 1).ToString();
                var itemLength = (_isVertical ? LineHeight(style) : MeasureText(text, style)) + 8;
                var itemRect = MakePrimaryRect(rect, cursor, itemLength);
                var active = index == _currentWorkspace;
                DrawFill(canvas, itemRect, active ? _options.Workspaces.CurrentBackground : _options.Workspaces.Background, 0);
                DrawText(canvas, text, itemRect, active ? _options.Workspaces.CurrentForeground : _options.Workspaces.Foreground, style, SKTextAlign.Center);
                cursor = PrimaryStart(itemRect) + itemLength + 4;
            }
        }

        private void DrawWidgets(SKCanvas canvas, SKRect rect, WorkspaceBarSectionOptions section, WorkspaceBarStyleOptions sectionStyle)
        {
            if (!_isFocused)
            {
                return;
            }

            var widgets = GetSectionWidgets(section);
            var widgetLengths = widgets.Select(widget => MeasureWidget(widget, sectionStyle)).ToList();
            var start = section.Alignment == WorkspaceBarSectionAlignment.Right
                ? PrimaryEnd(rect) - widgetLengths.Sum() - Math.Max(0, widgetLengths.Count - 1) * _barStyle.Spacing
                : PrimaryStart(rect);
            for (var index = 0; index < widgets.Count; index++)
            {
                var widget = widgets[index];
                var style = widget.Style ?? sectionStyle;
                var text = GetWidgetText(widget);
                var symbolWidth = string.IsNullOrEmpty(widget.Symbol) ? 0 : MeasureText(widget.Symbol, style) + 6;
                var length = widgetLengths[index];
                var widgetRect = MakePrimaryRect(rect, start, length);
                DrawFill(canvas, widgetRect, widget.ResultBackground, style.BorderRadius);
                var content = Inset(widgetRect, style.Padding, style.BorderWidth);
                if (!string.IsNullOrEmpty(widget.Symbol))
                {
                    DrawText(canvas, widget.Symbol, new SKRect(content.Left, content.Top, content.Left + symbolWidth, content.Bottom), widget.SymbolForeground, style, SKTextAlign.Left);
                }

                DrawText(canvas, text, new SKRect(content.Left + symbolWidth, content.Top, content.Right, content.Bottom), widget.ResultForeground, style, SKTextAlign.Left);
                start += length + _barStyle.Spacing;
            }
        }

        private IReadOnlyList<WorkspaceBarWidgetOptions> GetSectionWidgets(WorkspaceBarSectionOptions section)
            => _sectionWidgets.TryGetValue(section.Id, out var widgets) ? widgets : _options.Widgets;

        private float MeasureWorkspaces(WorkspaceBarStyleOptions style)
        {
            var symbols = _options.Workspaces.Symbols;
            var total = 0F;
            for (var index = 0; index < WorkspaceCount; index++)
            {
                var text = symbols.Count > index ? symbols[index] : (index + 1).ToString();
                total += (_isVertical ? LineHeight(style) : MeasureText(text, style)) + 12;
            }

            return total;
        }

        private float MeasureWidgets(WorkspaceBarSectionOptions section, WorkspaceBarStyleOptions sectionStyle)
        {
            var widgets = GetSectionWidgets(section);
            var total = 0F;
            for (var index = 0; index < widgets.Count; index++)
            {
                total += MeasureWidget(widgets[index], sectionStyle) + _barStyle.Spacing;
            }

            return total;
        }

        private float MeasureWidget(WorkspaceBarWidgetOptions widget, WorkspaceBarStyleOptions sectionStyle)
        {
            var style = widget.Style ?? sectionStyle;
            if (_isVertical)
            {
                return LineHeight(style) + style.Padding.Top + style.Padding.Bottom + 6;
            }

            var text = GetWidgetText(widget);
            return MeasureText(widget.Symbol, style) + MeasureText(text, style) + style.Padding.Left + style.Padding.Right + 6;
        }

        private string GetLayoutText()
            => _options.Layout.Symbols.TryGetValue(_layoutMode, out var symbol) ? symbol : FormatLayoutMode(_layoutMode);

        private static void DrawBox(SKCanvas canvas, SKRect rect, WorkspaceBarStyleOptions style)
        {
            DrawFill(canvas, rect, style.Background, style.BorderRadius);
            if (style.BorderWidth <= 0)
            {
                return;
            }

            using var paint = new SKPaint { IsAntialias = true, Color = ToSkColor(style.BorderColor), Style = SKPaintStyle.Stroke, StrokeWidth = style.BorderWidth };
            var inset = style.BorderWidth / 2F;
            using var path = CreateRoundedPath(new SKRect(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset), style.BorderRadius);
            canvas.DrawPath(path, paint);
        }

        private static void DrawFill(SKCanvas canvas, SKRect rect, Color color, int radius)
        {
            using var paint = new SKPaint { IsAntialias = true, Color = ToSkColor(color), Style = SKPaintStyle.Fill };
            using var path = CreateRoundedPath(rect, radius);
            canvas.DrawPath(path, paint);
        }

        private static void DrawText(SKCanvas canvas, string text, SKRect rect, Color color, WorkspaceBarStyleOptions style, SKTextAlign align, bool ellipsis = false)
        {
            if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            var styled = InlineStyleParser.Parse(text);
            DrawStyledText(canvas, styled, rect, color, style, align, ellipsis);
        }

        private static void DrawStyledText(SKCanvas canvas, StyledText styled, SKRect rect, Color defaultColor, WorkspaceBarStyleOptions style, SKTextAlign align, bool ellipsis = false)
        {
            if (styled.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            var targetStyled = ellipsis ? Ellipsize(styled, rect.Width, style, defaultColor) : styled;
            var lineHeight = LineHeight(style);
            var blockHeight = targetStyled.Lines.Count * lineHeight;
            var blockTop = rect.MidY - (blockHeight / 2F);

            for (var lineIndex = 0; lineIndex < targetStyled.Lines.Count; lineIndex++)
            {
                var line = targetStyled.Lines[lineIndex];
                var lineWidth = MeasureLineWidth(line, defaultColor, style);
                var startX = align switch
                {
                    SKTextAlign.Center => rect.MidX - (lineWidth / 2F),
                    SKTextAlign.Right => rect.Right - lineWidth,
                    _ => rect.Left
                };

                var lineMidY = blockTop + (lineIndex * lineHeight) + (lineHeight / 2F);
                var currentX = startX;
                foreach (var span in line)
                {
                    if (string.IsNullOrEmpty(span.Text))
                    {
                        continue;
                    }

                    var paint = GetSpanPaint(span, defaultColor, style, SKTextAlign.Left);
                    var spanWidth = paint.MeasureText(span.Text);
                    var metrics = paint.FontMetrics;
                    var y = lineMidY - ((metrics.Ascent + metrics.Descent) / 2F);

                    canvas.DrawText(span.Text, currentX, y, paint);
                    currentX += spanWidth;
                }
            }
        }

        private static float MeasureText(string text, WorkspaceBarStyleOptions style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var styled = InlineStyleParser.Parse(text);
            return MeasureStyledText(styled, style.Foreground, style);
        }

        private static float MeasureTextHeight(string text, WorkspaceBarStyleOptions style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return MeasureStyledTextHeight(InlineStyleParser.Parse(text), style);
        }

        private static float MeasureStyledTextHeight(StyledText styled, WorkspaceBarStyleOptions style)
            => styled.IsEmpty ? 0 : styled.Lines.Count * LineHeight(style);

        private static float LineHeight(WorkspaceBarStyleOptions style)
        {
            var paint = GetSpanPaint(new StyledSpan(string.Empty), style.Foreground, style);
            var metrics = paint.FontMetrics;
            return metrics.Descent - metrics.Ascent;
        }

        private static StyledText VerticalizeWindowTitle(string text, WorkspaceBarStyleOptions style)
        {
            if (string.IsNullOrEmpty(text))
            {
                return StyledText.Empty;
            }

            var maxChars = style.MaxLength is int max && max > 0 ? max : 24;
            var truncated = TruncateTitle(text, maxChars);
            var lines = truncated.Select(ch => (IReadOnlyList<StyledSpan>)new[] { new StyledSpan(ch.ToString()) }).ToList();
            return new StyledText(lines);
        }

        private static string TruncateTitle(string text, int? maxLength)
        {
            if (maxLength is not int max || max <= 0 || text.Length <= max)
            {
                return text;
            }

            return text[..max] + "...";
        }

        private static float MeasureLineWidth(IReadOnlyList<StyledSpan> line, Color defaultColor, WorkspaceBarStyleOptions style)
        {
            var width = 0F;
            foreach (var span in line)
            {
                if (string.IsNullOrEmpty(span.Text))
                {
                    continue;
                }

                var paint = GetSpanPaint(span, defaultColor, style, SKTextAlign.Left);
                width += paint.MeasureText(span.Text);
            }

            return width;
        }

        private static float MeasureStyledText(StyledText styled, Color defaultColor, WorkspaceBarStyleOptions style)
        {
            if (styled.IsEmpty)
            {
                return 0;
            }

            var totalWidth = 0F;
            foreach (var line in styled.Lines)
            {
                totalWidth = Math.Max(totalWidth, MeasureLineWidth(line, defaultColor, style));
            }

            return totalWidth;
        }

        private readonly record struct TextPaintKey(
            string FontFamily,
            float TextSize,
            SKFontStyleWeight Weight,
            SKFontStyleSlant Slant,
            uint Color,
            SKTextAlign Align);

        // Returns a shared, never-disposed paint: constructing one (and resolving its typeface) per span
        // per frame was the dominant cost of the bar's redraw loop.
        private static SKPaint GetSpanPaint(StyledSpan span, Color defaultColor, WorkspaceBarStyleOptions style, SKTextAlign align = SKTextAlign.Left)
        {
            var color = ToSkColor(span.Foreground ?? defaultColor);
            var textSize = (span.FontSize ?? style.FontSize) * 96F / 72F;
            var fontFamily = span.FontFamily ?? style.FontFamily;
            var weight = span.FontWeight ?? (style.FontStyle.HasFlag(FontStyle.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal);
            var slant = span.FontSlant ?? (style.FontStyle.HasFlag(FontStyle.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            var key = new TextPaintKey(fontFamily, textSize, weight, slant, (uint)color, align);
            if (TextPaints.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var paint = new SKPaint
            {
                IsAntialias = true,
                SubpixelText = true,
                Color = color,
                TextSize = textSize,
                Typeface = GetTypeface(fontFamily, weight, slant),
                TextAlign = align
            };
            TextPaints[key] = paint;
            return paint;
        }

        private static SKTypeface GetTypeface(string fontFamily, SKFontStyleWeight weight, SKFontStyleSlant slant)
        {
            var key = (fontFamily, weight, slant);
            if (Typefaces.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var typeface = SKTypeface.FromFamilyName(fontFamily, weight, SKFontStyleWidth.Normal, slant);
            Typefaces[key] = typeface;
            return typeface;
        }

        private static StyledText Ellipsize(StyledText styled, float maxWidth, WorkspaceBarStyleOptions style, Color defaultColor)
        {
            var lines = styled.Lines
                .Select(line => MeasureLineWidth(line, defaultColor, style) <= maxWidth ? line : EllipsizeLine(line, maxWidth, style, defaultColor))
                .ToList();
            return new StyledText(lines);
        }

        private static IReadOnlyList<StyledSpan> EllipsizeLine(IReadOnlyList<StyledSpan> spans, float maxWidth, WorkspaceBarStyleOptions style, Color defaultColor)
        {
            const string suffix = "...";
            if (spans.Count == 1)
            {
                var singleSpan = spans[0];
                var paint = GetSpanPaint(singleSpan, defaultColor, style, SKTextAlign.Left);
                return [singleSpan with { Text = Ellipsize(singleSpan.Text, maxWidth, paint) }];
            }

            var resultSpans = new List<StyledSpan>();
            var currentWidth = 0F;

            for (var i = 0; i < spans.Count; i++)
            {
                var span = spans[i];
                if (string.IsNullOrEmpty(span.Text))
                {
                    continue;
                }

                var paint = GetSpanPaint(span, defaultColor, style, SKTextAlign.Left);
                var spanWidth = paint.MeasureText(span.Text);

                if (currentWidth + spanWidth <= maxWidth)
                {
                    resultSpans.Add(span);
                    currentWidth += spanWidth;
                }
                else
                {
                    var text = span.Text;
                    var added = false;
                    for (var length = text.Length - 1; length > 0; length--)
                    {
                        var candidate = text[..length] + suffix;
                        if (currentWidth + paint.MeasureText(candidate) <= maxWidth)
                        {
                            resultSpans.Add(span with { Text = candidate });
                            added = true;
                            break;
                        }
                    }

                    if (!added)
                    {
                        if (resultSpans.Count == 0)
                        {
                            resultSpans.Add(span with { Text = suffix });
                        }
                        else
                        {
                            var lastIndex = resultSpans.Count - 1;
                            var lastSpan = resultSpans[lastIndex];
                            var lastPaint = GetSpanPaint(lastSpan, defaultColor, style, SKTextAlign.Left);
                            var available = maxWidth - (currentWidth - lastPaint.MeasureText(lastSpan.Text));
                            resultSpans[lastIndex] = lastSpan with { Text = Ellipsize(lastSpan.Text, available, lastPaint) };
                        }
                    }

                    break;
                }
            }

            return resultSpans;
        }

        private static string Ellipsize(string text, float maxWidth, SKPaint paint)
        {
            const string suffix = "...";
            if (paint.MeasureText(text) <= maxWidth)
            {
                return text;
            }

            for (var length = text.Length - 1; length > 0; length--)
            {
                var candidate = text[..length] + suffix;
                if (paint.MeasureText(candidate) <= maxWidth)
                {
                    return candidate;
                }
            }

            return suffix;
        }

        private static SKRect Inset(SKRect rect, Padding padding, int borderWidth)
            => new(
                rect.Left + padding.Left + borderWidth,
                rect.Top + padding.Top + borderWidth,
                rect.Right - padding.Right - borderWidth,
                rect.Bottom - padding.Bottom - borderWidth);

        private static SKPath CreateRoundedPath(SKRect rect, int radius)
        {
            var path = new SKPath();
            if (radius <= 0)
            {
                path.AddRect(rect);
                return path;
            }

            path.AddRoundRect(rect, radius, radius);
            return path;
        }

        private static SKColor ToSkColor(Color color)
            => new(color.R, color.G, color.B, color.A);

        private IWidgetProvider? GetOrCreateProvider(int index, WorkspaceBarWidgetOptions widget)
        {
            var signature = $"{widget.Kind}\n{widget.Symbol}\n{widget.BatterySymbols?.Count}\n{widget.Text}\n{widget.Command}\n{widget.IntervalMilliseconds}";
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

        private int GetWidgetIndex(WorkspaceBarWidgetOptions widget)
            => _widgetIndexes.TryGetValue(widget.Id, out var index) ? index : 0;

        private static string FormatLayoutMode(MasterStackLayoutMode mode)
        {
            return mode switch
            {
                MasterStackLayoutMode.MasterLeft => "ML",
                MasterStackLayoutMode.MasterTop => "MT",
                MasterStackLayoutMode.Monocle => "MON",
                MasterStackLayoutMode.Floating => "FLT",
                MasterStackLayoutMode.Grid => "GRD",
                MasterStackLayoutMode.Fibonacci => "FIB",
                MasterStackLayoutMode.Dwindle => "DWL",
                MasterStackLayoutMode.CenteredMaster => "CTR",
                _ => "?"
            };
        }

        public void Dispose()
        {
            foreach (var (_, provider) in _widgetProviders)
            {
                (provider as IDisposable)?.Dispose();
            }

            _renderBitmap?.Dispose();
            _form.Dispose();
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
