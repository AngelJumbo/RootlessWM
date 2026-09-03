[CmdletBinding()]
param(
    [switch]$SkipPublish,
    [switch]$NoStartup,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$installRoot = Join-Path $env:LOCALAPPDATA "RootlessWM"
$appPath = Join-Path $installRoot "app"
$publishPath = Join-Path $PSScriptRoot "..\artifacts\publish"
$executablePath = Join-Path $appPath "RootlessWM.exe"
$startupPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\RootlessWM.lnk"

if (Get-Process -Name "RootlessWM" -ErrorAction SilentlyContinue) {
    throw "RootlessWM is running. Exit it from the tray before installing an update."
}

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "Publish-Local.ps1")
}

if (-not (Test-Path (Join-Path $publishPath "RootlessWM.exe"))) {
    throw "Publish output is missing. Run Publish-Local.ps1 or omit -SkipPublish."
}

New-Item -ItemType Directory -Path $appPath -Force | Out-Null
Get-ChildItem -Path $appPath -Force | Remove-Item -Recurse -Force
Copy-Item -Path (Join-Path $publishPath "*") -Destination $appPath -Recurse -Force

if ($NoStartup) {
    Remove-Item $startupPath -Force -ErrorAction SilentlyContinue
}
else {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($startupPath)
    $shortcut.TargetPath = $executablePath
    $shortcut.Arguments = "--manage --no-logs"
    $shortcut.WorkingDirectory = $appPath
    $shortcut.Description = "Start RootlessWM window manager"
    $shortcut.Save()
}

Write-Host "Installed RootlessWM to $appPath"
Write-Host "Settings and managed-window state remain in $installRoot"

if (-not $NoLaunch) {
    Start-Process -FilePath $executablePath -ArgumentList "--manage --no-logs" -WorkingDirectory $appPath
}
