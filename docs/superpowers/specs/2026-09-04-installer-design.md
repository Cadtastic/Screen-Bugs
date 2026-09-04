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
setting the frame rate or multiple type slots during install, a license page
(the repository has no licence text), and **changing the settings of a user who
has already run Screen Bugs** — an install never touches a saved
`settings.json`, and the tray's Options dialog is where an existing user changes
their mind.

## 2. Seeding: how install-time options reach the app

### 2.1 Mechanism

The installer writes the chosen options to `install-defaults.json` in the
install directory. The app reads it at startup and, **only when the user has no
`settings.json`**, adopts it: saves those options as the user's settings and
applies the seed's startup choice through `StartupRegistration`.

```
installer    →  <INSTDIR>\install-defaults.json
app, 1st run →  %LocalAppData%\ScreenBugs\settings.json
             →  HKCU\...\Run\ScreenBugs   added or removed to match the seed
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

The seed is `settings.json`'s format — whatever `SettingsSerializer` currently
writes, which is the `{Type, Speed}` row shape, not the bare type strings the
options spec's section 5 shows — plus one extra field:

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
reads and writes. The installer always writes exactly one slot at speed 1 and
`FrameRate` 60 — install time offers no control over slot count, per-slot speed,
frame rate or type-change behaviour.

Reading is total, like the rest of the settings code. A missing, empty, corrupt
or partial seed yields `BugOptions.Default`. `StartAtLogin` reads as a
**three-state value**: `true`, `false`, or *absent* when the field is missing or
not a boolean. Absent means "leave the `Run` key alone", so a damaged seed
neither registers startup behind the user's back nor unregisters what the user
chose. A real install always writes the field explicitly.

## 3. Code changes

### 3.1 `InstallDefaults` (Core)

```csharp
// src/ScreenBugs.Core/Settings/InstallDefaults.cs
/// <summary>The installer's seed: the options to start a new user with, and its startup choice.</summary>
/// <param name="StartAtLogin">Null when the seed does not say, which means leave startup as it is.</param>
public sealed record InstallDefaults(BugOptions Options, bool? StartAtLogin)
{
    public const string FileName = "install-defaults.json";

    public static InstallDefaults Default { get; } = new(BugOptions.Default, StartAtLogin: null);

    /// <summary>Total: any input at all yields a valid record.</summary>
    public static InstallDefaults Parse(string json);
}
```

`Parse` delegates the options to `SettingsSerializer.Deserialize`, which is
already total and already clamps `BugCount` and validates `FrameRate` and
species names, then reads `StartAtLogin` from the same JSON with
`JsonNode.Parse` inside a `try`/`catch (JsonException)`. Parsing the string
twice is deliberate: it keeps `SettingsSerializer` unchanged and keeps the extra
field out of `BugOptions`, which describes what the Options dialog controls and
nothing else.

### 3.2 `FirstRunSeed` (Core)

The decision is a pure function, so it can be tested without touching the file
system or the registry — the test project references Core only.

```csharp
// src/ScreenBugs.Core/Settings/SeedOutcome.cs
/// <summary>What to start with, and what to do about startup registration.</summary>
/// <param name="StartAtLogin">Null to leave the Run key alone; otherwise the state to apply.</param>
public readonly record struct SeedOutcome(BugOptions Options, bool? StartAtLogin);

// src/ScreenBugs.Core/Settings/FirstRunSeed.cs
public static class FirstRunSeed
{
    /// <param name="savedSettingsJson">The user's settings file content, or null when there is no file.</param>
    /// <param name="installDefaultsJson">The installer's seed content, or null when there is no file.</param>
    public static SeedOutcome Decide(string? savedSettingsJson, string? installDefaultsJson);
}
```

Rules, in order:

1. `savedSettingsJson` is not null → `(SettingsSerializer.Deserialize(saved), StartAtLogin: null)`.
   A file that exists but is corrupt still counts as "not first run": it
   deserializes to defaults and the seed is ignored. Startup stays whatever the
   user last chose.
2. Otherwise `installDefaultsJson` is not null → `InstallDefaults.Parse` and
   return its options and its three-state `StartAtLogin`.
3. Otherwise → `(BugOptions.Default, null)`. This is the no-installer case:
   running from a build output folder behaves exactly as it does today.

`StartAtLogin` is a state to *apply*, not a flag to act on when true. That is
what makes an install with startup switched off actually switch it off, rather
than silently leaving a stale `Run` value that would make the Options dialog
show the box checked — the disagreement section 2.2 promises cannot happen.

The caller persists when it read no saved file; `SeedOutcome` deliberately does
not carry a "should save" flag, because the caller is the thing that knows.

### 3.3 `SettingsStore` and `SettingsBootstrap` (app)

`SettingsStore.Load()` is superseded and removed — `App.OnStartup` is its only
caller. It is replaced by a raw read, because the decision now needs to
distinguish "no file" from "unreadable file":

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
`FirstRunSeed.Decide`, and when there was no saved file:

```csharp
SettingsStore.Save(outcome.Options);
if (outcome.StartAtLogin is { } startAtLogin)
{
    StartupRegistration.SetEnabled(startAtLogin);
}
```

Then, on **every** launch and regardless of which rule fired, it calls
`StartupRegistration.Refresh()` (section 3.6) so a relocated install keeps
starting at sign-in.

Every file read is wrapped and logged through `CrashLog`, matching
`SettingsStore`.

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

`ScreenBugs.csproj` removes the implicit `System.Drawing` using, so `AntGlyph.cs`
needs an explicit `using System.Drawing;` — as `TrayIconFactory.cs` already has.

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
adds `<ApplicationIcon>..\..\assets\ScreenBugs.ico</ApplicationIcon>` — resolved
relative to the project directory — so the executable, its shortcuts and the
Add/Remove Programs entry show the ant instead of the generic .NET icon.

### 3.6 `StartupRegistration.Refresh` (app)

An install that moves the app to a new directory leaves the `Run` value pointing
at the old path. `StartupRegistration.IsEnabled()` deliberately reports true
"whatever path it holds", because that is what Windows will try to launch — so
nothing currently notices, and the app would silently stop starting at sign-in.

```csharp
// src/ScreenBugs/Settings/StartupRegistration.cs
/// <summary>Re-points an existing Run value at this executable. Does nothing when startup is off.</summary>
public static void Refresh();
```

`Refresh` reads the value; if it exists and does not name
`Environment.ProcessPath`, it rewrites it — which `SetEnabled(true)` already
does, and whose comment already describes as repairing a stale entry. It never
creates a value that was absent, so it cannot turn startup on behind the user's
back.

`SettingsBootstrap.Load` calls it on **every** launch, not just a first run:
that is what makes the relocation upgrade of section 5.7 preserve the user's
choice. It costs one registry read per launch.

Consequence worth knowing: with two copies of the app installed, the `Run` value
follows whichever ran most recently. That is the best available answer — the
value can only name one executable — and last-run-wins is the least surprising
one.

## 4. Branding assets

`assets/` holds three generated, checked-in files:

| File | Size | Used for |
|---|---|---|
| `ScreenBugs.ico` | 16, 24, 32, 48, 64, 128, 256 px | app icon, setup icon, uninstaller icon, shortcuts, Add/Remove Programs |
| `wizard-side.bmp` | 164×314, 24-bit | MUI2 welcome and finish page panel |
| `wizard-header.bmp` | 150×57, 24-bit | MUI2 inner page header |

They are regenerated by `dotnet run --project tools/IconGen`, a console app that
project-references `ScreenBugs.csproj` to reuse `AntGlyph`. Referencing the
WinExe keeps a single copy of the geometry. Because `AntGlyph.Draw` returns a
`System.Drawing.Bitmap` from the Windows Desktop framework reference, `IconGen`
targets `net10.0-windows` with `<UseWindowsForms>true</UseWindowsForms>`. It
joins the solution under a `/tools/` folder so it cannot rot, and is not part of
the installer build — the assets it writes are checked in.

```csharp
// tools/IconGen/IcoWriter.cs
/// <summary>Packs bitmaps into a multi-size .ico file, which System.Drawing cannot save.</summary>
public static class IcoWriter
{
    public static void Write(string path, IReadOnlyList<Bitmap> images);
}

// tools/IconGen/WizardBitmaps.cs
/// <summary>Draws the MUI2 wizard panel and header images.</summary>
public static class WizardBitmaps
{
    public static Bitmap Side(int width, int height);     // large ant, left-aligned on a light panel
    public static Bitmap Header(int width, int height);   // small ant, right-aligned on a light strip
}
```

The file icon uses the dark-theme red, `#D8321F`, at every size: the tray glyph
picks its colour from the current theme, but a file icon cannot, and the red ant
stays legible on both light and dark backgrounds where the near-black one
disappears.

`IcoWriter` writes the container by hand: a 6-byte `ICONDIR`, one 16-byte
`ICONDIRENTRY` per image (width and height stored as 0 for 256), then each image
as a PNG payload. `WizardBitmaps` draws into `PixelFormat.Format24bppRgb`
bitmaps: a 32bpp save produces an alpha channel that MUI2 composites against
black rather than the page background.

## 5. Installer structure

```
installer/ScreenBugs.nsi        pages, sections, install, uninstall
installer/options-page.nsh      the custom options page and its silent-mode parsing
```

Includes: `MUI2.nsh`, `MultiUser.nsh`, `nsDialogs.nsh`, `LogicLib.nsh`,
`FileFunc.nsh`, `WinVer.nsh`, `x64.nsh` — all stock NSIS 3, no third-party
plugins, so the build needs nothing but a standard NSIS installation.

Compression `SetCompressor /SOLID lzma`, which matters: a self-contained
ReadyToRun publish is roughly 254 files and 155 MB.

### 5.1 Scope

`MULTIUSER_EXECUTIONLEVEL Highest` with `MULTIUSER_MUI` and
`MULTIUSER_INSTALLMODE_COMMANDLINE`. An administrator is elevated at start and
chooses the scope on the install-mode page; a standard user is not elevated and
gets a current-user install.

Stock `MultiUser.nsh` already produces the per-scope defaults, so the script
configures rather than reimplements them:

```
!define MULTIUSER_INSTALLMODE_INSTDIR "ScreenBugs"
!define MULTIUSER_USE_PROGRAMFILES64
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallLocation"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallLocation"
```

which yields:

| Scope | Directory | Uninstall key | Shortcuts |
|---|---|---|---|
| All users | `$PROGRAMFILES64\ScreenBugs` | `HKLM` (64-bit view) | common Start Menu / Desktop |
| Current user | user Programs folder, i.e. `$LOCALAPPDATA\Programs\ScreenBugs` | `HKCU` | per-user Start Menu / Desktop |

`MULTIUSER_USE_PROGRAMFILES64` is not optional: without it the all-users default
is 32-bit Program Files, which is wrong for a win-x64 payload. The two
`_REGISTRY_` pairs are also what pre-select a prior install's scope and
directory on the mode and directory pages, so section 5.7 needs no hand-written
`.onInit` detection for that.

Registry writes use the `SHCTX` root and shortcut paths use `$SMPROGRAMS` and
`$DESKTOP`, both of which follow the shell-var context `MultiUser` sets.

**Three traps this creates, all of which the implementation must respect:**

- `${MULTIUSER_INIT}` overwrites `$INSTDIR` and `$MultiUser.InstallMode`. Any
  `.onInit` code that reads or sets either must run *after* the macro, or it is
  silently discarded.
- **`SetRegView 64` must run *before* `${MULTIUSER_INIT}`**, which is the one
  exception to the rule above — it touches neither `$INSTDIR` nor
  `$MultiUser.InstallMode`, so it is exempt. The macro itself performs the
  `HKLM` reads for both registry define pairs, and those reads obey the current
  view; the installer stub is 32-bit, so in the default view they resolve under
  `WOW6432Node`, which is not where section 5.5 writes the key. Get this
  backwards and every all-users upgrade silently falls back to the default
  scope and directory instead of the previous install's — section 5.7's
  mechanism fails with no error. `SetRegView` is a no-op for the current-user
  key, since WOW64 does not redirect `HKCU\...\Uninstall`.
- With `SetShellVarContext all`, NSIS's `$LOCALAPPDATA` resolves to
  `C:\ProgramData`, not the user's folder. The one place that needs the real
  per-user path — the optional data removal on uninstall — must bracket the read
  with `SetShellVarContext current` and restore the previous context afterwards.

### 5.2 Pages

1. `MUI_PAGE_WELCOME`
2. `MULTIUSER_PAGE_INSTALLMODE`
3. `MUI_PAGE_DIRECTORY`
4. **Options** — custom, `installer/options-page.nsh`
5. `MUI_PAGE_INSTFILES`
6. `MUI_PAGE_FINISH` with a run checkbox, checked by default. Both
   `!define MUI_FINISHPAGE_RUN ""` — which is what makes the checkbox appear at
   all — and `MUI_FINISHPAGE_RUN_FUNCTION`, pointing at the launcher of section
   5.6, are needed; the empty `MUI_FINISHPAGE_RUN` is not optional.

Uninstall: `MUI_UNPAGE_CONFIRM`, then a custom one-checkbox page, then
`MUI_UNPAGE_INSTFILES`.

`MUI_HEADERIMAGE`, `MUI_HEADERIMAGE_BITMAP`, `MUI_WELCOMEFINISHPAGE_BITMAP`,
`MUI_ICON` and `MUI_UNICON` point at the section 4 assets.
`MUI_UNWELCOMEFINISHPAGE_BITMAP` is deliberately not set: the uninstaller has no
welcome or finish page for it to appear on. `MUI_ABORTWARNING` on.

### 5.3 The options page

Controls, top to bottom:

| Control | Default |
|---|---|
| Droplist, **Bug type** | Black garden ant |
| Number field with an up-down spinner, **Bugs on screen** (1–50) | 5 |
| Checkbox, **Run Screen Bugs when I sign in to Windows** | checked |
| Checkbox, **Create a desktop shortcut** | unchecked |
| Static label below them | "These apply the first time each user runs Screen Bugs. If you've used it before, your saved settings are kept — change them from Options in the tray menu." |

That label is deliberately unconditional. Detecting whether *this* user already
has a `settings.json` would mean reading a per-user path from a possibly
elevated installer, which under over-the-shoulder elevation reads the wrong
profile and would show a misleading hint; the sentence is true either way.

The droplist lists `Random` first, then the nine species in `SpeciesId` order,
with the same labels the Options dialog uses (`BugTypeChoice.LabelFor`):
Hissing cockroach, Black garden ant, Red fire ant, Praying mantis, Seven-spot
ladybug, Stag beetle, House spider, Centipede, Stink bug. A `BugTypeNameFor`
function maps the selected index to the enum name the seed needs, via an
explicit `${Switch}`; the two lists live next to each other in
`options-page.nsh` so they cannot drift unnoticed.

The count uses stock `${NSD_CreateAutoUpDown}` with `${NSD_UD_SetRange32} 1 50`
(there is no `${NSD_UD_SetRange}`) rather than the trackbar the sketch called
for: nsDialogs routes `WM_NOTIFY` but not the `WM_HSCROLL` a trackbar sends, so
a live value label beside a slider would need dialog subclassing, and a spinner
shows its value without one. The leave function clamps the typed value into
1–50 — the app clamps too, but clamping here means the written seed says what
the installer showed.

The desktop-shortcut checkbox drives the install section directly. A Start Menu
shortcut is always created; a desktop shortcut is opt-in because Screen Bugs
lives in the tray and is rarely relaunched by hand.

### 5.4 Silent install

`.onInit` parses switches with `${GetParameters}` and `${GetOptions}` — after
`${MULTIUSER_INIT}`, per section 5.1 — so every option page value has a
command-line equivalent and the page is skipped under `/S`:

| Switch | Effect |
|---|---|
| `/S` | silent |
| `/ALLUSERS`, `/CURRENTUSER` | scope (from `MultiUser`) |
| `/D=<path>` | install directory (NSIS built-in, must be last) |
| `/BUGTYPE=<name>` | `Random` or a `SpeciesId` name |
| `/BUGCOUNT=<1-50>` | bug count |
| `/STARTUP=0\|1` | run at sign-in |
| `/DESKTOP=0\|1` | desktop shortcut |

Unrecognized `/BUGTYPE` values and out-of-range `/BUGCOUNT` values fall back to
the defaults rather than aborting: a cosmetic option is not worth failing a
deployment over, and `verify-install.ps1` asserts the written seed, so a typo in
a script surfaces there. Uninstall accepts `/S`, `/DELETEDATA=1`, and `/UPGRADE=1` — the last used
only by the relocation path of section 5.7, never by a user.

### 5.5 Install section

In order:

1. Refuse to continue unless `${RunningX64}` and `${AtLeastBuild} 14393`, with a
   message naming the requirement. The payload is win-x64, and Windows 10 1607
   (build 14393) is .NET 10's documented floor — `${AtLeastWin10}` would admit
   earlier Windows 10 builds.
2. Detect a running instance by opening the app's own single-instance mutex:
   `kernel32::OpenMutex(SYNCHRONIZE, 0, "Local\ScreenBugs.SingleInstance")`
   through `System::Call`; a non-null handle means it is running (close the
   handle immediately). If it is, ask for permission to close it — silently in
   `/S` mode — then `nsExec::ExecToStack 'taskkill /F /IM ScreenBugs.exe'`.
   `/F` denies the app its `OnExit`, which is harmless here: settings are saved
   when the Options dialog is accepted, not at exit, and `SingleInstanceGuard`
   already treats an abandoned mutex as a free slot. The mutex is in the
   session-local namespace, so an elevated installer in the same session still
   sees it. An instance running under a *different* user is invisible here and
   will instead lock the files; NSIS's standard retry prompt covers that.
3. Handle a prior install at a different path, per section 5.7.
4. `SetOutPath $INSTDIR` and `File /r` the publish output.
5. Write `install-defaults.json` with `FileOpen`/`FileWrite`, in the format of
   section 2.3, from the page's variables.
6. `CreateShortcut "$SMPROGRAMS\Screen Bugs.lnk"`, and the desktop one when
   asked. Both point at `$INSTDIR\ScreenBugs.exe` with no arguments.
7. `WriteUninstaller "$INSTDIR\Uninstall.exe"`.
8. Write the Add/Remove Programs values under
   `SHCTX Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs`:
   `DisplayName` "Screen Bugs", `DisplayVersion` `${VERSION}`, `Publisher`,
   `DisplayIcon` `"$INSTDIR\ScreenBugs.exe,0"`, `UninstallString`
   `'"$INSTDIR\Uninstall.exe"'`, `QuietUninstallString`
   `'"$INSTDIR\Uninstall.exe" /S'`, `InstallLocation` — which is also what
   section 5.1's registry defines read back on the next upgrade —
   `EstimatedSize` from `${GetSize}`, and `NoModify`/`NoRepair` 1.

### 5.6 Launching at the finish page

The installer may be elevated, and an elevated tray app would run its
click-through overlay with elevated privileges — wrong, and it would also make
its `HKCU` writes land in the elevated hive. No `ShellExecAsUser` plugin is
present, so when the installer is elevated the finish-page run function drops
privileges by asking the non-elevated shell to launch it:

```
${If} $MultiUser.Privileges == "Admin"
${OrIf} $MultiUser.Privileges == "Power"
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\ScreenBugs.exe"'
${Else}
  Exec '"$INSTDIR\ScreenBugs.exe"'
${EndIf}
```

The condition is *whether this process holds an elevated token*, which is what
`$MultiUser.Privileges` reports — `${MULTIUSER_INIT}` sets it from
`UserInfo::GetAccountType`, and under `RequestExecutionLevel highest` it reads
`Admin` exactly when the process is elevated. No `UAC` plugin macro is used,
because that plugin is not installed.

The install **mode** is the wrong test and must not be used here: section 5.1
elevates an administrator *before* the mode page, so an admin who then picks
"only for me" is still running elevated and would take the plain `Exec` branch,
handing the app the elevated token this section exists to avoid.

The branch does not depend on whether `explorer.exe` exists. If the shell has
been replaced or is not running, the app simply does not start and the user
launches it from the Start Menu shortcut; that is an acceptable outcome for a
convenience checkbox.

### 5.7 Prior installs and upgrades

Pre-selecting a prior install's scope and directory is handled entirely by the
`MULTIUSER_INSTALLMODE_*_REGISTRY_*` defines of section 5.1.

What remains is the relocation case, handled in the install section (step 3),
once the user has chosen a scope and directory. If an uninstall key exists — in
either `HKLM` (64-bit view) or `HKCU` — whose `InstallLocation` differs from the
chosen `$INSTDIR`, that install's uninstaller runs first, so the machine cannot
end up with two copies:

```
ExecWait '"$R0\Uninstall.exe" /S /UPGRADE=1 _?=$R0'
Delete "$R0\Uninstall.exe"
RMDir "$R0"
```

`_?=` keeps the uninstaller from relocating itself to `$TEMP`, which is what
makes `ExecWait` actually wait; it also means the uninstaller cannot delete its
own file, hence the two lines after. That silent run does not delete user data:
data removal is opt-in and off by default.

`/UPGRADE=1` is what stops this path from quietly discarding the user's startup
choice. Without it, the old uninstaller deletes the `HKCU` `Run` value
(section 5.8 step 3), and nothing puts it back: a returning user has a
`settings.json`, so `FirstRunSeed` takes rule 1 and leaves the `Run` key alone
by design. The value would be gone and the Options dialog would show the box
unchecked, which section 1 promises an install will not do. With the switch, the
uninstaller keeps the value — now pointing at the directory it just removed —
and the app repairs the path on its next launch, per section 3.6.

Installing over the same directory just overwrites, which is why step 2 of the
install section closes a running instance first.

### 5.8 Uninstall section

1. Close a running instance the same way the installer does. This comes first
   so a live app cannot re-create `error.log` after step 2 has deleted it.
2. The custom page's single checkbox, **Also delete my Screen Bugs settings**,
   default unchecked, `/DELETEDATA=1` in silent mode. When set, remove
   `%LocalAppData%\ScreenBugs` — the settings file and the crash log — bracketed
   with `SetShellVarContext current`. Reading the switch needs
   `!insertmacro un.GetParameters` and `!insertmacro un.GetOptions` to
   instantiate the `un.` variants; the installer side needs no such line, so it
   is easy to miss.
3. Delete the `HKCU` `Run` value `ScreenBugs`, unless `/UPGRADE=1` was passed.
   The app may have created it, and leaving it behind would make Windows try to
   launch a deleted executable at every sign-in — but section 5.7 invokes this
   same uninstaller as a *relocation* step, where deleting the value would throw
   away a preference the user still holds. `/UPGRADE=1` is therefore accepted
   only for that call, and the app repairs the now-stale path per section 3.6.
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

Accepted limitations, all consequences of per-user data under a possibly
elevated uninstall. Steps 1 and 3 act on **the account the uninstaller is
running as**: for a current-user uninstall that is the right account, but an
all-users uninstall elevated with a *different* admin's credentials removes that
admin's data and `Run` value, not the signed-in user's. Nor can any uninstall
reach other users' `settings.json` files or `Run` values. A stale `Run` value
pointing at a missing executable is ignored silently by Windows, and a leftover
`%LocalAppData%\ScreenBugs` is two small files.

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
   `PublishTrimmed` is deliberately absent: trimming is unsupported for WPF and
   fails the publish with `NETSDK1175`. ReadyToRun is on because the app is
   registered to launch at sign-in, where cold-start time is what the user
   notices.
3. Read the version with `dotnet msbuild Directory.Build.props
   -getProperty:Version` rather than by regex, and pass it to
   `makensis /DVERSION=<version> /DPUBLISH_DIR=<abs path> /DOUT_FILE=<abs path>
   installer/ScreenBugs.nsi`. The script locates `makensis.exe` on `PATH`, then
   at `${env:ProgramFiles(x86)}\NSIS\makensis.exe`, and fails with an install
   hint if neither is found.
4. Output `build/ScreenBugs-Setup-<version>.exe` and print its size.

`.gitignore` gains `/build/*.exe`; `build/publish/` is already covered by the
existing `publish/` rule. `assets/*.ico` and `assets/*.bmp` are checked in, so
building the installer does not require running `IconGen`.

## 7. Testing

### 7.1 Unit tests (Core)

`tests/ScreenBugs.Tests/InstallDefaultsTests.cs`:

1. A full valid seed parses to its options with `StartAtLogin` true.
2. `"StartAtLogin": false` → false, distinct from absent.
3. `StartAtLogin` absent → null.
4. `StartAtLogin` a non-boolean (`"yes"`) → null.
5. Malformed JSON → `BugOptions.Default`, `StartAtLogin` null.
6. Empty string → the same defaults.
7. `"Type": "Random"` → a single `new SlotSetting(BugTypeSlot.Random, 1f)`.
8. An unknown species name → the default slot, confirming the reuse of
   `SettingsSerializer`'s validation.
9. `BugCount` 999 → clamped to 50; `FrameRate` 7 → 60.

`tests/ScreenBugs.Tests/FirstRunSeedTests.cs`:

1. Saved settings present and a seed present → the saved options win and
   `StartAtLogin` is null, so the `Run` key is left alone.
2. Saved settings absent, seed present with `StartAtLogin` true → the seed's
   options and true.
3. Saved settings absent, seed present with `StartAtLogin` **false** → false,
   not null: an install with startup switched off must be able to switch it off.
4. Saved absent, seed present without the field → null.
5. Both absent → `BugOptions.Default`, null.
6. Saved absent, seed malformed → `BugOptions.Default`, null.
7. Saved present but malformed, seed present → defaults from the saved file and
   null: a corrupt user file is not a first run, so the installer's seed does
   not resurrect itself and re-apply a startup choice.

### 7.2 Installer verification

`build/verify-install.ps1` drives the real setup executable silently and
asserts, so the option plumbing is checked end to end without GUI automation.
It installs **per-user into a temporary directory**, so it never writes to
Program Files, and it never touches the developer's own `settings.json`, because
nothing in the installer writes one.

It does use the production per-user uninstall key, so it **first refuses to run
when `HKCU\...\Uninstall\ScreenBugs` already exists**, printing "uninstall your
existing per-user Screen Bugs first" — otherwise it would overwrite and then
delete a real installation's Add/Remove Programs entry. The per-user Start Menu
shortcut it creates is asserted gone again in step 5.

1. `Setup.exe /S /CURRENTUSER /BUGTYPE=HouseSpider /BUGCOUNT=12 /STARTUP=0
   /DESKTOP=1 /D=<temp>` exits 0. `/D=` must be last and **unquoted**, even when
   the temporary path contains spaces — quoting it is the standard trip-up, and
   PowerShell will happily add the quotes if the argument is passed as one
   string.
2. `<temp>\ScreenBugs.exe` exists, the directory holds more than 100 files
   (a sanity check that the self-contained payload actually shipped), and
   `install-defaults.json` parses to `HouseSpider`, count 12, speed 1,
   frame rate 60, `RespawnAll`, `StartAtLogin` false.
3. The `HKCU` uninstall key carries `DisplayName`, `DisplayVersion` matching
   `Directory.Build.props`, `InstallLocation`, `UninstallString` and
   `QuietUninstallString`.
4. `Screen Bugs.lnk` exists in the per-user Start Menu and on the desktop.
5. `<temp>\Uninstall.exe /S _?=<temp>` run with `-Wait`. The `_?=` is required:
   without it the launched process relocates itself to `$TEMP` and returns
   immediately, so the assertions would race an uninstall still in progress.
   Because `_?=` also stops the uninstaller deleting its own file, the expected
   end state is a directory holding **only** `Uninstall.exe` — assert that
   `ScreenBugs.exe` and `install-defaults.json` are gone, that the uninstall key
   is gone and that both shortcuts are gone, then delete the leftover
   `Uninstall.exe` and the directory from the script.
6. A second pass with `/BUGTYPE=Random /BUGCOUNT=1 /STARTUP=1 /DESKTOP=0`
   asserts `Random`, count 1, `StartAtLogin` true, and no desktop shortcut, then
   uninstalls the same way.

### 7.3 Manual checklist

1. Welcome, install mode, directory, options, install, finish — in that order,
   with the ant on the welcome and finish panels and in the inner page headers.
2. All-users install raises exactly one UAC prompt; current-user install raises
   none.
3. Options page opens with Black garden ant, 5, startup checked, desktop
   unchecked. The spinner refuses to go below 1 or above 50, and typing 99
   installs as 50.
4. On a profile with no `settings.json`, installing with startup **checked**:
   the app starts with the chosen type and count, the Options dialog shows them,
   its "Run at Windows startup" box is checked, and the `Run` value holds the
   quoted install path.
5. On a profile with no `settings.json`, installing with startup **unchecked**
   while a stale `Run` value exists: after the first launch the value is gone and
   the Options dialog's box is unchecked. This is the case section 3.2 exists
   for.
6. On a profile that already has `settings.json`: install changes neither the
   settings nor the `Run` value, and the Options dialog shows exactly what it
   showed before.
7. Installing while the app is running: setup asks, the tray icon disappears,
   the install completes, and the finish page's checkbox brings it back. In an
   all-users install, Task Manager's Elevated column shows the relaunched
   `ScreenBugs.exe` as not elevated.
8. Uninstall with the box unchecked leaves `%LocalAppData%\ScreenBugs`; with it
   checked the folder is gone. Either way the `Run` value and both shortcuts are
   gone.
9. Shortcut, taskbar and Add/Remove Programs entries show the ant icon; the ARP
   entry shows name, version and publisher, and its Uninstall button works.
10. Installing all-users over an existing current-user install removes the old
    copy; only one Add/Remove Programs entry remains.
11. That same relocation, done by a user who had startup **on** and a saved
    `settings.json`: after the upgrade the Options dialog still shows the box
    checked, and the `Run` value holds the *new* install path — the case
    sections 5.7 and 3.6 exist for. Their settings are otherwise unchanged.
12. Setup refuses to run, with a clear message, on 32-bit Windows or on a
    Windows build below 14393.

## 8. Implementation phases

The work stages into four separately-completable pieces, each leaving the
repository green:

1. **Seeding** — `InstallDefaults`, `SeedOutcome`, `FirstRunSeed`,
   `SettingsStore.TryRead`, `SettingsBootstrap`, `StartupRegistration.Refresh`,
   `App.OnStartup`, and the two test files. Verifiable by `dotnet test` alone, and by dropping an
   `install-defaults.json` next to a debug build.
2. **Assets** — `AntGlyph` extraction, `IconGen`, the checked-in `.ico` and
   `.bmp` files, `Directory.Build.props`, `ApplicationIcon`. Verifiable by
   looking at the built executable's icon.
3. **Installer** — `ScreenBugs.nsi`, `options-page.nsh`,
   `build-installer.ps1`, and the `.gitignore` entry. Verifiable by producing a setup executable and
   installing it.
4. **Verification** — `verify-install.ps1`, then the manual checklist.

## 9. File layout

```
Directory.Build.props                             new: version, company, product
.gitignore                                        modified: /build/*.exe
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
src/ScreenBugs/Settings/StartupRegistration.cs    modified: Refresh
src/ScreenBugs/Tray/AntGlyph.cs                   new: geometry from TrayIconFactory
src/ScreenBugs/Tray/TrayIconFactory.cs            modified: delegates to AntGlyph
src/ScreenBugs/ScreenBugs.csproj                  modified: ApplicationIcon
src/ScreenBugs/App.xaml.cs                        modified: SettingsBootstrap.Load
ScreenBugs.slnx                                   modified: tools/IconGen
tests/ScreenBugs.Tests/InstallDefaultsTests.cs    new
tests/ScreenBugs.Tests/FirstRunSeedTests.cs       new
```
