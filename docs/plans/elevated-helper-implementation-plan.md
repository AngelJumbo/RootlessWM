# Elevated Helper Implementation Plan

## Goal

Run the main RootlessWM process normally and keep a separate window-management helper permanently elevated without UAC popups during normal use.

The helper manages windows only. It must never launch applications or execute commands.

## Target Architecture

```text
RootlessWM.exe
Normal integrity
Settings, hotkeys, bar, runner, launches
        |
        | authenticated named pipe
        v
RootlessWM.WindowManager.exe
High integrity
Window discovery, hooks, tiling, focus, workspaces, restore
```

The helper starts at user logon through a scheduled task configured with `/RL HIGHEST`. UAC consent is requested once when the task is created or updated by the installer, not every time RootlessWM starts.

## Required Changes

### 1. Create the helper project

Add `src/RootlessWM.WindowManager` as a separate executable.

Move the management host from `WmApplication` into the helper:

- `WindowEnumerator`
- `WindowInspector`
- `WindowEligibilityClassifier`
- `WindowTracker`
- `WindowEventSource`
- `MouseFocusSource`
- `TilingState`
- `WindowTiler`
- `MonitorCatalog`
- `MonitorOwnership`
- `WorkspaceState`
- `FullscreenState`
- `WorkspaceVisibilitySynchronizer`
- `Win32WindowCommander`
- `WindowRestorer`
- `ExplorerVisibilityController`

The helper owns all live window state and all Win32 operations against managed windows.

### 2. Keep RootlessWM normal

Modify [WmApplication.cs](../src/RootlessWM/App/WmApplication.cs):

- Keep settings loading and reload.
- Keep global hotkey registration.
- Keep workspace bar.
- Keep runner behavior.
- Keep configured application launching.
- Remove direct window-management object ownership.
- Replace tiling/workspace/focus calls with named-pipe commands.
- Render status from helper state snapshots.

Modify [Program.cs](../src/RootlessWM/Program.cs) only if a client-specific startup mode is needed.

### 3. Remove de-elevation launching (DONE)

Delete `ShellProcessLauncher.cs`.

Modify [RunnerController.cs](../src/RootlessWM/App/RunnerController.cs):

- Use normal `Process.Start` with `UseShellExecute = true`.

Modify [WmApplication.cs](../src/RootlessWM/App/WmApplication.cs):

- Normal launch entries use ordinary `Process.Start`.
- `admin = true` sets `Verb = "runas"`.
- No launch request is sent to the helper.

### 4. Add shared IPC contracts

Create a shared protocol area or project containing:

- command DTOs;
- response DTOs;
- state snapshot DTOs;
- protocol version;
- request IDs;
- state revisions;
- stable error codes.

Allow only explicit commands such as:

- `ApplyManagementOptions`;
- `ExecuteTilingCommand`;
- `ToggleExplorerVisibility`;
- `SetMouseFocusSuspended`;
- `SetSessionLocked`;
- `Resync`;
- `RequestSnapshot`;
- `Shutdown`.

Do not include executable paths, shell text, `Process.Start`, or arbitrary native calls.

### 5. Implement secure named-pipe IPC

Add a named-pipe server to the elevated helper and a client to RootlessWM.

Use a per-user pipe name containing the current user SID:

```text
\\.\pipe\RootlessWM.<user-sid>
```

Configure an explicit ACL allowing only:

- the current user;
- SYSTEM if required by the scheduled-task model.

On connection:

1. Exchange protocol version, user SID, session ID, process ID, and nonce.
2. Verify the client token SID and interactive session.
3. Reject mismatches.
4. Enforce bounded message sizes and timeouts.
5. Close the connection on malformed or unauthorized requests.

The helper publishes complete snapshots with monotonically increasing revisions.

### 6. Move event and focus handling

The helper owns:

- WinEvent hooks;
- low-level mouse hook;
- delayed window eligibility rechecks;
- focus and foreground operations;
- lock/unlock handling;
- display and monitor resynchronization.

The normal process sends `SetMouseFocusSuspended` while the runner is visible, so focus-follows-mouse cannot steal runner focus.

After helper startup, unlock, display changes, reconnect, or event loss, perform a full window enumeration and publish a new snapshot.

### 7. Define disconnect behavior

When the pipe disconnects:

- RootlessWM marks management unavailable.
- The helper stops accepting commands.
- RootlessWM reconnects with bounded backoff.
- The helper sends a full snapshot after reconnection.
- The helper follows one explicit policy: keep windows managed or restore them before exit.

Do not apply stale client state after reconnecting.

### 8. Change installer startup

Modify [RootlessWM.iss](../installer/RootlessWM.iss):

- Install both executables.
- Start normal RootlessWM from the user Startup folder or a normal user task.
- Create a separate scheduled task named `RootlessWM.WindowManager`.
- Run the helper task at logon with `/RL HIGHEST` and `/IT`.
- Configure the task to run only in the current user session.
- Request elevation when the installer creates or updates the helper task.
- Do not run the main RootlessWM task elevated.
- Remove the old monolithic `RootlessWM` elevated task during upgrade.
- Remove the helper task during uninstall.

The helper task is the only permanently elevated component. RootlessWM startup must not use `runas` or show a UAC prompt.

### 9. Move persistence ownership

Keep `%LOCALAPPDATA%\\RootlessWM` files owned and written by the normal process.

The helper sends validated managed-window and workspace snapshots to RootlessWM. RootlessWM writes:

- `managed-windows.json`;
- `workspaces.json`.

On startup, RootlessWM sends validated recovery state to the helper. The helper remains authoritative for live HWND state.

### 10. Update solution and build scripts

Modify:

- `RootlessWM.sln` to include the helper and shared project;
- `Directory.Build.props` if shared build settings are required;
- publish scripts to publish both executables;
- installer inputs to include both outputs;
- README and development documentation with the two-process startup model.

## Implementation Order

1. Add shared protocol contracts and tests.
2. Add named-pipe transport and authentication tests.
3. Extract the current management loop behind an interface.
4. Create the helper executable using the extracted management loop.
5. Add the normal-process IPC client.
6. Replace direct management calls in `WmApplication` with IPC.
7. Restore normal `Process.Start` behavior and delete `ShellProcessLauncher`.
8. Move persistence coordination to the normal process.
9. Update installer tasks and upgrade cleanup.
10. Test normal integrity, elevated windows, helper restart, lock/unlock, monitor changes, and `admin = true`.

## Completion Criteria

- RootlessWM runs at normal integrity.
- The helper is always elevated through its separate scheduled task.
- RootlessWM startup shows no UAC prompt.
- Runner applications start normally.
- Launch entries start normally unless `admin = true`.
- Elevated Task Manager and similar windows can be tiled and managed.
- The helper cannot launch applications or execute commands.
- Unauthorized users and sessions cannot connect to the pipe.
- Helper restart produces a full synchronized state.

## Status

 Steps 1–2 of the plan's implementation order are done and verified: solution builds clean, and all 250 tests pass (240
  pre-existing + 10 new files covering protocol contracts and pipe transport/auth).

  What was added:
  • src/RootlessWM.Protocol (new project, net9.0-windows, added to RootlessWM.sln): command/response DTOs (ManagementCommand
     + 8 concrete commands), ManagementStateSnapshot/ManagedWindowSnapshot, ProtocolErrorCode, RequestId, StateRevision,
    source-generated JSON context as an explicit wire-type whitelist.
  • RootlessWM.Protocol/Transport: SID-based pipe naming, ACL factory (owner + optional SYSTEM only), handshake DTOs,
    ManagementPipeAuthenticator (version/SID/session checks), bounded length-prefixed framing (PipeFrameIo) with size and
    cancellation handling.
  • 10 new test files under tests/RootlessWM.Tests covering serialization round-trips, error-code stability, ACL grants,
    authenticator rejection paths, frame bounds/truncation/cancellation, and a real duplex-pipe handshake integration test.

  Not yet touched: WmApplication.cs, RunnerController.cs, the helper executable project, or installer/RootlessWM.iss — those
  are steps 3 onward in the plan's implementation order.

● ──────────────────────────────────────────────────
  Implemented and verified steps 1–2 of docs/plans/elevated-helper-implementation-plan.md ("Add shared protocol contracts
  and tests" and "Add named-pipe transport and authentication tests"):

  New project src/RootlessWM.Protocol (added to RootlessWM.sln):
  • Command/response DTOs: ManagementCommand (polymorphic base) + 8 concrete commands (ApplyManagementOptions,
    ExecuteTilingCommand, ToggleExplorerVisibility, SetMouseFocusSuspended, SetSessionLocked, Resync, RequestSnapshot,
    Shutdown), ManagementResponse/CommandAckResponse/SnapshotResponse.
  • State types: ManagementStateSnapshot, ManagedWindowSnapshot, WindowBoundsSnapshot, StateRevision, RequestId,
    ProtocolVersion, ProtocolErrorCode (stable numeric wire codes).
  • Source-generated JSON context as an explicit whitelist of what can cross the pipe — no arbitrary types, no
    Process.Start/shell text anywhere in this project, matching the plan's constraint.
  • Transport/ subfolder: per-user SID-based pipe naming, an ACL factory granting only the owning user (+ optional SYSTEM),
    a handshake protocol (version/SID/session/PID/nonce) with ManagementPipeAuthenticator enforcing rejection of version,
    SID, and session mismatches, and bounded length-prefixed frame I/O (PipeFrameIo) with size-limit and cancellation
    handling.

  Tests: 10 new test files added to tests/RootlessWM.Tests (serialization round-trips, error-code stability, ACL rule
  checks, authenticator accept/reject cases, frame truncation/oversize/cancellation, and a real anonymous-pipe handshake
  integration test).

  Verified: dotnet build RootlessWM.sln succeeds with 0 warnings/errors; dotnet test reports 250/250 passing.

  Not started yet (steps 3+ of the plan): extracting the management loop, creating the RootlessWM.WindowManager helper
  executable, the IPC client in WmApplication, restoring normal Process.Start/deleting ShellProcessLauncher, and the
  installer/scheduled-task changes. Let me know if you'd like me to continue with the next step (extracting the management
  loop behind an interface).