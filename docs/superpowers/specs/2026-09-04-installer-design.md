# Installer: design spec

Date: 2026-09-04
Status: approved for planning
Builds on: `2026-09-03-bug-options-design.md`. Where the two disagree, this spec
wins; it amends section 5 (persistence) and section 6 (run at Windows startup)
by adding a first-run seeding step ahead of both, and section 10 (application
composition) by replacing the settings load in `App.OnStartup`.

## 1. Overview

Screen Bugs currently ships as a `dotnet build` output folder. This feature adds
`ScreenBugs-Setup-<version>.exe`: an NSIS 3 installer, built with MUI2, wrapping
a self-contained win-x64 publish of the app, that installs either for all users
or for the current user only and collects four basic options during the wizard:

- which bug type to show;
- how many bugs are on screen;
- whether to run at Windows startup;
- whether to create a desktop shortcut.

Those choices reach the app through a **seed file** written into the install
directory, not by the installer writing user settings directly. The app adopts
the seed the first time it runs for a user who has no `settings.json` yet.

The installer also supports fully silent, switch-driven installation, which is
how the option plumbing is verified without driving a GUI.

Runtime: self-contained. There is no .NET prerequisite check, no download step,
and no dependency on the machine having any .NET runtime installed.

Non-goals: code signing (no certificate is available, so SmartScreen will warn
on first download), auto-update, MSI/MSIX/winget packaging, localization,
setting the frame rate or multiple type slots during install, and a license
page (the repository has no licence text).

## 2. Seeding: how install-time options reach the app

### 2.1 Mechanism

The installer writes the chosen options to `install-defaults.json` in the
install directory. The app reads it at startup and, **only when the user has no
`settings.json`**, adopts it: saves those options as the user's settings and, if
the seed asks for it, registers the app under the per-user `Run` key.

```
installer   →  <INSTDIR>\install-defaults.json
app, 1st run →  %LocalAppData%\ScreenBugs\settings.json
             →  HKCU\...\Run\ScreenBugs   (only when the seed says so)
```

The installer never writes `settings.json` and never touches a `Run` key.

### 2.2 Why not have the installer write the user's settings directly

- **Elevation safety.** An all-users install runs elevated. `%LocalAppData%` and
  `HKCU` then resolve to the *elevated* account, which under over-the-shoulder
  elevation is not the person installing the app. Seeding sidesteps this: the
  app applies the seed as whichever user actually runs it.
- **Every user of an all-users install** gets the chosen defaults on their own
  first run, not just whoever ran setup.
- **One owner per concern.** `StartupRegistration` stays the sole owner of the
  `Run` value, so the Options dialog's "Run at Windows startup" checkbox can
  never disagree with the registry. `SettingsStore` stays the sole writer of
  `settings.json`.
- **A reinstall cannot clobber saved settings**, because an existing
  `settings.json` always wins over the seed.

### 2.3 Seed file format

The seed is `settings.json`'s format (section 5 of the options spec) plus one
extra field:

```json
{
  "TypeSlots": [ { "Type": "BlackGardenAnt", "Speed": 1 } ],
  "BugCount": 5,
  "FrameRate": 60,
  "OnTypeChange": "RespawnAll",
  "StartAtLogin": true
}
```

`Type` is a `SpeciesId` name or `Random`, matching what `SettingsSerializer`
already writes and reads. The installer always writes exactly one slot at
speed 1 and `FrameRate` 60 — install time offers no control over slot count,
per-slot speed, frame rate or type-change behaviour.

Reading is total, like the rest of the settings code: a missing, empty, corrupt
or partial seed yields `BugOptions.Default` with `StartAtLogin` false. A missing
or unparseable `StartAtLogin` reads as **false**, so a damaged seed never
registers startup behind the user's back.

## 3. Code changes

### 3.1 `InstallDefaults` (Core)

```csharp
// src/ScreenBugs.Core/Settings/InstallDefaults.cs
/// <summary>The installer's seed: the options to start a new user with, plus whether to run at login.</summary>
public sealed record InstallDefaults(BugOptions Options, bool StartAtLogin)
{
    public const string FileName = "install-defaults.json";

    public static InstallDefaults Default { get; } = new(BugOptions.Default, StartAtLogin: false);

    /// <summary>Total: any input at all yields a valid record.</summary>
    public static InstallDefaults Parse(string json);
}
```

`Parse` delegates the options to `SettingsSerializer.Deserialize`, which is
already total and already clamps `BugCount` and validates `FrameRate` and
species names, then reads `StartAtLogin` from the same JSON with
`JsonNode.Parse` inside a `try`/`catch (JsonException)`. Parsing the string
twice is deliberate: it keeps `SettingsSerializer` unchanged and keeps the
extra field out of `BugOptions`, which describes what the Options dialog
controls and nothing else.

### 3.2 `FirstRunSeed` (Core)

The decision is a pure function, so it can be tested without touching the file
system or the registry — the test project references Core only.

```csharp
// src/ScreenBugs.Core/Settings/SeedOutcome.cs
/// <summary>What to start with, and whether this run should register startup.</summary>
public readonly record struct SeedOutcome(BugOptions Options, bool RegisterStartup);

// src/ScreenBugs.Core/Settings/FirstRunSeed.cs
public static class FirstRunSeed
{
    /// <param name="savedSettingsJson">The user's settings file content, or null when there is no file.</param>
    /// <param name="installDefaultsJson">The installer's seed content, or null when there is no file.</param>
    public static SeedOutcome Decide(string? savedSettingsJson, string? installDefaultsJson);
}
```

Rules, in order:

1. `savedSettingsJson` is not null → `(SettingsSerializer.Deserialize(saved), RegisterStartup: false)`.
   A file that exists but is corrupt still counts as "not first run": it
   deserializes to defaults and the seed is ignored. Startup registration is
   the user's to change from then on.
2. Otherwise `installDefaultsJson` is not null → `InstallDefaults.Parse` and
   return its options and `StartAtLogin`.
3. Otherwise → `(BugOptions.Default, false)`. This is the no-installer case:
   running from a build output folder behaves exactly as it does today.

The caller persists when it read no saved file; `SeedOutcome` deliberately does
not carry a "should save" flag, because the caller is the thing that knows.

### 3.3 `SettingsStore` and `SettingsBootstrap` (app)

`SettingsStore.Load()` is superseded and removed. It is replaced by a raw read,
because the decision now needs to distinguish "no file" from "unreadable file":

```csharp
// src/ScreenBugs/Settings/SettingsStore.cs
public static string FilePath { get; }          // unchanged
public static string? TryRead();                // null when absent or unreadable; logs and returns null on error
public static void Save(BugOptions options);    // unchanged
```

```csharp
// src/ScreenBugs/Settings/SettingsBootstrap.cs
/// <summary>Loads the user's options, seeding them from the installer's defaults on a first run.</summary>
public static class SettingsBootstrap
{
    public static BugOptions Load();
}
```

`Load` reads `SettingsStore.TryRead()` and the seed from
`Path.Combine(AppContext.BaseDirectory, InstallDefaults.FileName)`, calls
`FirstRunSeed.Decide`, and when there was no saved file saves the result and
calls `StartupRegistration.SetEnabled(true)` if `RegisterStartup` is set. Every
file read is wrapped and logged through `CrashLog`, matching `SettingsStore`.

`AppContext.BaseDirectory` is the install directory for a self-contained,
non-single-file publish, so the seed is found next to `ScreenBugs.exe`.

`App.OnStartup` changes one line: `current = SettingsStore.Load();` becomes
`current = SettingsBootstrap.Load();`. It runs where the old load ran, before
the overlay and frame loop are built, so the seeded count and type are in force
from the first frame.

### 3.4 `AntGlyph` (app)

`TrayIconFactory.Draw()` currently draws the ant at hard-coded 32×32
coordinates. The geometry moves out unchanged, parameterized by size and
colour, so the icon generator and the tray share one definition:

```csharp
// src/ScreenBugs/Tray/AntGlyph.cs
/// <summary>The ant the app identifies itself with, drawn at any size.</summary>
public static class AntGlyph
{
    /// <summary>The coordinate space the glyph is drawn in.</summary>
    public const int DesignSize = 32;

    public static Bitmap Draw(int size, Color color);
}
```

`Draw` creates a `size`×`size` transparent bitmap and applies
`ScaleTransform(size / (float)DesignSize, size / (float)DesignSize)` before the
existing draw calls; GDI+ scales the 2px pen with the world transform, so the
glyph keeps its proportions at 16px and at 256px. `TrayIconFactory` keeps its
theme decision and its caching and calls
`AntGlyph.Draw(32, TaskbarIsLight() ? ... : ...)`.

### 3.5 Assembly metadata

A repository-root `Directory.Build.props` becomes the single version source for
the app, the installer and the file properties Windows shows:

```xml
<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <Company>Addam Boord</Company>
    <Product>Screen Bugs</Product>
    <Copyright>Copyright © 2026 Addam Boord</Copyright>
  </PropertyGroup>
</Project>
```

`Company` is what Add/Remove Programs shows as Publisher. `ScreenBugs.csproj`
adds `<ApplicationIcon>..\..\assets\ScreenBugs.ico</ApplicationIcon>` so the
executable, its shortcuts and the Add/Remove Programs entry show the ant
instead of the generic .NET icon.

## 4. Branding assets

`assets/` holds three generated, checked-in files:

| File | Size | Used for |
|---|---|---|
| `ScreenBugs.ico` | 16, 24, 32, 48, 64, 128, 256 px | app icon, setup icon, uninstaller icon, shortcuts, Add/Remove Programs |
| `wizard-side.bmp` | 164×314, 24-bit | MUI2 welcome and finish page panel |
| `wizard-header.bmp` | 150×57, 24-bit | MUI2 inner page header |

They are regenerated by `dotnet run --project tools/IconGen`, a console app
that project-references `ScreenBugs.csproj` to reuse `AntGlyph`. Referencing the
WinExe keeps a single copy of the geometry; the tool joins the solution under a
`/tools/` folder so it cannot rot, and is not part of the installer build.

The file icon uses the dark-theme red, `#D8321F`, at every size: the tray glyph
picks its colour from the current theme, but a file icon cannot, and the red ant
stays legible on both light and dark backgrounds where the near-black one
disappears.

`IconGen` writes the ICO container itself, because `System.Drawing` cannot save
a multi-size icon: a 6-byte `ICONDIR`, one 16-byte `ICONDIRENTRY` per size
(width and height stored as 0 for 256), then each image as a PNG payload. The
BMPs are drawn into `PixelFormat.Format24bppRgb` bitmaps, because NSIS rejects
the alpha-channel BMPs that a 32bpp save produces.

## 5. Installer structure

```
installer/ScreenBugs.nsi        pages, sections, install, uninstall
installer/options-page.nsh      the custom options page and its silent-mode parsing
```

Includes: `MUI2.nsh`, `MultiUser.nsh`, `nsDialogs.nsh`, `LogicLib.nsh`,
`FileFunc.nsh`, `WinVer.nsh`, `x64.nsh` — all stock NSIS 3, no third-party
plugins, so the build needs nothing but a standard NSIS installation.

Compression `lzma` with `SetCompressor /SOLID lzma`, which matters: the payload
is a self-contained publish of roughly 150 MB across ~200 files.

### 5.1 Scope

`MULTIUSER_EXECUTIONLEVEL Highest` with `MULTIUSER_MUI` and
`MULTIUSER_INSTALLMODE_COMMANDLINE`. An administrator is elevated at start and
chooses the scope on the install-mode page; a standard user is not elevated and
gets a current-user install. `MULTIUSER_INSTALLMODE_FUNCTION` points at a
function that sets the default directory per scope:

| Scope | Directory | Uninstall key | Shortcuts |
|---|---|---|---|
| All users | `$PROGRAMFILES64\ScreenBugs` | `HKLM` (64-bit view) | common Start Menu / Desktop |
| Current user | `$LOCALAPPDATA\Programs\ScreenBugs` | `HKCU` | per-user Start Menu / Desktop |

Registry writes use the `SHCTX` root and shortcut paths use `$SMPROGRAMS` and
`$DESKTOP`, both of which follow the shell-var context `MultiUser` sets.
`SetRegView 64` is set in `.onInit` so the all-users uninstall entry lands in
the 64-bit view where Add/Remove Programs looks.

**One trap this creates:** with `SetShellVarContext all`, NSIS's `$LOCALAPPDATA`
resolves to the *common* app-data folder, not the user's. Every place that needs
the current user's `%LocalAppData%\ScreenBugs` — the "existing settings"
detection on the options page, and the optional data removal on uninstall —
must bracket the read with `SetShellVarContext current` and restore the previous
context afterwards.

### 5.2 Pages

1. `MUI_PAGE_WELCOME`
2. `MULTIUSER_PAGE_INSTALLMODE`
3. `MUI_PAGE_DIRECTORY`
4. **Options** — custom, `installer/options-page.nsh`
5. `MUI_PAGE_INSTFILES`
6. `MUI_PAGE_FINISH` with a run checkbox, `MUI_FINISHPAGE_RUN_NOTCHECKED` unset
   (checked by default) and `MUI_FINISHPAGE_RUN_FUNCTION` pointing at the
   un-elevating launcher of section 5.6

Uninstall: `MUI_UNPAGE_CONFIRM`, then a custom one-checkbox page, then
`MUI_UNPAGE_INSTFILES`.

`MUI_HEADERIMAGE`, `MUI_HEADERIMAGE_BITMAP`, `MUI_WELCOMEFINISHPAGE_BITMAP`,
`MUI_UNWELCOMEFINISHPAGE_BITMAP` and `MUI_ICON`/`MUI_UNICON` point at the
section 4 assets. `MUI_ABORTWARNING` on.

### 5.3 The options page

Controls, top to bottom:

| Control | Default |
|---|---|
| Droplist, **Bug type** | Black garden ant |
| Number field with an up-down spinner, **Bugs on screen** (1–50) | 5 |
| Checkbox, **Run Screen Bugs when I sign in to Windows** | checked |
| Checkbox, **Create a desktop shortcut** | unchecked |
| Checkbox, **Replace my current settings with these** | unchecked, and only created when the current user already has a `settings.json` |
| Label, shown with that last checkbox | "You already have saved Screen Bugs settings. These choices otherwise apply only to users who haven't run Screen Bugs yet." |

The droplist lists `Random` first, then the nine species in `SpeciesId` order,
with the same labels the Options dialog uses (`BugTypeChoice.LabelFor`):
Hissing cockroach, Black garden ant, Red fire ant, Praying mantis, Seven-spot
ladybug, Stag beetle, House spider, Centipede, Stink bug. A `BugTypeNameFor`
function maps the selected index to the enum name the seed needs, via an
explicit `${Switch}`; the two lists live next to each other in
`options-page.nsh` so they cannot drift unnoticed.

The count uses `${NSD_CreateNumber}` with an `msctls_updown32` buddy
(`UDS_AUTOBUDDY|UDS_SETBUDDYINT|UDS_ALIGNRIGHT`, range set with `UDM_SETRANGE`)
rather than the trackbar the sketch called for: nsDialogs routes `WM_NOTIFY`
but not the `WM_HSCROLL` a trackbar sends, so a live value label beside a slider
would need dialog subclassing, and a spinner shows its value without one. The
leave function clamps the typed value into 1–50 — the app clamps too, but
clamping here means the written seed says what the installer showed.

The desktop-shortcut checkbox drives the install section directly. A Start Menu
shortcut is always created; a desktop shortcut is opt-in because Screen Bugs
lives in the tray and is rarely relaunched by hand.

### 5.4 Silent install

`.onInit` parses switches with `${GetParameters}` and `${GetOptions}`, so every
option page value has a command-line equivalent and the page is skipped under
`/S`:

| Switch | Effect |
|---|---|
| `/S` | silent |
| `/ALLUSERS`, `/CURRENTUSER` | scope (from `MultiUser`) |
| `/D=<path>` | install directory (NSIS built-in, must be last) |
| `/BUGTYPE=<name>` | `Random` or a `SpeciesId` name |
| `/BUGCOUNT=<1-50>` | bug count |
| `/STARTUP=0\|1` | run at sign-in |
| `/DESKTOP=0\|1` | desktop shortcut |
| `/RESETSETTINGS=1` | delete the current user's `settings.json` so the seed is re-adopted |

Unrecognized `/BUGTYPE` values and out-of-range `/BUGCOUNT` values fall back to
the defaults rather than aborting: a cosmetic option is not worth failing a
deployment over, and `verify-install.ps1` asserts the written seed, so a typo in
a script surfaces there. Uninstall accepts `/S` and `/DELETEDATA=1`.

### 5.5 Install section

In order:

1. Refuse to continue unless `${RunningX64}` and `${AtLeastWin10}`, with a
   message naming the requirement. The payload is win-x64 and WPF on .NET 10
   supports Windows 10 and later.
2. Detect a running instance by opening the app's own single-instance mutex:
   `kernel32::OpenMutex(SYNCHRONIZE, 0, "Local\ScreenBugs.SingleInstance")`
   through `System::Call`; a non-null handle means it is running (close the
   handle immediately). If it is, ask for permission to close it — silently in
   `/S` mode — then `nsExec::ExecToStack 'taskkill /F /IM ScreenBugs.exe'`.
   The mutex is in the session-local namespace, so an elevated installer in the
   same session still sees it. An instance running under a *different* user is
   invisible here and will instead lock the files; NSIS's standard retry prompt
   covers that.
3. Handle a prior install, per section 5.7.
4. `SetOutPath $INSTDIR` and `File /r` the publish output.
5. Write `install-defaults.json` with `FileOpen`/`FileWrite`, in the format of
   section 2.3, from the page's variables.
6. When "replace my current settings" is set, delete
   `%LocalAppData%\ScreenBugs\settings.json` for the current user (bracketed
   with `SetShellVarContext current`). Deleting rather than rewriting keeps a
   single write path: the app re-seeds from `install-defaults.json` on its next
   launch, which also applies the startup choice through
   `StartupRegistration`.
7. `CreateShortcut "$SMPROGRAMS\Screen Bugs.lnk"`, and the desktop one when
   asked. Both point at `$INSTDIR\ScreenBugs.exe` with no arguments.
8. `WriteUninstaller "$INSTDIR\Uninstall.exe"`.
9. Write the Add/Remove Programs values under
   `SHCTX Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs`:
   `DisplayName` "Screen Bugs", `DisplayVersion` `${VERSION}`, `Publisher`,
   `DisplayIcon` `"$INSTDIR\ScreenBugs.exe,0"`, `UninstallString`
   `'"$INSTDIR\Uninstall.exe"'`, `QuietUninstallString`
   `'"$INSTDIR\Uninstall.exe" /S'`, `InstallLocation`, `EstimatedSize` from
   `${GetSize}`, and `NoModify`/`NoRepair` 1.

### 5.6 Launching at the finish page

The installer may be elevated, and an elevated tray app would run its
click-through overlay with elevated privileges — wrong, and it would also make
its `HKCU` writes land in the elevated hive. No `ShellExecAsUser` plugin is
present, so the finish-page run function drops privileges by asking the
non-elevated shell to launch it:

```
Exec '"$WINDIR\explorer.exe" "$INSTDIR\ScreenBugs.exe"'
```

If `explorer.exe` is not running — a rare shell-replacement or session state —
the process simply does not start; a current-user install is not elevated in the
first place, so the fallback path only matters for all-users installs, where a
plain `Exec "$INSTDIR\ScreenBugs.exe"` is used when `$WINDIR\explorer.exe` is
missing.

### 5.7 Prior installs and upgrades

`.onInit` reads `InstallLocation` from the uninstall key in both `HKLM`
(64-bit view) and `HKCU`. When exactly one exists, its scope and directory
pre-select the install-mode and directory pages, so an upgrade lands where the
previous version did.

After the directory page, if a prior install exists whose path differs from the
chosen `$INSTDIR`, its uninstaller runs first so the machine cannot end up with
two copies:

```
ExecWait '"$R0\Uninstall.exe" /S _?=$R0'
Delete "$R0\Uninstall.exe"
RMDir "$R0"
```

`_?=` keeps the uninstaller from relocating itself to `$TEMP`, which is what
makes `ExecWait` actually wait; it also means the uninstaller cannot delete its
own file, hence the two lines after. That silent run does not delete user data:
data removal is opt-in and off by default.

Installing over the same directory just overwrites, which is why step 2 of the
install section closes a running instance first.

### 5.8 Uninstall section

1. The custom page's single checkbox, **Also delete my Screen Bugs settings**,
   default unchecked. When set, remove `%LocalAppData%\ScreenBugs` — the
   settings file and the crash log — for the uninstalling user, bracketed with
   `SetShellVarContext current`.
2. Close a running instance the same way the installer does.
3. Delete the `HKCU` `Run` value `ScreenBugs` unconditionally: the app may have
   created it, and leaving it behind would make Windows try to launch a deleted
   executable at every sign-in.
4. Delete the Start Menu and desktop shortcuts in the uninstall context.
5. Remove the install directory, guarded so a bad `$INSTDIR` cannot delete
   something else:

   ```
   ${If} ${FileExists} "$INSTDIR\ScreenBugs.exe"
     RMDir /r "$INSTDIR"
   ${EndIf}
   ```

   No `/REBOOTOK` gymnastics are needed for `Uninstall.exe` itself: NSIS
   relocates a normally-launched uninstaller to `$TEMP` before running it, so
   the original file is not in use.
6. Delete the uninstall key from `SHCTX`.

Accepted limitations, both consequences of per-user data under an all-users
install: an uninstall cannot reach *other* users' `settings.json` files or their
`Run` values. A stale `Run` value for another user points at a missing
executable, which Windows ignores silently.

## 6. Build

```
build/build-installer.ps1     one command, from a clean tree to the setup exe
build/verify-install.ps1      silent install/uninstall assertions
```

`build-installer.ps1` steps, failing the script on any non-zero exit:

1. `dotnet test ScreenBugs.slnx -c Release`.
2. `dotnet publish src/ScreenBugs/ScreenBugs.csproj -c Release -r win-x64
   --self-contained true -p:PublishReadyToRun=true
   -p:SatelliteResourceLanguages=en -o build/publish`, after clearing
   `build/publish` so a removed file cannot survive into the payload.
   `PublishTrimmed` is deliberately absent: trimming is unsupported for WPF.
   ReadyToRun is on because the app is registered to launch at sign-in, where
   cold-start time is what the user notices.
3. Read `<Version>` from `Directory.Build.props` and pass it to
   `makensis /DVERSION=<version> /DPUBLISH_DIR=<abs path> /DOUT_FILE=<abs path>
   installer/ScreenBugs.nsi`. The script locates `makensis.exe` on `PATH`, then
   at `${env:ProgramFiles(x86)}\NSIS\makensis.exe`, and fails with an install
   hint if neither is found.
4. Output `build/ScreenBugs-Setup-<version>.exe` and print its size.

`.gitignore` gains `/build/publish/` and `/build/*.exe`. `assets/*.ico` and
`assets/*.bmp` are checked in, so building the installer does not require
running `IconGen`.

## 7. Testing

### 7.1 Unit tests (Core)

`tests/ScreenBugs.Tests/InstallDefaultsTests.cs`:

1. A full valid seed parses to its options with `StartAtLogin` true.
2. `StartAtLogin` absent → false, options still read.
3. `StartAtLogin` a non-boolean (`"yes"`) → false.
4. Malformed JSON → `BugOptions.Default`, `StartAtLogin` false.
5. Empty string → the same defaults.
6. `"Type": "Random"` → a `BugTypeSlot.Random` slot.
7. An unknown species name → the default slot, confirming the reuse of
   `SettingsSerializer`'s validation.
8. `BugCount` 999 → clamped to 50; `FrameRate` 7 → 60.

`tests/ScreenBugs.Tests/FirstRunSeedTests.cs`:

1. Saved settings present and a seed present → the saved options win and
   `RegisterStartup` is false.
2. Saved settings absent, seed present → the seed's options, and
   `RegisterStartup` follows the seed's `StartAtLogin` (both true and false
   cases).
3. Both absent → `BugOptions.Default`, `RegisterStartup` false.
4. Saved absent, seed malformed → `BugOptions.Default`, `RegisterStartup` false.
5. Saved present but malformed, seed present → defaults from the saved file and
   `RegisterStartup` false: a corrupt user file is not a first run, so the
   installer's seed does not resurrect itself and re-register startup.

### 7.2 Installer verification

`build/verify-install.ps1` drives the real setup executable silently and
asserts, so the option plumbing is checked end to end without GUI automation.
It installs **per-user into a temporary directory**, so it never writes to
Program Files; it does briefly create the current user's Start Menu shortcut and
`HKCU` uninstall key, both of which step 5 asserts are gone again. It never
touches the developer's own `settings.json`, because it passes no
`/RESETSETTINGS`.

1. `Setup.exe /S /CURRENTUSER /BUGTYPE=HouseSpider /BUGCOUNT=12 /STARTUP=0
   /DESKTOP=1 /D=<temp>` exits 0.
2. `<temp>\ScreenBugs.exe` exists, the directory holds more than 100 files
   (a sanity check that the self-contained payload actually shipped), and
   `install-defaults.json` parses to `HouseSpider`, count 12, speed 1,
   frame rate 60, `RespawnAll`, `StartAtLogin` false.
3. The `HKCU` uninstall key carries `DisplayName`, `DisplayVersion` matching
   `Directory.Build.props`, `UninstallString` and `QuietUninstallString`.
4. `Screen Bugs.lnk` exists in the per-user Start Menu and on the desktop.
5. `<temp>\Uninstall.exe /S` exits 0, after which the directory, the uninstall
   key and both shortcuts are gone.
6. A second pass with `/BUGTYPE=Random /BUGCOUNT=1 /STARTUP=1 /DESKTOP=0`
   asserts `Random`, count 1, `StartAtLogin` true, and no desktop shortcut,
   then uninstalls again.

### 7.3 Manual checklist

1. Welcome, install mode, directory, options, install, finish — in that order,
   with the ant on the welcome and finish panels and in the inner page headers.
2. All-users install raises exactly one UAC prompt; current-user install raises
   none.
3. Options page opens with Black garden ant, 5, startup checked, desktop
   unchecked. The spinner refuses to go below 1 or above 50, and typing 99
   installs as 50.
4. On a profile with no `settings.json`: after install the app starts with the
   chosen type and count; the Options dialog shows them; the "Run at Windows
   startup" checkbox matches the install choice; the `Run` value holds the
   quoted install path when startup was chosen.
5. On a profile that already has `settings.json`: the options page shows the
   hint and the replace checkbox; leaving it unchecked leaves the saved settings
   untouched after install; checking it makes the next launch adopt the
   installed defaults.
6. Installing while the app is running: setup asks, the tray icon disappears,
   the install completes, and the finish page's checkbox brings it back. In an
   all-users install, Task Manager's Elevated column shows the relaunched
   `ScreenBugs.exe` as not elevated.
7. Uninstall with the box unchecked leaves `%LocalAppData%\ScreenBugs`; with it
   checked the folder is gone. Either way the `Run` value and both shortcuts are
   gone.
8. Shortcut, taskbar and Add/Remove Programs entries show the ant icon; the ARP
   entry shows name, version and publisher, and its Uninstall button works.
9. Installing all-users over an existing current-user install removes the old
   copy; only one Add/Remove Programs entry remains.
10. Setup refuses to run, with a clear message, on 32-bit Windows or on a
    Windows version below 10.

## 8. File layout

```
Directory.Build.props                             new: version, company, product
.gitignore                                        modified: build outputs
assets/ScreenBugs.ico                             new, generated, checked in
assets/wizard-side.bmp                            new, generated, checked in
assets/wizard-header.bmp                          new, generated, checked in
installer/ScreenBugs.nsi                          new
installer/options-page.nsh                        new
build/build-installer.ps1                         new
build/verify-install.ps1                          new
tools/IconGen/IconGen.csproj                      new
tools/IconGen/Program.cs                          new
tools/IconGen/IcoWriter.cs                        new
tools/IconGen/WizardBitmaps.cs                    new
src/ScreenBugs.Core/Settings/InstallDefaults.cs   new
src/ScreenBugs.Core/Settings/FirstRunSeed.cs      new
src/ScreenBugs.Core/Settings/SeedOutcome.cs       new
src/ScreenBugs/Settings/SettingsBootstrap.cs      new
src/ScreenBugs/Settings/SettingsStore.cs          modified: Load replaced by TryRead
src/ScreenBugs/Tray/AntGlyph.cs                   new: geometry from TrayIconFactory
src/ScreenBugs/Tray/TrayIconFactory.cs            modified: delegates to AntGlyph
src/ScreenBugs/ScreenBugs.csproj                  modified: ApplicationIcon
src/ScreenBugs/App.xaml.cs                        modified: SettingsBootstrap.Load
ScreenBugs.slnx                                   modified: tools/IconGen
tests/ScreenBugs.Tests/InstallDefaultsTests.cs    new
tests/ScreenBugs.Tests/FirstRunSeedTests.cs       new
```
