#Requires -Version 7
<#
.SYNOPSIS
    Builds ScreenBugs-Setup-<version>.exe from a clean tree.
.DESCRIPTION
    Tests, publishes self-contained win-x64, then compiles the NSIS installer.
    See docs/superpowers/specs/2026-09-04-installer-design.md section 6.
#>
[CmdletBinding()]
param(
    # Skips dotnet test. For iterating on the installer script only.
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $repo 'build/publish'
$assets = Join-Path $repo 'assets'

function Invoke-Step {
    param([string] $Name, [scriptblock] $Action)

    Write-Host "==> $Name" -ForegroundColor Cyan

    # Reset first: under Set-StrictMode -Version Latest, reading $LASTEXITCODE when nothing has
    # set it is a terminating error, so a step whose action runs no native command would fail
    # here rather than at whatever actually went wrong.
    $global:LASTEXITCODE = 0
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

# makensis is not on PATH by default.
$makensis = (Get-Command 'makensis.exe' -ErrorAction SilentlyContinue)?.Source
if (-not $makensis) {
    $makensis = Join-Path ${env:ProgramFiles(x86)} 'NSIS/makensis.exe'
}
if (-not (Test-Path $makensis)) {
    throw "makensis.exe not found. Install NSIS 3 from https://nsis.sourceforge.io and re-run."
}

foreach ($asset in 'ScreenBugs.ico', 'wizard-side.bmp', 'wizard-header.bmp') {
    if (-not (Test-Path (Join-Path $assets $asset))) {
        throw "Missing asset '$asset'. Run: dotnet run --project tools/IconGen"
    }
}

if (-not $SkipTests) {
    Invoke-Step 'Testing' { dotnet test (Join-Path $repo 'ScreenBugs.slnx') -c Release --nologo -v q }
}

# Cleared first, so a file removed from the project cannot survive into the payload.
if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

Invoke-Step 'Publishing (self-contained win-x64)' {
    dotnet publish (Join-Path $repo 'src/ScreenBugs/ScreenBugs.csproj') `
        -c Release -r win-x64 --self-contained true `
        -p:PublishReadyToRun=true -p:SatelliteResourceLanguages=en `
        -o $publish --nologo -v q
}

if (-not (Test-Path (Join-Path $publish 'ScreenBugs.exe'))) {
    throw "Publish did not produce ScreenBugs.exe in $publish."
}

# One version source for the app, the installer and the Add/Remove Programs entry.
$version = (dotnet msbuild (Join-Path $repo 'Directory.Build.props') -getProperty:Version -nologo).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Could not read a three-part version from Directory.Build.props (got '$version')."
}

$output = Join-Path $repo "build/ScreenBugs-Setup-$version.exe"
Invoke-Step "Compiling installer $version" {
    & $makensis -V2 `
        "-DVERSION=$version" `
        "-DASSETS_DIR=$assets" `
        "-DPUBLISH_DIR=$publish" `
        "-DOUT_FILE=$output" `
        (Join-Path $repo 'installer/ScreenBugs.nsi')
}

$size = [math]::Round((Get-Item $output).Length / 1MB, 1)
$files = (Get-ChildItem $publish -Recurse -File).Count
Write-Host ""
Write-Host "Built $output" -ForegroundColor Green
Write-Host "  $size MB, from $files published files"
