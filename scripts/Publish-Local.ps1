[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\artifacts\publish")
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "..\src\RootlessWM\RootlessWM.csproj"
$resolvedOutputPath = [IO.Path]::GetFullPath($OutputPath)

if (Test-Path $resolvedOutputPath) {
    Remove-Item $resolvedOutputPath -Recurse -Force
}

& dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $resolvedOutputPath

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published RootlessWM to $resolvedOutputPath"
