using System.ComponentModel;
using System.Diagnostics;
using RootlessWM.Contracts.Messages;
using RootlessWM.Domain;
using RootlessWM.Platform.Win32;

namespace RootlessWM.App;

internal sealed class WmApplication
{
    private const string WindowManagerTaskName = "RootlessWM.WindowManager";
    private static readonly TimeSpan HelperConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HelperConnectPollInterval = TimeSpan.FromMilliseconds(200);

    private ConsoleDiagnosticLog _log = new();
    private readonly MonitorCatalog _monitorCatalog = new();
    private readonly TomlSettingsProvider _settingsProvider = new();
    private RootlessWMSettings _settings = RootlessWMSettings.Default;
    private WindowManagerClient? _windowManagerClient;
    private RunnerController? _runnerController;
    private WorkspaceBarController? _workspaceBar;

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

        if (args.Contains("--manage", StringComparer.OrdinalIgnoreCase))
        {
            RunManageMode();
        }

        _log.Info("application_stopped", new { reason = "host_completed" });
        return 0;
    }

    private void OpenSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_settingsProvider.FilePath)
            {
                UseShellExecute = true
            });
        }
        catch (Win32Exception exception)
        {
            _log.Error("settings_open_failed", new { path = _settingsProvider.FilePath, exception.Message });
        }
    }

    private void RunManageMode()
    {
        var client = new WindowManagerClient(_log);
        if (client.GetStatus() is null && !StartWindowManagerHelper())
        {
            _log.Error("management_blocked", new { reason = "helper_start_failed" });
            return;
        }

        _windowManagerClient = client;
        try
        {
            if (!WaitForHelper(client))
            {
                _log.Error("management_blocked", new { reason = "helper_unreachable" });
                return;
            }

            using var workspaceBar = new WorkspaceBarController(
                9,
                GetFocusedMonitorHandle,
                GetFocusedWindowTitle,
                _log);
            _workspaceBar = workspaceBar;
            using var runnerController = new RunnerController(_log);
            _runnerController = runnerController;
            using var messageLoop = new HotkeyMessageLoop();
            GlobalHotkeySource? hotkeySource = null;
            TrayController? trayControllerRef = null;

            using var trayController = new TrayController(
                () => ExecuteTilingCommand(client.GetStatus()?.ManagementEnabled == true
                    ? TilingCommand.DisableManagement
                    : TilingCommand.EnableManagement),
                ReloadSettingsAndHotkeys,
                OpenSettings,
                messageLoop.Stop,
                () => client.GetStatus()?.ManagementEnabled == true);
            trayControllerRef = trayController;

            ReloadSettingsAndHotkeys();
            UpdateStatus();
            _log.Info("management_started", new { hotkeyModifier = "Alt+Shift" });

            messageLoop.Run((messageId, hotkeyIdentifier) =>
            {
                if (hotkeySource?.TryGetCommand(messageId, hotkeyIdentifier, out var command) == true)
                {
                    ExecuteTilingCommand(command);
                }
                else if (hotkeySource?.TryGetLaunch(messageId, hotkeyIdentifier, out var launch) == true)
                {
                    ExecuteLaunch(launch);
                }
            });

            hotkeySource?.Dispose();

            void ReloadSettingsAndHotkeys()
            {
                LoadSettings();
                hotkeySource?.Dispose();
                hotkeySource = new GlobalHotkeySource();
                var unavailableCommands = hotkeySource.Start(_settings.Hotkeys, _settings.Launch);
                workspaceBar.ApplyOptions(_settings.ToWorkspaceBarOptions());
                runnerController.ApplySettings(_settings.Runner);
                client.ReloadSettings();
                _log.Info("settings_reloaded", new
                {
                    unavailableCommands = unavailableCommands.Select(c => c.ToString()).ToArray(),
                    unavailableLaunchHotkeys = hotkeySource.UnavailableLaunchHotkeys
                });
                UpdateStatus();
            }

            void UpdateStatus()
            {
                var status = client.GetStatus();
                if (status is null)
                {
                    return;
                }

                trayControllerRef?.SetManagementEnabled(status.ManagementEnabled);
                trayControllerRef?.SetStatus(StatusText.Format(new StatusSnapshot(
                    status.ManagementEnabled,
                    status.CurrentWorkspace,
                    status.WorkspaceCount,
                    status.MasterRatio,
                    status.OuterGap,
                    status.InnerGap,
                    status.ManagedWindowCount,
                    status.MasterCount)));

                workspaceBar.Update(
                    _monitorCatalog.GetWorkAreas(),
                    monitorHandle => status.Monitors?.FirstOrDefault(m => m.MonitorHandle == monitorHandle.ToInt64())?.CurrentWorkspace ?? 0,
                    monitorHandle => Enum.TryParse<MasterStackLayoutMode>(
                        status.Monitors?.FirstOrDefault(m => m.MonitorHandle == monitorHandle.ToInt64())?.LayoutMode,
                        out var mode)
                        ? mode
                        : MasterStackLayoutMode.MasterLeft);
                workspaceBar.SetVisible(status.ManagementEnabled);
            }

            void ExecuteTilingCommand(TilingCommand command)
            {
                if (command != TilingCommand.OpenRunner)
                {
                    runnerController.Dismiss();
                    client.SetMouseFocusSuspended(false);
                }

                if (command == TilingCommand.OpenRunner)
                {
                    runnerController.Toggle();
                    client.SetMouseFocusSuspended(runnerController.IsVisible);
                    _log.Info("runner_hotkey_pressed", new { source = "hotkey", visible = runnerController.IsVisible });
                    return;
                }

                if (command == TilingCommand.ToggleStatusBar)
                {
                    var monitorHandle = GetFocusedMonitorHandle();
                    if (monitorHandle != nint.Zero)
                    {
                        var status = client.GetStatus();
                        var workspace = status?.Monitors?.FirstOrDefault(m => m.MonitorHandle == monitorHandle.ToInt64())?.CurrentWorkspace ?? 0;
                        workspaceBar.ToggleStatusBar(monitorHandle, workspace);
                        _log.Info("status_bar_toggled", new { monitor = $"0x{monitorHandle.ToInt64():X}", workspace });
                    }
                    return;
                }

                var executed = client.ExecuteCommand(command);
                UpdateStatus();
                _log.Info("tiling_command_forwarded", new { command = command.ToString(), executed });
            }
        }
        finally
        {
            _runnerController = null;
            _workspaceBar = null;
            client.Shutdown();
            _windowManagerClient = null;
        }
    }

    private bool StartWindowManagerHelper()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                $"/Run /TN \"{WindowManagerTaskName}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Win32Exception exception)
        {
            _log.Error("helper_start_failed", new { exception = exception.NativeErrorCode });
            return false;
        }
    }

    private static bool WaitForHelper(WindowManagerClient client)
    {
        var deadline = DateTime.UtcNow + HelperConnectTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (client.GetStatus() is not null)
            {
                return true;
            }

            Thread.Sleep(HelperConnectPollInterval);
        }

        return false;
    }

    private void ExecuteLaunch(LaunchHotkeySettings launch)
    {
        _runnerController?.Dismiss();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = launch.Command,
                Arguments = launch.Args ?? string.Empty,
                WorkingDirectory = launch.WorkingDirectory ?? string.Empty,
                UseShellExecute = true
            };

            if (launch.RunAsAdmin)
            {
                startInfo.Verb = "runas";
            }

            Process.Start(startInfo);
            _log.Info("program_launched", new { hotkey = launch.Hotkey, command = launch.Command, args = launch.Args, admin = launch.RunAsAdmin });
        }
        catch (Win32Exception exception)
        {
            _log.Error("program_launch_failed", new { hotkey = launch.Hotkey, command = launch.Command, error = exception.NativeErrorCode });
        }
    }

    private nint GetFocusedMonitorHandle()
    {
        return _monitorCatalog.GetMonitorHandle(NativeMethods.GetForegroundWindow());
    }

    private static string GetFocusedWindowTitle()
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

    private void LoadSettings()
    {
        try
        {
            var settings = _settingsProvider.Load();
            _settings = settings;
            _log.Info("settings_loaded", new { settings.MasterRatio, settings.OuterGap, settings.InnerGap, settings.MasterCount });
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException or ArgumentOutOfRangeException or Tomlyn.TomlException)
        {
            _settings = RootlessWMSettings.Default;
            _log.Error("settings_invalid", new
            {
                fallback = "defaults",
                exception = exception.GetType().Name
            });
        }
    }
}
