[CmdletBinding()]
param(
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"

$installRoot = Join-Path $env:LOCALAPPDATA "RootlessWM"
$appPath = Join-Path $installRoot "app"
$startupPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\RootlessWM.lnk"

if (Get-Process -Name "RootlessWM" -ErrorAction SilentlyContinue) {
    throw "RootlessWM is running. Exit it from the tray before uninstalling."
}

Remove-Item $startupPath -Force -ErrorAction SilentlyContinue
Remove-Item $appPath -Recurse -Force -ErrorAction SilentlyContinue

if ($RemoveData) {
    Remove-Item $installRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Removed RootlessWM binaries and startup registration."
if (-not $RemoveData) {
    Write-Host "Settings and state were preserved in $installRoot"
}
