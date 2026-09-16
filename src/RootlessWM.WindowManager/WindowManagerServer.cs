using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using RootlessWM.App;
using RootlessWM.Contracts.Messages;

namespace RootlessWM.WindowManager;

internal sealed class WindowManagerServer : IDisposable
{
    public const string DefaultPipeName = "RootlessWM.WindowManager";

    private readonly WindowManagerHost _host;
    private readonly ConsoleDiagnosticLog _log;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenTask;

    public WindowManagerServer(WindowManagerHost host, ConsoleDiagnosticLog log, string? pipeName = null)
    {
        _host = host;
        _log = log;
        PipeName = pipeName ?? DefaultPipeName;
    }

    public string PipeName { get; }

    public void Start()
    {
        _listenTask = Task.Run(() => AcceptLoopAsync(_cancellation.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = CreatePipeServer();
                await pipeServer.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleClientAsync(pipeServer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log.Error("pipe_server_error", new { exception = exception.GetType().Name });
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipeServer, leaveOpen: true);
        using var writer = new StreamWriter(pipeServer, leaveOpen: true) { AutoFlush = true };
        while (pipeServer.IsConnected)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return;
            }

            WindowManagerResponse response;
            try
            {
                var request = JsonSerializer.Deserialize<WindowManagerRequest>(line);
                response = request is null
                    ? new WindowManagerResponse(false, "invalid_request")
                    : _host.HandleRequest(request);
            }
            catch (JsonException)
            {
                response = new WindowManagerResponse(false, "invalid_request");
            }

            await writer.WriteLineAsync(JsonSerializer.Serialize(response)).ConfigureAwait(false);
        }
    }

    // The pipe must be reachable from the normal-integrity RootlessWM process while this
    // process runs elevated, so the ACL explicitly grants the interactive user read/write.
    private NamedPipeServerStream CreatePipeServer()
    {
        var security = new PipeSecurity();
        var identity = WindowsIdentity.GetCurrent().User;
        if (identity is not null)
        {
            security.AddAccessRule(new PipeAccessRule(identity, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        }

        var interactiveUsers = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
        security.AddAccessRule(new PipeAccessRule(interactiveUsers, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _cancellation.Dispose();
    }
}
