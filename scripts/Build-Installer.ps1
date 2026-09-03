[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [string]$InnoCompilerPath
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$publishScript = Join-Path $repoRoot "scripts\Publish-Local.ps1"
$installerScript = Join-Path $repoRoot "installer\RootlessWM.iss"

if (-not $InnoCompilerPath) {
    $InnoCompilerPath = @(
        (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

if (-not $InnoCompilerPath -or -not (Test-Path $InnoCompilerPath)) {
    throw "Inno Setup was not found. Install it or pass -InnoCompilerPath <path to ISCC.exe>."
}

& $publishScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

$env:ROOTLESSWM_VERSION = $Version
& $InnoCompilerPath $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed with exit code $LASTEXITCODE."
}

$outputPath = Join-Path $repoRoot "artifacts\installer\RootlessWM-Setup.exe"
Write-Host "Built installer: $outputPath"