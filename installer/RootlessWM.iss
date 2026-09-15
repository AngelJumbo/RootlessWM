#define AppName "RootlessWM"
#define AppVersion GetEnv("ROOTLESSWM_VERSION")
#if AppVersion == ""
  #define AppVersion "0.1.0"
#endif
#define AppPublisher "RootlessWM"
#define AppExeName "RootlessWM.exe"
#define PublishDir AddBackslash(SourcePath) + "..\artifacts\publish"
#define ExampleSettingsPath AddBackslash(SourcePath) + "..\settings.example.toml"

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