# Simple Elevated Helper Plan

## Goal

Run RootlessWM normally while a separate window-manager executable performs window management at high integrity.

Normal startup must not show UAC. The installer creates one elevated scheduled task.

## Small Architecture

```text
RootlessWM.exe --manage
Normal integrity
Hotkeys, bar, runner, application launching
        |
        | one request per named-pipe connection
        v
RootlessWM.WindowManager.exe
High integrity
Window hooks, tiling, focus, workspaces, restore
```

Keep the normal UI and elevated window manager as separate executable projects. Add one tiny contracts project containing only the messages shared between them.

The scheduled task runs:

```text
RootlessWM.WindowManager.exe --no-logs
```

## IPC

Use `NamedPipeServerStream` in the helper and `NamedPipeClientStream` in the normal process.

Pipe name:

```text
RootlessWM.WindowManager.<session-id>
```

Create it with `PipeOptions.CurrentUserOnly`. This restricts access to processes running as the same Windows user and avoids a custom ACL and authentication handshake.

Use one connection for one command:

```text
connect -> send one JSON request -> receive one JSON response -> close
```

Encode each request and response as one UTF-8 JSON line:

```text
{"command":"ExecuteCommand","value":"FocusNext"}
```

Use `StreamReader.ReadLineAsync` and `StreamWriter.WriteLineAsync`. Reject empty lines, invalid JSON, and unknown commands.

There is no persistent connection to maintain. A failed request fails immediately; the next command creates a fresh connection.

No push events, subscriptions, handshakes, nonces, request IDs, revisions, or streaming snapshots.

## Commands

Keep the allowlist small:

| Command | Payload | Result |
|---|---|---|
| `ExecuteCommand` | Existing `TilingCommand` value | Success or error |
| `SetMouseFocusSuspended` | Boolean | Success |
| `GetStatus` | None | Complete status DTO |

`OpenRunner` and `ToggleStatusBar` remain local and are never sent to the helper.

The protocol must not accept executable paths, command lines, shell text, or native function names.

Keep all shared types in one file in `RootlessWM.Contracts`:

- `WindowManagerCommand` enum;
- `WindowManagerRequest` record;
- `WindowManagerResponse` record;
- `WindowManagerStatus` record.

Do not build a protocol framework, dispatcher abstraction, version negotiation, or error-code catalog.

## Ownership

### Normal process

- Loads UI, runner, launch, and hotkey settings.
- Registers global hotkeys.
- Owns the workspace bar, and runner.
- Launches configured applications.
- Sends management hotkeys to the helper.
- Requests `GetStatus` on the existing UI refresh timer.
- Shows management as unavailable when a request fails.

### Elevated helper

- Contains the management code extracted from `WmApplication`.
- Owns all live window, monitor, tiling, fullscreen, and workspace state.
- Owns WinEvent, mouse, display-change, and session lock/unlock handling.
- Reads the same settings file directly when it starts.
- Owns the existing managed-window and workspace persistence for this first version.
- Never starts a process and exposes no launch command.

Keeping persistence in the helper avoids a second synchronization protocol. Move it later only if there is a demonstrated problem.

## Failure Behavior

- The helper keeps managing windows when no client is connected.
- Every request already uses a new connection.
- Failed commands are not queued or replayed.
- Helper exit uses the current restore behavior.

This avoids stale command and snapshot handling.

## Implementation Steps

### 1. Add the two small projects

Add:

- `RootlessWM.Contracts`, a class library with the four shared message types;
- `RootlessWM.WindowManager`, an executable referencing the contracts project.

`RootlessWM` also references the contracts project. The two executables do not reference each other.

### 2. Move management ownership

Move the management host, live state, hooks, Win32 window operations, and persistence into `RootlessWM.WindowManager`.

Keep `WmApplication` responsible for hotkeys, bar, runner, and launching.

Do not redesign the domain classes. Move the existing code with minimal changes.

### 3. Add the tiny transport

Add only:

- `WindowManagerClient.cs` in `RootlessWM`;
- `WindowManagerServer.cs` in `RootlessWM.WindowManager`.

The server accepts a connection, reads one line, handles it, writes one line, closes it, and waits for the next connection. Use `System.IO.Pipes` and `System.Text.Json`; add no package dependencies.

Test one successful round trip, one unknown command, and helper-unavailable behavior.

### 4. Replace direct calls

In `WmApplication`:

- Handle runner and status-bar commands locally.
- Send other `TilingCommand` values through `ExecuteCommand`.
- Send runner visibility through `SetMouseFocusSuspended`.
- Request `GetStatus` on the existing UI refresh timer.

The helper reads settings at startup and reloads them when `WmApplication` sends `ReloadSettings`.

### 5. Install both modes

Update `RootlessWM.iss`:

- Keep the normal Startup shortcut using `--manage --no-logs`.
- Publish and install `RootlessWM.WindowManager.exe` beside `RootlessWM.exe`.
- Create the `RootlessWM.WindowManager` scheduled task using `/RL HIGHEST /IT`.
- Request elevation only while creating or updating that task.
- Delete the scheduled task during uninstall.
- Remove any old elevated monolithic task during upgrade.

## Implementation Status

Status as of 2026-09-16:

| Step | Status | Notes |
|---|---|---|
| 1. Add projects | Complete with scope drift | Both projects exist and build. `RootlessWM.Contracts` contains more than the four IPC records: settings, logging, geometry, selected domain types, and Win32 interop were also moved there. |
| 2. Move management ownership | Complete | `WindowManagerHost` owns management state, hooks, tiling, workspaces, persistence, and Win32 window operations. `WmApplication` owns hotkeys, bar, runner, tray, and application launching. |
| 3. Add transport | Partial | Client and server exist and use JSON lines over a named pipe. The implemented pipe is `RootlessWM.WindowManager`, not session-specific. The ACL grants the current identity and interactive users. No focused IPC tests were added. |
| 4. Replace direct calls | Complete with additions | Management commands and status use IPC. `ReloadSettings` and `Shutdown` were also implemented. |
| 5. Install both modes | Partial | Publishing places both executables in one folder and the installer wildcard includes them. The installer does not create the elevated scheduled task or remove it during uninstall. |

Automated verification repeated on 2026-09-16:

- `dotnet build RootlessWM.sln`: passed with 0 errors.
- `dotnet test tests/RootlessWM.Tests/RootlessWM.Tests.csproj`: 198 tests passed.

The main completion criterion is not met yet. `WmApplication` currently starts `RootlessWM.WindowManager.exe --manage` using `Verb = "runas"`, so every normal application start requests UAC consent. The scheduled-task startup required for no-UAC logon has not been implemented or tested.

## Deliberate Non-Goals

Do not add these to the first working version:

- Long-lived pipe connections or reconnect loops.
- Server-pushed state snapshots.
- Protocol negotiation or version exchange.
- Nonces, request IDs, or state revisions.
- Cross-process persistence synchronization.
- Command replay after reconnect.
- Multiple simultaneous clients.
- Live management-settings reload.
- Automatic helper task repair at runtime.

Add hardening only when a concrete failure requires it.

## How to Test

### Automated checks

From the repository root:

```powershell
dotnet build RootlessWM.sln
dotnet test tests/RootlessWM.Tests/RootlessWM.Tests.csproj
.\scripts\Publish-Local.ps1
```

Pass criteria:

- Build finishes with 0 errors.
- All tests pass.
- `artifacts\publish` contains both `RootlessWM.exe` and `RootlessWM.WindowManager.exe`.

### Current implementation smoke test

1. Stop existing RootlessWM processes:

        ```powershell
        Get-Process RootlessWM, RootlessWM.WindowManager -ErrorAction SilentlyContinue | Stop-Process
        ```

2. Start the published normal process:

        ```powershell
        Start-Process .\artifacts\publish\RootlessWM.exe -ArgumentList '--manage'
        ```

3. Accept the UAC prompt. This prompt is expected in the current implementation and proves the helper is started through `runas`; it is not the final desired behavior.
4. In Task Manager, enable the **Elevated** column on the **Details** tab. Confirm `RootlessWM.exe` is not elevated and `RootlessWM.WindowManager.exe` is elevated.
5. Open several normal windows. Exercise enable/disable management, focus next/previous, move, resize, layout, workspace, fullscreen, runner, and status-bar hotkeys.
6. Run Task Manager elevated and confirm its window can be focused, moved, and tiled by RootlessWM.
7. Open the runner and confirm focus-follows-mouse does not steal focus. Launch a normal application and confirm it is not elevated.
8. Reload settings and confirm bar and management settings update without restarting either process.
9. End `RootlessWM.WindowManager.exe` in Task Manager. Confirm the normal process does not crash and management commands fail without moving windows.
10. Exit the normal process. Confirm the helper exits and managed windows are restored.

### Final no-UAC acceptance test

Run this only after the scheduled-task installer work is complete:

1. Build and install the package.
2. Confirm Task Scheduler contains `RootlessWM.WindowManager`, configured for the current user with **Run with highest privileges** and an interactive logon trigger.
3. Sign out and sign back in, or reboot.
4. Confirm no UAC prompt appears.
5. Confirm both processes start and have the expected integrity levels.
6. Repeat the hotkey, elevated-window, runner, settings, and restore checks above.
7. Uninstall and confirm the Startup shortcut and `RootlessWM.WindowManager` scheduled task are removed.

## Done

- Window management lives in a separate executable.
- Only `RootlessWM.WindowManager.exe` is elevated.
- Normal startup has no UAC prompt.
- A small allowlisted named-pipe protocol carries management commands.
- The helper cannot launch applications through IPC.
- Existing window-management behavior still works.
