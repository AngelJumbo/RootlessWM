using System.IO.Pipes;
using System.Text.Json;
using RootlessWM.Contracts.Messages;
using RootlessWM.Domain;

namespace RootlessWM.App;

internal sealed class WindowManagerClient
{
    private const int ConnectTimeoutMilliseconds = 2000;
    private readonly string _pipeName;
    private readonly ConsoleDiagnosticLog _log;

    public WindowManagerClient(ConsoleDiagnosticLog log, string? pipeName = null)
    {
        _log = log;
        _pipeName = pipeName ?? "RootlessWM.WindowManager";
    }

    public bool ExecuteCommand(TilingCommand command)
    {
        var response = Send(new WindowManagerRequest(WindowManagerCommand.ExecuteCommand, TilingCommand: command));
        return response?.Success == true;
    }

    public void SetMouseFocusSuspended(bool suspended)
    {
        _ = Send(new WindowManagerRequest(WindowManagerCommand.SetMouseFocusSuspended, Suspended: suspended));
    }

    public void SetStatusBarVisibility(nint monitorHandle, int workspace, bool hidden)
    {
        _ = Send(new WindowManagerRequest(
            WindowManagerCommand.SetStatusBarVisibility,
            MonitorHandle: monitorHandle.ToInt64(),
            Workspace: workspace,
            StatusBarHidden: hidden));
    }

    public WindowManagerStatus? GetStatus()
    {
        return Send(new WindowManagerRequest(WindowManagerCommand.GetStatus))?.Status;
    }

    public WindowManagerStatus? ReloadSettings()
    {
        return Send(new WindowManagerRequest(WindowManagerCommand.ReloadSettings))?.Status;
    }

    public void Shutdown()
    {
        _ = Send(new WindowManagerRequest(WindowManagerCommand.Shutdown));
    }

    private WindowManagerResponse? Send(WindowManagerRequest request)
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipeClient.Connect(ConnectTimeoutMilliseconds);
            using var writer = new StreamWriter(pipeClient, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipeClient, leaveOpen: true);
            writer.WriteLine(JsonSerializer.Serialize(request));
            var line = reader.ReadLine();
            return line is null ? null : JsonSerializer.Deserialize<WindowManagerResponse>(line);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            _log.Error("window_manager_unreachable", new { command = request.Command.ToString(), exception = exception.GetType().Name });
            return null;
        }
    }
}
