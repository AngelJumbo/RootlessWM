#define AppName "RootlessWM"
#define AppVersion GetEnv("ROOTLESSWM_VERSION")
#if AppVersion == ""
  #define AppVersion "0.1.0"
#endif
#define AppPublisher "RootlessWM"
#define AppExeName "RootlessWM.exe"
#define PublishDir AddBackslash(SourcePath) + "..\\artifacts\\publish"

[Setup]
AppId={{B7A5A4B8-5EE6-4C02-AF0A-6D5B5E1D9C8F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\RootlessWM\app
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=RootlessWM-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "startupadmin"; Description: "Start as administrator (allows tiling elevated apps such as Task Manager)"; GroupDescription: "Startup options:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\RootlessWM"; Filename: "{app}\{#AppExeName}"; Parameters: "--manage --no-logs"; WorkingDir: "{app}"
Name: "{userstartup}\RootlessWM"; Filename: "{app}\{#AppExeName}"; Parameters: "--manage --no-logs"; WorkingDir: "{app}"; Tasks: not startupadmin

[InstallDelete]
Type: files; Name: "{userstartup}\RootlessWM.lnk"

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--manage --no-logs"; WorkingDir: "{app}"; Description: "Launch RootlessWM"; Flags: nowait postinstall skipifsilent unchecked; Tasks: not startupadmin
Filename: "{sys}\schtasks.exe"; Parameters: "/Run /TN ""RootlessWM"""; Description: "Launch RootlessWM"; Flags: runhidden postinstall skipifsilent unchecked; Tasks: startupadmin

[Code]
const
  ScheduledTaskName = 'RootlessWM';

function IsScheduledTaskInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
    ExpandConstant('{sys}\schtasks.exe'),
    '/Query /TN "' + ScheduledTaskName + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure RemoveScheduledTask();
var
  ResultCode: Integer;
begin
  if not IsScheduledTaskInstalled() then
    Exit;

  if not ShellExec(
    'runas',
    ExpandConstant('{sys}\schtasks.exe'),
    '/Delete /TN "' + ScheduledTaskName + '" /F',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    RaiseException('Unable to remove the existing elevated RootlessWM startup task.');
end;

procedure CreateScheduledTask();
var
  Parameters: String;
  ResultCode: Integer;
begin
  Parameters :=
    '/Create /TN "' + ScheduledTaskName + '" ' +
    '/TR """' + ExpandConstant('{app}\{#AppExeName}') + '"" --manage --no-logs" ' +
    '/SC ONLOGON /IT /RL HIGHEST /F';

  if not ShellExec(
    'runas',
    ExpandConstant('{sys}\schtasks.exe'),
    Parameters,
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
    RaiseException('Unable to create the elevated RootlessWM startup task.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    Exit;

  if WizardIsTaskSelected('startupadmin') then
    CreateScheduledTask()
  else
    RemoveScheduledTask();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveScheduledTask();
end;