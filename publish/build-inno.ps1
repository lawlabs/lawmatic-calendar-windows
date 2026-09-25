#Requires -Version 5.1
[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $Version = "",
    [switch] $SkipPublish
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$PublishDir = Join-Path $RepoRoot "artifacts\unpackaged\win-x64"
$OutDir = Join-Path $RepoRoot "artifacts\inno"
$Project = Join-Path $RepoRoot "src\LawMatic.Calendar\LawMatic.Calendar.csproj"
$Icon = Join-Path $RepoRoot "src\LawMatic.Calendar\Assets\AppIcon.ico"
$Iss = Join-Path $PSScriptRoot "LawMaticCalendar.iss"

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 (ISCC.exe) is required." }

if (-not $Version) {
    [xml] $projectXml = Get-Content -LiteralPath $Project -Raw
    $Version = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version').InnerText.Trim()
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Expected a three-part version (for example 1.0.1), got '$Version'."
}

if (-not $SkipPublish) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "unpackaged.ps1") `
        -Configuration $Configuration -OutDir $PublishDir
    if ($LASTEXITCODE -ne 0) { throw "Unpackaged publish failed." }
}

foreach ($file in @("LawMatic.Calendar.exe", "Kalends.WinUI.dll", "Kalends.WinUI.pri", "CourtTimeline.WinUI.dll", "Microsoft.UI.Xaml.dll", "coreclr.dll")) {
    if (-not (Test-Path (Join-Path $PublishDir $file))) { throw "Missing published file: $file" }
}
if (-not (Test-Path $Icon)) { throw "Missing application icon: $Icon" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
& $iscc "/DMyAppVersion=$Version" "/DPublishDir=$PublishDir" "/DOutDir=$OutDir" "/DIconPath=$Icon" $Iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$Setup = Join-Path $OutDir "LawMaticCalendar-Setup-$Version-x64.exe"
if (-not (Test-Path $Setup)) { throw "Installer was not created: $Setup" }
Write-Host "Installer: $Setup"
