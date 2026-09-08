using System.ComponentModel;
using System.Text.Json;
using Microsoft.Win32;
using RootlessWM.Domain;
using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class WmApplication
{
    private const double MasterRatioStep = 0.05;
    private const int MasterCountStep = 1;
    private const int GapStep = 2;
    private const int MaxEligibilityRecheckAttempts = 8;
    private static readonly TimeSpan EligibilityRecheckDelay = TimeSpan.FromMilliseconds(250);
    private static readonly MasterStackLayoutOptions FullscreenLayoutOptions =
        new(0.5, 0, 0, MasterStackLayoutMode.Monocle);

    private ConsoleDiagnosticLog _log = new();
    private readonly WindowEnumerator _windowEnumerator = new();
    private readonly WindowInspector _windowInspector = new();
    private readonly WindowEligibilityOptions _eligibilityOptions = new();
    private readonly WindowEligibilityClassifier _eligibilityClassifier;
    private readonly WindowTracker _windowTracker;
    private readonly TilingState _tilingState = new();
    private readonly MonitorCatalog _monitorCatalog = new();
    private readonly MonitorOwnership _monitorOwnership = new();
    private readonly WindowTiler _windowTiler;
    private readonly TilingCommandProcessor _commandProcessor;
    private readonly MonitorCommandProcessor _monitorCommandProcessor;
    private readonly WorkspaceState _workspaceState = new();
    private readonly FullscreenState _fullscreenState = new();
    private readonly WorkspaceCommandProcessor _workspaceCommandProcessor;
    private readonly WorkspaceVisibilitySynchronizer _workspaceVisibilitySynchronizer;
    private readonly Win32WindowCommander _windowCommander = new();
    private readonly Win32CursorController _cursorController = new();
    private readonly ExplorerVisibilityController _explorerVisibility = new();
    private readonly MouseFocusController _mouseFocusController;
    private readonly ManagementState _managementState = new();
    private readonly IManagedWindowStateStore _windowStateStore = new JsonManagedWindowStateStore();
    private readonly JsonWorkspaceStateStore _workspaceStateStore = new();
    private readonly WindowRestorer _windowRestorer = new(new GuardedWindowBoundsRestorer());
    private readonly TomlSettingsProvider _settingsProvider = new();
    private MasterStackLayoutOptions _layoutOptions = MasterStackLayoutOptions.Default;
    private readonly LayoutState _layoutState = new();
    private readonly Dictionary<nint, int> _eligibilityRecheckAttempts = [];
    private readonly HashSet<nint> _workspaceHiddenHandles = [];
    private readonly HashSet<nint> _topmostHandles = [];
    private Action<nint>? _scheduleEligibilityRecheck;
    private RootlessWMSettings _settings = RootlessWMSettings.Default;
    private RunnerController? _runnerController;
    private WorkspaceBarController? _workspaceBar;
    private bool _sessionLocked;

    public WmApplication()
    {
        _eligibilityClassifier = new WindowEligibilityClassifier(_eligibilityOptions);
        _windowTracker = new WindowTracker(_eligibilityClassifier);
        _windowTiler = new WindowTiler(
            new MasterStackLayout(),
            new GuardedWindowPlacementApplier(_windowInspector, _eligibilityClassifier));
        _commandProcessor = new TilingCommandProcessor(_tilingState, _windowCommander);
        _monitorCommandProcessor = new MonitorCommandProcessor(
            _tilingState,
            _monitorOwnership,
            _windowCommander);
        _workspaceCommandProcessor = new WorkspaceCommandProcessor(_workspaceState);
        _workspaceVisibilitySynchronizer = new WorkspaceVisibilitySynchronizer(
            _workspaceState,
            _monitorOwnership,
            _windowCommander);
        _mouseFocusController = new MouseFocusController(
            _windowCommander,
            CanFocusWithMouse,
            NativeMethods.GetForegroundWindow);
    }

    public int Run(string[] args)
    {
        if (args.Contains("--no-logs", StringComparer.OrdinalIgnoreCase))
        {
            _log = new ConsoleDiagnosticLog(writeToFile: false);
        }

        if (!OperatingSystem.IsWindows())
        {
            _log.Error("unsupported_platform", new { required = "Windows" });
            return 1;
        }

        _log.Info("application_started", new { mode = "foundation" });
        LoadSettings();

        if (args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase))
        {
            var trackedWindows = RefreshTrackedWindows();
            var monitors = _monitorCatalog.GetWorkAreas()
                .Select(monitor => new
                {
                    handle = $"0x{monitor.Handle.ToInt64():X}",
                    monitor.IsPrimary,
                    monitor.Bounds
                })
                .ToArray();
            var classifications = trackedWindows
                .Select(window => new
                {
                    handle = $"0x{window.Handle.ToInt64():X}",
                    window.Candidate.ClassName,
                    window.Candidate.ProcessName,
                    eligibility = window.Eligibility.ToString()
                })
                .ToArray();

            _log.Info("top_level_windows_classified", new
            {
                count = classifications.Length,
                managedCandidateCount = classifications.Count(window => window.eligibility == nameof(WindowEligibility.Managed)),
                monitors,
                classifications
            });
        }

        if (args.Contains("--watch", StringComparer.OrdinalIgnoreCase))
        {
            RunWatchMode();
        }

        if (args.Contains("--tile", StringComparer.OrdinalIgnoreCase))
        {
            RunTileMode();
        }

        if (args.Contains("--untile", StringComparer.OrdinalIgnoreCase))
        {
            RunUntileMode();
        }

        if (args.Contains("--manage", StringComparer.OrdinalIgnoreCase))
        {
            RunManageMode();
        }

        _log.Info("application_stopped", new { reason = "host_completed" });
        return 0;
    }

    private void RunTileMode()
    {
        if (!TryLoadManagedWindowStates(out var pendingStates, "tiling"))
        {
            return;
        }

        if (pendingStates.Count > 0)
        {
            _log.Error("tiling_blocked", new { reason = "pending_restore_exists", command = "--untile" });
            return;
        }

        var trackedWindows = RefreshTrackedWindows();
        var tiledHandles = GetEligibleTiledHandles();
        var originalBoundsByHandle = trackedWindows.ToDictionary(window => window.Handle, window => window.OriginalBounds);
        var managedWindowStates = tiledHandles
            .Select(handle => new ManagedWindowState(handle.ToInt64(), originalBoundsByHandle[handle]))
            .ToArray();

        if (managedWindowStates.Length == 0)
        {
            _log.Info("tiling_skipped", new { reason = "no_eligible_windows" });
            return;
        }

        _windowStateStore.Save(managedWindowStates);
        var operations = TileAllMonitors(tiledHandles);
        _log.Info("tiling_completed", new
        {
            monitorCount = operations.Count,
            plannedWindowCount = operations.Sum(operation => operation.PlannedPlacements.Count),
            appliedWindowCount = operations.Sum(operation => operation.PlacementResults.Count(result => result.Applied)),
            skipped = operations
                .SelectMany(operation => operation.PlacementResults)
                .Where(result => !result.Applied)
                .Select(result => new { handle = $"0x{result.Handle.ToInt64():X}", result.Reason })
                .ToArray()
        });
    }

    private void RunUntileMode()
    {
        if (!TryLoadManagedWindowStates(out var managedWindowStates, "restore"))
        {
            return;
        }

        if (managedWindowStates.Count == 0)
        {
            _log.Info("restore_skipped", new { reason = "no_pending_restore" });
            return;
        }

        var results = _windowRestorer.Restore(managedWindowStates);
        var pendingStates = managedWindowStates
            .Where((_, index) => RestoreResultPolicy.RetainsForRetry(results[index]))
            .ToArray();

        if (pendingStates.Length == 0)
        {
            _windowStateStore.Clear();
        }
        else
        {
            _windowStateStore.Save(pendingStates);
        }

        _log.Info("restore_completed", new
        {
            requestedWindowCount = managedWindowStates.Count,
            restoredWindowCount = results.Count(result => result.Applied),
            skipped = results
                .Where(result => !result.Applied)
                .Select(result => new { handle = $"0x{result.Handle.ToInt64():X}", result.Reason })
                .ToArray()
        });
    }

    private void RunWatchMode()
    {
        RefreshTrackedWindows();
        using var eventSource = new WindowEventSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            eventSource.Stop();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            _scheduleEligibilityRecheck = handle => eventSource.ScheduleRecheck(handle, EligibilityRecheckDelay);
            eventSource.Start(HandleWindowEvent);
            _log.Info("window_watcher_started", new { trackedWindowCount = _windowTracker.Snapshot().Count });
            eventSource.RunMessageLoop();
        }
        finally
        {
            _scheduleEligibilityRecheck = null;
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private void RunManageMode()
    {
        if (!TryLoadManagedWindowStates(out var pendingStates, "management"))
        {
            return;
        }

        if (pendingStates.Count > 0)
        {
            // A prior session likely ended without a clean exit (for example, a reboot); recover it automatically.
            _log.Info("management_recovering", new { reason = "pending_restore_exists", pendingWindowCount = pendingStates.Count });
            RunUntileMode();
            if (!TryLoadManagedWindowStates(out pendingStates, "management"))
            {
                return;
            }

            if (pendingStates.Count > 0)
            {
                _log.Error("management_blocked", new { reason = "pending_restore_exists", command = "--untile" });
                return;
            }
        }

        var trackedWindows = RefreshTrackedWindows();
        LoadWorkspaceState(trackedWindows.Select(window => window.Handle));
        RunTileMode();
        _ = _managementState.Enable();
        if (_settings.HideExplorerOnStart)
        {
            _explorerVisibility.Toggle(_settings.ToggleExplorerBehaviour);
        }
        ApplyWorkspaceVisibility();
        SaveWorkspaceState();
        using var eventSource = new WindowEventSource();
        using var mouseFocusSource = new MouseFocusSource();
        GlobalHotkeySource? hotkeySource = null;
        TrayController? statusController = null;
        using var workspaceBar = new WorkspaceBarController(
            _workspaceState.WorkspaceCount,
            GetFocusedMonitorHandle,
            GetFocusedWindowTitle,
            _log);
        _workspaceBar = workspaceBar;
        using var runnerController = new RunnerController(_log);
        _runnerController = runnerController;
        using var trayController = new TrayController(
            ToggleManagement,
            ReloadSettingsAndHotkeys,
            eventSource.Stop,
            command =>
            {
                ExecuteTilingCommand(command);
                UpdateStatus(statusController);
            },
            () => _managementState.IsEnabled);
        statusController = trayController;
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            eventSource.Stop();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            _scheduleEligibilityRecheck = handle => eventSource.ScheduleRecheck(handle, EligibilityRecheckDelay);
            eventSource.Start(HandleWindowEvent);
            try
            {
                mouseFocusSource.Start(windowHandle => _ = _mouseFocusController.TryFocus(windowHandle));
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                _log.Error("mouse_focus_unavailable", new { exception = exception.NativeErrorCode });
            }
            ReloadSettingsAndHotkeys();
            trayController.SetManagementEnabled(_managementState.IsEnabled);
            UpdateStatus(statusController);
            _log.Info("management_started", new { hotkeyModifier = "Alt+Shift" });
            SystemEvents.SessionSwitch += OnSessionSwitch;
            eventSource.RunMessageLoop((messageId, hotkeyIdentifier) =>
            {
                if (hotkeySource?.TryGetCommand(messageId, hotkeyIdentifier, out var command) == true)
                {
                    ExecuteTilingCommand(command);
                    UpdateStatus(trayController);
                }
                else if (messageId is NativeMethods.WmDisplayChange or NativeMethods.WmSettingChange
                    && _managementState.IsEnabled
                    && !_sessionLocked)
                {
                    RetilePrimaryWindows();
                    UpdateStatus(trayController);
                }
            });
        }
        finally
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _scheduleEligibilityRecheck = null;
            Console.CancelKeyPress -= cancelHandler;
            hotkeySource?.Dispose();
            _runnerController = null;
            SaveWorkspaceState();
            RunUntileMode();
            _ = _managementState.Disable();
            _explorerVisibility.EnsureVisible();
        }

        void ReloadSettingsAndHotkeys()
        {
            LoadSettings();
            hotkeySource?.Dispose();
            hotkeySource = new GlobalHotkeySource();
            var unavailableCommands = hotkeySource.Start(_settings.Hotkeys);
            workspaceBar.ApplyOptions(_settings.ToWorkspaceBarOptions());
            runnerController.ApplySettings(_settings.Runner);
            if (_managementState.IsEnabled)
            {
                RetilePrimaryWindows();
            }
            _log.Info("settings_reloaded", new
            {
                unavailableCommands = unavailableCommands.Select(command => command.ToString()).ToArray()
            });
            UpdateStatus(statusController);
        }

        void UpdateStatus(TrayController? controller)
        {
            if (controller is null)
            {
                return;
            }

            var activeMonitor = GetActiveMonitorHandle(NativeMethods.GetForegroundWindow());
            var currentWorkspace = activeMonitor == nint.Zero
                ? 0
                : _workspaceState.GetCurrentWorkspace(activeMonitor);
            var statusLayoutOptions = GetActiveLayoutOptions();
            controller.SetStatus(StatusText.Format(new StatusSnapshot(
                _managementState.IsEnabled,
                currentWorkspace,
                _workspaceState.WorkspaceCount,
                statusLayoutOptions.MasterRatio,
                statusLayoutOptions.OuterGap,
                statusLayoutOptions.InnerGap,
                GetFocusScopeHandles(NativeMethods.GetForegroundWindow()).Count,
                statusLayoutOptions.MasterCount)));

            workspaceBar.Update(
                _monitorCatalog.GetWorkAreas(),
                _workspaceState.GetCurrentWorkspace,
                GetLayoutModeForMonitor);
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs eventArgs)
        {
            if (eventArgs.Reason == SessionSwitchReason.SessionLock)
            {
                _sessionLocked = true;
                workspaceBar.SetVisible(false);
                _log.Info("session_locked");
                return;
            }

            if (eventArgs.Reason != SessionSwitchReason.SessionUnlock)
            {
                return;
            }

            _sessionLocked = false;
            _log.Info("session_unlocked");
            // Do not reseed the tracker here: windows hidden for other workspaces are not
            // WS_VISIBLE, so re-inspecting them would misclassify them as NotVisible and
            // drop them from tiling/workspace state.
            ApplyWorkspaceVisibility();
            if (_managementState.IsEnabled)
            {
                RetilePrimaryWindows();
            }

            workspaceBar.SetVisible(true);
            UpdateStatus(statusController);
        }
    }

    private void ExecuteTilingCommand(TilingCommand command)
    {
        if (command == TilingCommand.DisableManagement)
        {
            DisableManagement();
            return;
        }

        if (command == TilingCommand.EnableManagement)
        {
            EnableManagement();
            return;
        }

        if (command == TilingCommand.ToggleExplorer)
        {
            // Works regardless of management state, like dwm-win32's MOD+E: Explorer keeps
            // running (tray icons stay alive), only the shell chrome is shown/hidden.
            var explorerVisible = _explorerVisibility.Toggle(_settings.ToggleExplorerBehaviour);
            if (_managementState.IsEnabled)
            {
                RetilePrimaryWindows();
            }
            _log.Info("explorer_visibility_toggled", new { visible = explorerVisible, behaviour = _settings.ToggleExplorerBehaviour.ToString() });
            return;
        }

        if (command == TilingCommand.OpenRunner)
        {
            _runnerController?.Toggle();
            _log.Info("runner_hotkey_pressed", new { source = "hotkey", visible = true });
            return;
        }

        if (!_managementState.IsEnabled)
        {
            _log.Info("tiling_command_ignored", new { command = command.ToString(), reason = "management_disabled" });
            return;
        }

        var activeHandle = NativeMethods.GetForegroundWindow();
        if (command is TilingCommand.FocusNextMonitor
            or TilingCommand.FocusPreviousMonitor
            or TilingCommand.MoveToNextMonitor
            or TilingCommand.MoveToPreviousMonitor)
        {
            var monitorHandles = _monitorCatalog.GetWorkAreas().Select(monitor => monitor.Handle).ToArray();
            var hasMonitorFocusTarget = _monitorCommandProcessor.TryGetFocusTarget(
                command,
                activeHandle,
                monitorHandles,
                out var monitorFocusTargetHandle);
            var executedOnMonitor = _monitorCommandProcessor.Execute(
                command,
                activeHandle,
                monitorHandles,
                () =>
                {
                    // The moved window must land on the destination monitor's current workspace,
                    // not keep the workspace number it had on the source monitor.
                    if (command is TilingCommand.MoveToNextMonitor or TilingCommand.MoveToPreviousMonitor
                        && _monitorOwnership.TryGetMonitor(activeHandle, out var destinationMonitor))
                    {
                        _ = _workspaceState.MoveToCurrentWorkspace(activeHandle, destinationMonitor);
                        SaveWorkspaceState();
                    }

                    ApplyWorkspaceVisibility();
                    RetilePrimaryWindows();
                });
            if (executedOnMonitor
                && hasMonitorFocusTarget
                && monitorFocusTargetHandle != nint.Zero)
            {
                _ = _cursorController.CenterOn(monitorFocusTargetHandle);
            }
            _log.Info("tiling_command_executed", new
            {
                command = command.ToString(),
                handle = $"0x{activeHandle.ToInt64():X}",
                executed = executedOnMonitor
            });
            return;
        }

        if (command is TilingCommand.NextWorkspace
            or TilingCommand.PreviousWorkspace
            or TilingCommand.MoveToNextWorkspace
            or TilingCommand.MoveToPreviousWorkspace
            or >= TilingCommand.SelectWorkspace1 and <= TilingCommand.SelectWorkspace9
            or >= TilingCommand.MoveToWorkspace1 and <= TilingCommand.MoveToWorkspace9)
        {
            var workspaceMonitor = IsWorkspaceMoveCommand(command)
                ? GetActiveMonitorHandle(activeHandle)
                : _monitorCatalog.GetCursorMonitorHandle();
            if (workspaceMonitor == nint.Zero)
            {
                return;
            }
            var executedOnWorkspace = _workspaceCommandProcessor.Execute(
                command,
                activeHandle,
                workspaceMonitor,
                ApplyWorkspaceVisibility,
                RetilePrimaryWindows);
            if (executedOnWorkspace)
            {
                FocusWorkspaceWindow(workspaceMonitor);
                SaveWorkspaceState();
            }
            _log.Info("tiling_command_executed", new
            {
                command = command.ToString(),
                handle = $"0x{activeHandle.ToInt64():X}",
                workspace = _workspaceState.GetCurrentWorkspace(workspaceMonitor),
                monitor = $"0x{workspaceMonitor.ToInt64():X}",
                executed = executedOnWorkspace
            });
            return;
        }

        if (command == TilingCommand.MaximizeWindow)
        {
            var monitorHandle = GetActiveMonitorHandle(activeHandle);
            if (monitorHandle == nint.Zero || !_tilingState.TiledHandles.Contains(activeHandle))
            {
                _log.Info("fullscreen_toggle_skipped", new
                {
                    handle = $"0x{activeHandle.ToInt64():X}",
                    reason = monitorHandle == nint.Zero ? "no_monitor" : "not_tiled"
                });
                return;
            }

            var workspace = _workspaceState.GetCurrentWorkspace(monitorHandle);
            var toggled = _fullscreenState.Toggle(monitorHandle, workspace, activeHandle);
            if (toggled)
            {
                RetilePrimaryWindows();
                _ = _windowCommander.Focus(activeHandle);
            }

            _log.Info("fullscreen_toggled", new
            {
                handle = $"0x{activeHandle.ToInt64():X}",
                monitor = $"0x{monitorHandle.ToInt64():X}",
                workspace,
                fullscreen = _fullscreenState.IsFullscreen(activeHandle)
            });
            return;
        }

        if (command == TilingCommand.CycleLayout)
        {
            var monitorHandle = GetActiveMonitorHandle(activeHandle);
            if (monitorHandle == nint.Zero)
            {
                return;
            }

            var workspace = _workspaceState.GetCurrentWorkspace(monitorHandle);
            var layout = _layoutState.Cycle(workspace, monitorHandle, _layoutOptions);
            RetilePrimaryWindows();
            _log.Info("layout_cycled", new
            {
                workspace,
                monitor = $"0x{monitorHandle.ToInt64():X}",
                layout = layout.Mode.ToString()
            });
            return;
        }

        if (command is TilingCommand.DecreaseMasterRatio or TilingCommand.IncreaseMasterRatio)
        {
            var monitorHandle = GetActiveMonitorHandle(activeHandle);
            if (monitorHandle == nint.Zero)
            {
                return;
            }

            var workspace = _workspaceState.GetCurrentWorkspace(monitorHandle);
            var step = command == TilingCommand.IncreaseMasterRatio
                ? MasterRatioStep
                : -MasterRatioStep;
            var layout = _layoutState.AdjustMasterRatio(
                workspace,
                monitorHandle,
                _layoutOptions,
                step);
            RetilePrimaryWindows();
            _log.Info("master_ratio_adjusted", new
            {
                workspace,
                monitor = $"0x{monitorHandle.ToInt64():X}",
                masterRatio = layout.MasterRatio
            });
            return;
        }

        if (command is TilingCommand.DecreaseMasterCount or TilingCommand.IncreaseMasterCount)
        {
            var monitorHandle = GetActiveMonitorHandle(activeHandle);
            if (monitorHandle == nint.Zero)
            {
                return;
            }

            var workspace = _workspaceState.GetCurrentWorkspace(monitorHandle);
            var step = command == TilingCommand.IncreaseMasterCount
                ? MasterCountStep
                : -MasterCountStep;
            var layout = _layoutState.AdjustMasterCount(
                workspace,
                monitorHandle,
                _layoutOptions,
                step);
            RetilePrimaryWindows();
            _log.Info("master_count_adjusted", new
            {
                workspace,
                monitor = $"0x{monitorHandle.ToInt64():X}",
                masterCount = layout.MasterCount
            });
            return;
        }

        if (command is TilingCommand.DecreaseOuterGap
            or TilingCommand.IncreaseOuterGap
            or TilingCommand.DecreaseInnerGap
            or TilingCommand.IncreaseInnerGap)
        {
            var monitorHandle = GetActiveMonitorHandle(activeHandle);
            if (monitorHandle == nint.Zero)
            {
                return;
            }

            var workspace = _workspaceState.GetCurrentWorkspace(monitorHandle);
            var step = command is TilingCommand.IncreaseOuterGap or TilingCommand.IncreaseInnerGap
                ? GapStep
                : -GapStep;
            var layout = command is TilingCommand.IncreaseOuterGap or TilingCommand.DecreaseOuterGap
                ? _layoutState.AdjustOuterGap(workspace, monitorHandle, _layoutOptions, step)
                : _layoutState.AdjustInnerGap(workspace, monitorHandle, _layoutOptions, step);
            RetilePrimaryWindows();
            _log.Info("gap_adjusted", new
            {
                workspace,
                monitor = $"0x{monitorHandle.ToInt64():X}",
                outerGap = layout.OuterGap,
                innerGap = layout.InnerGap
            });
            return;
        }

        var focusScopeHandles = command is TilingCommand.FocusNext or TilingCommand.FocusPrevious
            ? GetFocusScopeHandles(activeHandle)
            : null;
        var hasFocusTarget = _commandProcessor.TryGetFocusTarget(
            command,
            activeHandle,
            out var focusTargetHandle,
            focusScopeHandles);
        var focusTarget = hasFocusTarget ? DescribeWindow(focusTargetHandle) : null;
        var executed = _commandProcessor.Execute(command, activeHandle, RetilePrimaryWindows, focusScopeHandles);
        if (executed
            && command is TilingCommand.FocusNext or TilingCommand.FocusPrevious
            && focusTargetHandle != nint.Zero)
        {
            _ = _cursorController.CenterOn(focusTargetHandle);
        }
        _log.Info("tiling_command_executed", new
        {
            command = command.ToString(),
            handle = $"0x{activeHandle.ToInt64():X}",
            focusTarget,
            executed
        });
    }

    private object? DescribeWindow(nint handle)
    {
        if (!_windowInspector.TryInspect(handle, out var candidate))
        {
            return new { handle = $"0x{handle.ToInt64():X}", status = "invalid" };
        }

        return new
        {
            handle = $"0x{candidate.Handle.ToInt64():X}",
            candidate.ProcessName,
            candidate.ClassName,
            candidate.Bounds,
            eligibility = _eligibilityClassifier.Classify(candidate).ToString()
        };
    }

    private IReadOnlyList<nint> GetFocusScopeHandles(nint activeHandle)
    {
        var monitorHandle = GetActiveMonitorHandle(activeHandle);
        return monitorHandle == nint.Zero
            ? []
            : _workspaceState.GetWindows(
                monitorHandle,
                _tilingState.TiledHandles,
                GetAssignedMonitorHandle);
    }

    private void FocusWorkspaceWindow(nint monitorHandle)
    {
        var handles = _workspaceState.GetWindows(
            monitorHandle,
            _tilingState.TiledHandles,
            GetAssignedMonitorHandle);
        if (handles.Count > 0)
        {
            _ = _windowCommander.Focus(handles[0]);
        }
    }

    private nint GetActiveMonitorHandle(nint activeHandle)
    {
        return _monitorOwnership.TryGetMonitor(activeHandle, out var assignedMonitor)
            ? assignedMonitor
            : _monitorCatalog.GetMonitorHandle(activeHandle);
    }

    private nint GetFocusedMonitorHandle()
    {
        return GetActiveMonitorHandle(NativeMethods.GetForegroundWindow());
    }

    private string GetFocusedWindowTitle()
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == nint.Zero)
        {
            return string.Empty;
        }

        var length = NativeMethods.GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder(length + 1);
        _ = NativeMethods.GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private MasterStackLayoutOptions GetActiveLayoutOptions()
    {
        var monitorHandle = GetActiveMonitorHandle(NativeMethods.GetForegroundWindow());
        return monitorHandle == nint.Zero
            ? _layoutOptions
            : _layoutState.Get(_workspaceState.GetCurrentWorkspace(monitorHandle), monitorHandle, _layoutOptions);
    }

    private MasterStackLayoutMode GetLayoutModeForMonitor(nint monitorHandle)
    {
        return _layoutState.Get(
            _workspaceState.GetCurrentWorkspace(monitorHandle),
            monitorHandle,
            _layoutOptions).Mode;
    }

    private nint GetAssignedMonitorHandle(nint windowHandle)
    {
        return _monitorOwnership.TryGetMonitor(windowHandle, out var monitorHandle)
            ? monitorHandle
            : nint.Zero;
    }

    private static bool IsWorkspaceMoveCommand(TilingCommand command)
    {
        return command is TilingCommand.MoveToNextWorkspace
            or TilingCommand.MoveToPreviousWorkspace
            or >= TilingCommand.MoveToWorkspace1 and <= TilingCommand.MoveToWorkspace9;
    }

    private bool CanFocusWithMouse(nint windowHandle)
    {
        if (!_managementState.IsEnabled || !_windowTracker.Contains(windowHandle))
        {
            return false;
        }

        return _monitorOwnership.TryGetMonitor(windowHandle, out var monitorHandle)
            && _workspaceState.IsInCurrentWorkspace(windowHandle, monitorHandle);
    }

    private void DisableManagement()
    {
        var disabled = _managementState.Disable();
        SaveWorkspaceState();
        ClearFloatingZOrder();
        RunUntileMode();
        _fullscreenState.Clear();
        _explorerVisibility.EnsureVisible();
        _workspaceBar?.SetVisible(false);
        _log.Info("management_disabled", new { disabled, reason = "emergency_hotkey" });
    }

    private void EnableManagement()
    {
        if (!_managementState.Enable())
        {
            _log.Info("management_enable_skipped", new { reason = "already_enabled" });
            return;
        }

        RunTileMode();
        ApplyWorkspaceVisibility();
        SaveWorkspaceState();
        _workspaceBar?.SetVisible(true);
        _log.Info("management_enabled", new { source = "hotkey" });
    }

    private void ToggleManagement()
    {
        if (_managementState.IsEnabled)
        {
            DisableManagement();
        }
        else
        {
            EnableManagement();
        }
    }

    private void RetilePrimaryWindows()
    {
        try
        {
            _ = TileAllMonitors(GetEligibleTiledHandles());
            SynchronizeFloatingZOrder();
        }
        catch (Exception exception) when (exception is Win32Exception or ArgumentOutOfRangeException or InvalidOperationException)
        {
            // Display topology can be transiently inconsistent while monitors connect/disconnect.
            _log.Error("retile_failed", new
            {
                exception = exception.GetType().Name
            });
        }
    }

    // A one-shot raise is not enough: activating a tiled window puts it above the float again.
    // Marking floats as topmost keeps them above tiled windows until they are unfloated.
    private void SynchronizeFloatingZOrder()
    {
        foreach (var handle in _tilingState.FloatingHandles)
        {
            if (_topmostHandles.Contains(handle))
            {
                continue;
            }

            if (SetTopmost(handle, true))
            {
                _ = _topmostHandles.Add(handle);
            }
        }

        foreach (var handle in _topmostHandles.ToArray())
        {
            if (_tilingState.FloatingHandles.Contains(handle))
            {
                continue;
            }

            _ = SetTopmost(handle, false);
            _ = _topmostHandles.Remove(handle);
        }
    }

    private void ClearFloatingZOrder()
    {
        foreach (var handle in _topmostHandles)
        {
            _ = SetTopmost(handle, false);
        }

        _topmostHandles.Clear();
    }

    private bool SetTopmost(nint handle, bool topmost)
    {
        if (!_windowInspector.TryInspect(handle, out _))
        {
            return false;
        }

        return NativeMethods.SetWindowPos(
            handle,
            topmost ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    private IReadOnlyList<TilingOperationResult> TileAllMonitors(IReadOnlyList<nint> orderedHandles)
    {
        var operations = ApplyAllMonitorLayouts(orderedHandles);
        var failedHandles = operations
            .SelectMany(operation => operation.PlacementResults)
            .Where(result => !result.Applied)
            .Select(result => result.Handle)
            .ToArray();
        if (failedHandles.Length == 0)
        {
            return operations;
        }

        foreach (var handle in failedHandles)
        {
            _ = _windowTracker.Remove(handle);
            _ = _monitorOwnership.Remove(handle);
        }

        _tilingState.Synchronize(_windowTracker.Snapshot());
        var retryHandles = GetEligibleTiledHandles();
        return retryHandles.Count == orderedHandles.Count
            ? operations
            : ApplyAllMonitorLayouts(retryHandles);
    }

    private List<TilingOperationResult> ApplyAllMonitorLayouts(IReadOnlyList<nint> orderedHandles)
    {
        var operations = new List<TilingOperationResult>();
        var workspaceBarOptions = _settings.ToWorkspaceBarOptions();
        var fullscreenMonitors = new HashSet<nint>();
        foreach (var monitor in _monitorCatalog.GetWorkAreas())
        {
            var monitorHandles = _monitorOwnership.GetWindows(monitor.Handle, orderedHandles);
            if (monitorHandles.Count > 0)
            {
                var workspace = _workspaceState.GetCurrentWorkspace(monitor.Handle);
                if (_fullscreenState.TryGetWindow(monitor.Handle, workspace, out var fullscreenHandle)
                    && monitorHandles.Contains(fullscreenHandle))
                {
                    // A fullscreen window takes the whole monitor: no bar reservation and no gaps.
                    fullscreenMonitors.Add(monitor.Handle);
                    operations.Add(_windowTiler.Tile(monitor.Bounds, [fullscreenHandle], FullscreenLayoutOptions));
                    continue;
                }

                var options = _layoutState.Get(
                    workspace,
                    monitor.Handle,
                    _layoutOptions);
                var workArea = workspaceBarOptions.ReserveTopSpace(monitor.Bounds);
                if (!workArea.IsUsable)
                {
                    continue;
                }

                operations.Add(_windowTiler.Tile(workArea, monitorHandles, options));
            }
        }

        _workspaceBar?.SetFullscreenMonitors(fullscreenMonitors);
        return operations;
    }

    private IReadOnlyList<nint> GetEligibleTiledHandles()
    {
        var monitors = _monitorCatalog.GetWorkAreas();
        var availableMonitors = monitors
            .Select(monitor => monitor.Handle)
            .Where(handle => handle != nint.Zero)
            .ToHashSet();
        var fallbackMonitor = monitors
            .OrderByDescending(monitor => monitor.IsPrimary)
            .Select(monitor => monitor.Handle)
            .FirstOrDefault();
        foreach (var handle in _tilingState.TiledHandles.ToArray())
        {
            var monitorHandle = GetAssignedMonitorHandle(handle);
            if (monitorHandle != nint.Zero
                && _workspaceState.HasWorkspace(handle)
                && !_workspaceState.IsInCurrentWorkspace(handle, monitorHandle))
            {
                continue;
            }

            if (!_windowInspector.TryInspect(handle, out var candidate)
                || _eligibilityClassifier.Classify(candidate) != WindowEligibility.Managed)
            {
                _ = _windowTracker.Remove(handle);
                _ = _monitorOwnership.Remove(handle);
                continue;
            }

            _ = _windowTracker.Observe(candidate);
            var hasAssignedMonitor = _monitorOwnership.TryGetMonitor(handle, out monitorHandle);
            if (!hasAssignedMonitor || monitorHandle == nint.Zero || !availableMonitors.Contains(monitorHandle))
            {
                var resolvedMonitor = ResolveMonitorHandle(handle, availableMonitors, fallbackMonitor);
                if (resolvedMonitor == nint.Zero)
                {
                    _ = _monitorOwnership.Remove(handle);
                }
                else
                {
                    _ = _monitorOwnership.Assign(handle, resolvedMonitor);
                }
            }

            if (_monitorOwnership.TryGetMonitor(handle, out var assignedMonitor))
            {
                _workspaceState.AssignToCurrentWorkspaceIfMissing(handle, assignedMonitor);
            }
        }

        _tilingState.Synchronize(_windowTracker.Snapshot());
        _workspaceState.Synchronize(GetManagedHandles(), GetDefaultWorkspace);
        _fullscreenState.Synchronize(_tilingState.TiledHandles);
        return _tilingState.TiledHandles
            .Where(handle => _monitorOwnership.TryGetMonitor(handle, out var monitorHandle)
                && _workspaceState.IsInCurrentWorkspace(handle, monitorHandle))
            .ToArray();
    }

    private void ApplyWorkspaceVisibility()
    {
        var managedHandles = GetManagedHandles();
        _workspaceState.Synchronize(managedHandles, GetDefaultWorkspace);
        _workspaceVisibilitySynchronizer.Synchronize(managedHandles);
        _workspaceHiddenHandles.Clear();
        foreach (var handle in managedHandles)
        {
            if (_monitorOwnership.TryGetMonitor(handle, out var monitorHandle)
                && !_workspaceState.IsInCurrentWorkspace(handle, monitorHandle))
            {
                _workspaceHiddenHandles.Add(handle);
            }
        }
    }

    private IReadOnlyList<nint> GetManagedHandles()
    {
        return [.. _tilingState.TiledHandles, .. _tilingState.FloatingHandles];
    }

    // A window that does not resolve to a monitor yet must land where the user is, not on the primary monitor.
    private nint ResolveMonitorHandle(nint windowHandle, IReadOnlySet<nint> availableMonitors, nint fallbackMonitor)
    {
        nint[] candidates =
        [
            _monitorCatalog.GetMonitorHandle(windowHandle),
            _monitorCatalog.GetNearestMonitorHandle(windowHandle),
            _monitorCatalog.GetCursorMonitorHandle(),
            fallbackMonitor
        ];

        return candidates.FirstOrDefault(candidate => candidate != nint.Zero && availableMonitors.Contains(candidate));
    }

    private int GetDefaultWorkspace(nint windowHandle)
    {
        if (_monitorOwnership.TryGetMonitor(windowHandle, out var monitorHandle) && monitorHandle != nint.Zero)
        {
            return _workspaceState.GetCurrentWorkspace(monitorHandle);
        }

        var cursorMonitor = _monitorCatalog.GetCursorMonitorHandle();
        return cursorMonitor == nint.Zero
            ? 0
            : _workspaceState.GetCurrentWorkspace(cursorMonitor);
    }

    private void LoadWorkspaceState(IEnumerable<nint> managedHandles)
    {
        try
        {
            var state = _workspaceStateStore.Load();
            if (state is not null)
            {
                _workspaceState.Restore(state, managedHandles);
            }
            else
            {
                _workspaceState.Synchronize(managedHandles);
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentOutOfRangeException)
        {
            _workspaceState.Synchronize(managedHandles);
            _log.Error("workspace_state_invalid", new
            {
                fallback = "defaults",
                exception = exception.GetType().Name
            });
        }
    }

    private void SaveWorkspaceState()
    {
        try
        {
            _workspaceStateStore.Save(_workspaceState.Export());
        }
        catch (IOException exception)
        {
            _log.Error("workspace_state_save_failed", new { exception = exception.GetType().Name });
        }
    }

    private bool TryLoadManagedWindowStates(
        out IReadOnlyList<ManagedWindowState> states,
        string operation)
    {
        try
        {
            states = _windowStateStore.Load();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            states = [];
            _log.Error("managed_window_state_invalid", new
            {
                operation,
                action = "blocked",
                exception = exception.GetType().Name
            });
            return false;
        }
    }

    private void PersistOriginalBoundsIfNeeded(TrackedWindow trackedWindow)
    {
        if (_monitorCatalog.GetMonitorHandle(trackedWindow.Handle) == nint.Zero)
        {
            return;
        }

        var existingStates = _windowStateStore.Load();
        var updatedStates = ManagedWindowStateSet.AddIfMissing(existingStates, trackedWindow);
        if (!ReferenceEquals(existingStates, updatedStates))
        {
            _windowStateStore.Save(updatedStates);
        }
    }

    private bool ShouldRetileFor(WindowEventKind kind)
    {
        return kind is WindowEventKind.Created or WindowEventKind.Destroyed or WindowEventKind.Shown or WindowEventKind.Hidden;
    }

    // Windows like Chromium browsers are cloaked/zero-sized when Shown fires and emit no further
    // events once ready, so poll them a few times until they become eligible or give up.
    private void MaybeScheduleEligibilityRecheck(nint handle, WindowEligibility eligibility, WindowEventKind kind, WindowCandidate candidate)
    {
        if (kind != WindowEventKind.Shown
            || eligibility is not (WindowEligibility.Cloaked or WindowEligibility.InvalidBounds or WindowEligibility.NotVisible))
        {
            return;
        }

        var attempts = _eligibilityRecheckAttempts.GetValueOrDefault(handle);
        if (attempts >= MaxEligibilityRecheckAttempts)
        {
            _ = _eligibilityRecheckAttempts.Remove(handle);
            _log.Info("eligibility_recheck_abandoned", new
            {
                handle = $"0x{handle.ToInt64():X}",
                eligibility = eligibility.ToString(),
                processName = candidate.ProcessName
            });
            return;
        }

        _eligibilityRecheckAttempts[handle] = attempts + 1;
        _scheduleEligibilityRecheck?.Invoke(handle);
    }

    private void HandleWindowEvent(WindowEvent windowEvent)
    {
        try
        {
            // While the workstation is locked the desktop switches to the secure desktop;
            // any retile/placement here fights the lock screen and corrupts window bounds.
            // Events fired during lock are discarded; a full resync happens on unlock.
            if (_sessionLocked)
            {
                return;
            }

            if (windowEvent.Kind == WindowEventKind.Destroyed)
            {
                _ = _workspaceHiddenHandles.Remove(windowEvent.Handle);
                _ = _eligibilityRecheckAttempts.Remove(windowEvent.Handle);
                var wasTracked = _windowTracker.Remove(windowEvent.Handle);
                _tilingState.Synchronize(_windowTracker.Snapshot());
                if (wasTracked)
                {
                    _log.Info("managed_window_destroyed", new
                    {
                        handle = $"0x{windowEvent.Handle.ToInt64():X}",
                        trackedWindowCount = _windowTracker.Snapshot().Count
                    });
                }

                if (wasTracked && _managementState.IsEnabled)
                {
                    RetilePrimaryWindows();
                }

                return;
            }

            // A window we hid because it belongs to another workspace must never be removed
            // from tracking, no matter which event fires for it (Hidden, Shown,
            // LocationChanged, Activated). Otherwise a stale event while it is hidden would
            // classify it as NotVisible and drop it, so it never reappears when its
            // workspace is selected again.
            if (_workspaceHiddenHandles.Contains(windowEvent.Handle))
            {
                return;
            }

            if (windowEvent.Kind == WindowEventKind.Hidden
                && _windowTracker.Contains(windowEvent.Handle))
            {
                // A tracked window can be hidden by the app itself. Re-inspect its current
                // state to survive the race where a workspace switch back re-shows the
                // window before this stale Hidden event is processed.
                var currentlyVisible = _windowInspector.TryInspect(windowEvent.Handle, out var hiddenCandidate)
                    && hiddenCandidate.IsVisible;
                var inNonCurrentWorkspace = _monitorOwnership.TryGetMonitor(windowEvent.Handle, out var monitorHandle)
                    && !_workspaceState.IsInCurrentWorkspace(windowEvent.Handle, monitorHandle);
                if (currentlyVisible || inNonCurrentWorkspace)
                {
                    return;
                }
            }

            if (_windowInspector.TryInspect(windowEvent.Handle, out var candidate))
            {
                if ((candidate.ClassName.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase)
                        || candidate.ClassName.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
                    && _managementState.IsEnabled
                    && windowEvent.Kind is WindowEventKind.Shown or WindowEventKind.Hidden)
                {
                    RetilePrimaryWindows();
                    return;
                }

                var eligibility = _eligibilityClassifier.Classify(candidate);
                if (eligibility != WindowEligibility.Managed)
                {
                    MaybeScheduleEligibilityRecheck(windowEvent.Handle, eligibility, windowEvent.Kind, candidate);
                    var wasTracked = _windowTracker.Remove(windowEvent.Handle);
                    if (wasTracked)
                    {
                        _tilingState.Synchronize(_windowTracker.Snapshot());
                        if (_managementState.IsEnabled && ShouldRetileFor(windowEvent.Kind))
                        {
                            RetilePrimaryWindows();
                        }
                    }

                    return;
                }

                var wasAlreadyTracked = _windowTracker.Contains(windowEvent.Handle);
                var tracked = _windowTracker.Observe(candidate);
                _ = _eligibilityRecheckAttempts.Remove(windowEvent.Handle);
                _tilingState.Synchronize(_windowTracker.Snapshot());
                if (tracked.Eligibility == WindowEligibility.Managed && _managementState.IsEnabled)
                {
                    PersistOriginalBoundsIfNeeded(tracked);
                }

                // Windows that are cloaked or zero-sized at Shown time (e.g. Chromium browsers)
                // only become eligible later via LocationChanged/Activated, so retile on the
                // transition into the managed set regardless of event kind.
                var newlyManaged = !wasAlreadyTracked && tracked.Eligibility == WindowEligibility.Managed;
                if (newlyManaged && !ShouldRetileFor(windowEvent.Kind))
                {
                    _log.Info("managed_window_discovered_late", new
                    {
                        handle = $"0x{windowEvent.Handle.ToInt64():X}",
                        kind = windowEvent.Kind.ToString(),
                        processName = candidate.ProcessName
                    });
                }

                if (_managementState.IsEnabled && (ShouldRetileFor(windowEvent.Kind) || newlyManaged))
                {
                    RetilePrimaryWindows();
                }
            }
        }
        catch (Exception exception)
        {
            _log.Error("window_event_processing_failed", new
            {
                kind = windowEvent.Kind.ToString(),
                handle = $"0x{windowEvent.Handle.ToInt64():X}",
                exception = exception.GetType().Name
            });
        }
    }

    private IReadOnlyList<TrackedWindow> RefreshTrackedWindows()
    {
        var trackedWindows = _windowTracker.Seed(_windowEnumerator.GetTopLevelWindowHandles().Select(_windowInspector.Inspect));
        _tilingState.Synchronize(trackedWindows);
        return trackedWindows;
    }

    private void LoadSettings()
    {
        try
        {
            var settings = _settingsProvider.Load();
            _settings = settings;
            _layoutOptions = settings.ToLayoutOptions();
            _eligibilityOptions.ExcludedExecutableNames.Clear();
            foreach (var executable in settings.ExcludedExecutables ?? [])
            {
                _eligibilityOptions.ExcludedExecutableNames.Add(executable);
            }
            _log.Info("settings_loaded", new { settings.MasterRatio, settings.OuterGap, settings.InnerGap, settings.MasterCount });
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or ArgumentOutOfRangeException or Tomlyn.TomlException)
        {
            _layoutOptions = MasterStackLayoutOptions.Default;
            _settings = RootlessWMSettings.Default;
            _log.Error("settings_invalid", new
            {
                fallback = "defaults",
                exception = exception.GetType().Name
            });
        }
    }
}
