#Requires -Version 7
<#
.SYNOPSIS
    Verifies the installer's options reach the seed file, and that uninstall cleans up.
.DESCRIPTION
    Installs per-user into a temporary directory, asserts, uninstalls, asserts again.
    See docs/superpowers/specs/2026-09-04-installer-design.md section 7.2.
#>
[CmdletBinding()]
param(
    [string] $Setup
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = Split-Path -Parent $PSScriptRoot
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs'
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Screen Bugs.lnk'
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Screen Bugs.lnk'
$failures = 0

function Test-Claim {
    param([string] $Claim, [bool] $Condition, [string] $Detail = '')

    if ($Condition) {
        Write-Host "  PASS  $Claim" -ForegroundColor Green
    }
    else {
        Write-Host "  FAIL  $Claim" -ForegroundColor Red
        if ($Detail) { Write-Host "        $Detail" -ForegroundColor DarkGray }
        $script:failures++
    }
}

if (-not $Setup) {
    $Setup = Get-ChildItem (Join-Path $repo 'build') -Filter 'ScreenBugs-Setup-*.exe' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Setup -or -not (Test-Path $Setup)) {
    throw "No setup executable found. Run build/build-installer.ps1 first, or pass -Setup <path>."
}

# The production key is used below, so a real per-user install would be clobbered.
if (Test-Path $uninstallKey) {
    throw 'Uninstall your existing per-user Screen Bugs first: this script writes the same registry key and would delete its Add/Remove Programs entry.'
}

# Every case below ends in an uninstall, and the uninstaller deletes HKCU Run "ScreenBugs"
# unconditionally -- by design, but it does not know the value belongs to your own dev build
# rather than to the installation being removed. Save it and put it back at the end.
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$savedRunValue = (Get-ItemProperty $runKey -Name ScreenBugs -ErrorAction SilentlyContinue).ScreenBugs
if ($savedRunValue) {
    Write-Host "Saved your Run value; it will be restored at the end." -ForegroundColor DarkGray
}

$expectedVersion = (dotnet msbuild (Join-Path $repo 'Directory.Build.props') -getProperty:Version -nologo).Trim()
if ($expectedVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Could not read a three-part version from Directory.Build.props (got '$expectedVersion')."
}

# Start-Process quotes any argument containing a space, which would break the /D= rule below.
if ([System.IO.Path]::GetTempPath() -match ' ') {
    throw 'The temp path contains a space, which Start-Process would quote and /D= cannot accept. Set TEMP to a path without spaces and re-run.'
}

function Invoke-Case {
    param(
        [string] $Name,
        [string[]] $Switches,
        [hashtable] $ExpectedSeed,
        [bool] $ExpectDesktopShortcut
    )

    Write-Host ""
    Write-Host "== $Name" -ForegroundColor Cyan

    # A fresh directory per case, and /D= must be LAST and UNQUOTED even with spaces in the
    # path -- quoting it is the standard trip-up.
    $target = Join-Path ([System.IO.Path]::GetTempPath()) "sb-verify-$(Get-Random)"
    $arguments = @('/S', '/CURRENTUSER') + $Switches + @("/D=$target")

    $process = Start-Process -FilePath $Setup -ArgumentList $arguments -Wait -PassThru
    Test-Claim 'setup exits 0' ($process.ExitCode -eq 0) "exit code $($process.ExitCode)"

    Test-Claim 'ScreenBugs.exe is installed' (Test-Path (Join-Path $target 'ScreenBugs.exe'))

    $fileCount = if (Test-Path $target) { (Get-ChildItem $target -Recurse -File).Count } else { 0 }
    Test-Claim 'the self-contained payload shipped (>100 files)' ($fileCount -gt 100) "$fileCount files"

    $seedPath = Join-Path $target 'install-defaults.json'
    if (Test-Path $seedPath) {
        $seed = Get-Content $seedPath -Raw | ConvertFrom-Json
        Test-Claim "seed type is $($ExpectedSeed.Type)" ($seed.TypeSlots[0].Type -eq $ExpectedSeed.Type) "got $($seed.TypeSlots[0].Type)"
        Test-Claim 'seed holds exactly one slot' ($seed.TypeSlots.Count -eq 1)
        Test-Claim 'seed speed is 1' ($seed.TypeSlots[0].Speed -eq 1)
        Test-Claim "seed count is $($ExpectedSeed.BugCount)" ($seed.BugCount -eq $ExpectedSeed.BugCount) "got $($seed.BugCount)"
        Test-Claim 'seed frame rate is 60' ($seed.FrameRate -eq 60)
        Test-Claim 'seed type-change is RespawnAll' ($seed.OnTypeChange -eq 'RespawnAll')
        Test-Claim "seed StartAtLogin is $($ExpectedSeed.StartAtLogin)" ($seed.StartAtLogin -eq $ExpectedSeed.StartAtLogin) "got $($seed.StartAtLogin)"
    }
    else {
        Test-Claim 'install-defaults.json was written' $false
    }

    if (Test-Path $uninstallKey) {
        $arp = Get-ItemProperty $uninstallKey
        Test-Claim 'ARP DisplayName' ($arp.DisplayName -eq 'Screen Bugs')
        Test-Claim "ARP DisplayVersion is $expectedVersion" ($arp.DisplayVersion -eq $expectedVersion) "got $($arp.DisplayVersion)"
        Test-Claim 'ARP InstallLocation points at the install' ($arp.InstallLocation -eq $target)
        Test-Claim 'ARP UninstallString' ($arp.UninstallString -eq "`"$target\Uninstall.exe`"")
        Test-Claim 'ARP QuietUninstallString' ($arp.QuietUninstallString -eq "`"$target\Uninstall.exe`" /S")
    }
    else {
        Test-Claim 'the uninstall key was written' $false
    }

    Test-Claim 'Start Menu shortcut exists' (Test-Path $startMenu)
    Test-Claim "desktop shortcut $(if ($ExpectDesktopShortcut) { 'exists' } else { 'does not exist' })" `
        ((Test-Path $desktop) -eq $ExpectDesktopShortcut)

    # _?= is required. Without it the launched process relocates itself to $TEMP and returns
    # immediately, so everything below would race an uninstall still in progress.
    $uninstaller = Join-Path $target 'Uninstall.exe'
    if (Test-Path $uninstaller) {
        $u = Start-Process -FilePath $uninstaller -ArgumentList '/S', "_?=$target" -Wait -PassThru
        Test-Claim 'uninstall exits 0' ($u.ExitCode -eq 0) "exit code $($u.ExitCode)"

        # _?= also stops the uninstaller deleting its own file, so this is the expected end state.
        Test-Claim 'ScreenBugs.exe is gone' (-not (Test-Path (Join-Path $target 'ScreenBugs.exe')))
        Test-Claim 'install-defaults.json is gone' (-not (Test-Path $seedPath))
        # Read the names through the pipeline, not as a property of the collection. Two traps
        # this avoids, both of which bite on the *expected* outcomes: `(Get-ChildItem ...).Name`
        # on an empty or missing directory accesses a property of $null, which throws
        # PropertyNotFoundException under Set-StrictMode and is fatal here because
        # $ErrorActionPreference is Stop; and with exactly one file left -- the documented
        # success state -- an unwrapped result is a String, so .Count throws and $left[0] would
        # be the character 'U'. ForEach-Object runs per emitted item, so neither can happen.
        $left = @(Get-ChildItem $target -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
        Test-Claim 'only Uninstall.exe is left behind' (($left.Count -eq 0) -or ($left.Count -eq 1 -and $left[0] -eq 'Uninstall.exe')) "left: $($left -join ', ')"
        Test-Claim 'the uninstall key is gone' (-not (Test-Path $uninstallKey))
        Test-Claim 'the Start Menu shortcut is gone' (-not (Test-Path $startMenu))
        Test-Claim 'the desktop shortcut is gone' (-not (Test-Path $desktop))
    }
    else {
        Test-Claim 'Uninstall.exe was written' $false
    }

    Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
}

Invoke-Case -Name 'Explicit options, startup off, desktop shortcut on' `
    -Switches @('/BUGTYPE=HouseSpider', '/BUGCOUNT=12', '/STARTUP=0', '/DESKTOP=1') `
    -ExpectedSeed @{ Type = 'HouseSpider'; BugCount = 12; StartAtLogin = $false } `
    -ExpectDesktopShortcut $true

Invoke-Case -Name 'Random at the minimum count, startup on, no desktop shortcut' `
    -Switches @('/BUGTYPE=Random', '/BUGCOUNT=1', '/STARTUP=1', '/DESKTOP=0') `
    -ExpectedSeed @{ Type = 'Random'; BugCount = 1; StartAtLogin = $true } `
    -ExpectDesktopShortcut $false

Invoke-Case -Name 'Bad switches fall back to the defaults' `
    -Switches @('/BUGTYPE=Wasp', '/BUGCOUNT=999') `
    -ExpectedSeed @{ Type = 'BlackGardenAnt'; BugCount = 50; StartAtLogin = $true } `
    -ExpectDesktopShortcut $false

# No option switches at all. This case exists because its absence hid a real bug: NSIS's
# ${GetOptions} clears its destination variable when the switch is missing, so every default
# set in .onInit was being overwritten, and a plain install seeded BugCount 1 instead of 5.
# Every other case here passes at least one switch, so none of them would catch a regression.
Invoke-Case -Name 'No option switches: the documented defaults' `
    -Switches @() `
    -ExpectedSeed @{ Type = 'BlackGardenAnt'; BugCount = 5; StartAtLogin = $true } `
    -ExpectDesktopShortcut $false

# Put the developer's own Run value back, whatever the outcome above.
if ($savedRunValue) {
    Set-ItemProperty $runKey -Name ScreenBugs -Value $savedRunValue
    Write-Host 'Restored your Run value.' -ForegroundColor DarkGray
}

Write-Host ""
if ($failures -eq 0) {
    Write-Host 'All checks passed.' -ForegroundColor Green
    exit 0
}

Write-Host "$failures check(s) failed." -ForegroundColor Red
exit 1
