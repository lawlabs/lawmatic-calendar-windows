#Requires -Version 5.1
[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $OutDir = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $OutDir) {
    $OutDir = Join-Path $RepoRoot "artifacts\unpackaged\$Runtime"
}
if ($Runtime -ne "win-x64") {
    throw "The Inno Setup build currently supports win-x64 only."
}

$Project = Join-Path $RepoRoot "src\LawMatic.Calendar\LawMatic.Calendar.csproj"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

dotnet publish $Project `
    -c $Configuration `
    -r $Runtime `
    -p:Platform=x64 `
    -p:PublishUnpackaged=true `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=false `
    -o $OutDir `
    --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

foreach ($file in @("LawMatic.Calendar.exe", "Kalends.WinUI.dll", "Kalends.WinUI.pri", "CourtTimeline.WinUI.dll", "Microsoft.UI.Xaml.dll", "coreclr.dll")) {
    if (-not (Test-Path (Join-Path $OutDir $file))) {
        throw "Published application is missing $file"
    }
}

Write-Host "Unpackaged application: $OutDir"
