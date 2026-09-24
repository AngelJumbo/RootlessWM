#define AppName "RootlessWM"
#define AppVersion GetEnv("ROOTLESSWM_VERSION")
#if AppVersion == ""
  #define AppVersion "0.1.0"
#endif
#define AppPublisher "RootlessWM"
#define AppExeName "RootlessWM.exe"
#define WindowManagerExeName "RootlessWM.WindowManager.exe"
#define PublishDir AddBackslash(SourcePath) + "..\artifacts\publish"
#define ExampleSettingsPath AddBackslash(SourcePath) + "..\settings.example.toml"

[Setup]
AppId={{B7A5A4B8-5EE6-4C02-AF0A-6D5B5E1D9C8F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
SetupIconFile=..\assets\RootlessWM.ico
DefaultDirName={localappdata}\RootlessWM\app
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\artifacts\installer
OutputBaseFilename=RootlessWM-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ExampleSettingsPath}"; DestDir: "{localappdata}\RootlessWM"; DestName: "settings.toml"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\RootlessWM"; Filename: "{app}\{#AppExeName}"; Parameters: "--manage --no-logs"; WorkingDir: "{app}"

[InstallDelete]
Type: files; Name: "{userstartup}\RootlessWM.lnk"

[Code]
const
  ManagerTaskName = 'RootlessWM.Manager';
  WindowManagerTaskName = 'RootlessWM.WindowManager';

procedure LaunchManager();
var
  ResultCode: Integer;
begin
  ShellExec(
    'open',
    ExpandConstant('{sys}\schtasks.exe'),
    '/Run /TN "' + ManagerTaskName + '"',
    '',
    SW_HIDE,
    ewNoWait,
    ResultCode
  );
end;

function QuotePowerShellLiteral(Value: String): String;
begin
  StringChangeEx(Value, '''', '''''', True);
  Result := '''' + Value + '''';
end;

procedure CreateScheduledTaskFor(TaskName, ExeName: String; Elevated: Boolean);
var
  Parameters: String;
  ResultCode: Integer;
  UserName: String;
  ExePath: String;
  PowerShellCommand: String;
  RunLevelArg: String;
  FriendlyName: String;
begin
  if Elevated then
  begin
    RunLevelArg := '/RL HIGHEST ';
    FriendlyName := 'elevated RootlessWM window-manager';
  end
  else
  begin
    RunLevelArg := '/RL LIMITED ';
    FriendlyName := 'RootlessWM';
  end;

  UserName := GetUserNameString();
  ExePath := ExpandConstant('{app}\' + ExeName);
  Parameters :=
    '/Create ' +
    '/TN "' + TaskName + '" ' +
    '/TR ""' + ExePath + '" --manage --no-logs" ' +
    '/SC ONLOGON ' +
    '/RU "' + UserName + '" ' +
    '/IT ' +
    RunLevelArg +
    '/F';

  if not ShellExec(
    'runas',
    ExpandConstant('{sys}\schtasks.exe'),
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode
  ) then
  begin
    RaiseException('Unable to create the ' + FriendlyName + ' task.');
  end;

  if ResultCode <> 0 then
  begin
    RaiseException(
      'Unable to create the ' + FriendlyName + ' task.' +
      Chr(13) + Chr(10) + Chr(13) + Chr(10) +
      'schtasks exit code: ' + IntToStr(ResultCode)
    );
  end;

  { Configure power settings }
  PowerShellCommand :=
    '-NoProfile -Command ' +
    '"$settings = New-ScheduledTaskSettingsSet ' +
    '-AllowStartIfOnBatteries ' +
    '-DontStopIfGoingOnBatteries ' +
    '-MultipleInstances IgnoreNew ' +
    '-ExecutionTimeLimit ([TimeSpan]::Zero); ' +
    'Set-ScheduledTask -TaskName ''' + TaskName + ''' ' +
    '-Settings $settings"';

  if not ShellExec(
    'runas',
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    PowerShellCommand,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode
  ) then
  begin
    RaiseException(
      'Unable to execute PowerShell while configuring the ' + FriendlyName + ' startup task.'
    );
  end;

  if ResultCode <> 0 then
  begin
    RaiseException(
      'Unable to configure the ' + FriendlyName + ' startup task power settings.' +
      Chr(13) + Chr(10) + Chr(13) + Chr(10) +
      'PowerShell exit code: ' + IntToStr(ResultCode)
    );
  end;

end;

procedure CreateScheduledTasks();
begin
  CreateScheduledTaskFor(ManagerTaskName, '{#AppExeName}', False);
  CreateScheduledTaskFor(WindowManagerTaskName, '{#WindowManagerExeName}', True);
end;

procedure DeleteScheduledTaskNamed(TaskName: String);
var
  Parameters: String;
  ResultCode: Integer;
begin
  Parameters := '/Delete /TN "' + TaskName + '" /F';
  if ShellExec(
    'runas',
    ExpandConstant('{sys}\schtasks.exe'),
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode
  ) then
  begin
    Log('Scheduled task removal exit code for ' + TaskName + ': ' + IntToStr(ResultCode));
  end;
end;

procedure DeleteScheduledTasks();
begin
  DeleteScheduledTaskNamed(ManagerTaskName);
  DeleteScheduledTaskNamed(WindowManagerTaskName);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    CreateScheduledTasks();
    if not WizardSilent then
      LaunchManager();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DeleteScheduledTasks();
  end;
end;