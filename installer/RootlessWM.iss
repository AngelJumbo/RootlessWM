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
PrivilegesRequired=lowest
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
Name: "{userstartup}\RootlessWM"; Filename: "{app}\{#AppExeName}"; Parameters: "--manage --no-logs"; WorkingDir: "{app}"

[InstallDelete]
Type: files; Name: "{userstartup}\RootlessWM.lnk"

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--manage --no-logs"; WorkingDir: "{app}"; Description: "Launch RootlessWM"; Flags: nowait postinstall skipifsilent unchecked

[Code]
const
  ScheduledTaskName = 'RootlessWM.WindowManager';

function QuotePowerShellLiteral(Value: String): String;
begin
  StringChangeEx(Value, '''', '''''', True);
  Result := '''' + Value + '''';
end;

procedure CreateScheduledTask();
var
  Parameters: String;
  ResultCode: Integer;
  UserName: String;
  ExePath: String;
  PowerShellCommand: String;
begin
  UserName := GetUserNameString();
  ExePath := ExpandConstant('{app}\{#WindowManagerExeName}');
  Parameters :=
    '/Create ' +
    '/TN "' + ScheduledTaskName + '" ' +
    '/TR ""' + ExePath + '" --manage --no-logs" ' +
    '/SC ONLOGON ' +
    '/RU "' + UserName + '" ' +
    '/IT ' +
    '/RL HIGHEST ' +
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
    RaiseException('Unable to create the elevated RootlessWM window-manager task.');
  end;

  if ResultCode <> 0 then
  begin
    RaiseException(
      'Unable to create the elevated RootlessWM window-manager task.' +
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
    'Set-ScheduledTask -TaskName ''' + ScheduledTaskName + ''' ' +
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
      'Unable to execute PowerShell while configuring the RootlessWM startup task.'
    );
  end;

  if ResultCode <> 0 then
  begin
    RaiseException(
      'Unable to configure the RootlessWM startup task power settings.' +
      Chr(13) + Chr(10) + Chr(13) + Chr(10) +
      'PowerShell exit code: ' + IntToStr(ResultCode)
    );
  end;

end;

procedure DeleteScheduledTask();
var
  Parameters: String;
  ResultCode: Integer;
begin
  Parameters := '/Delete /TN "' + ScheduledTaskName + '" /F';
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
    Log('Scheduled task removal exit code: ' + IntToStr(ResultCode));
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    CreateScheduledTask();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DeleteScheduledTask();
  end;
end;