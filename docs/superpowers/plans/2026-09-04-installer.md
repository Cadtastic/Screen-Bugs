# Installer Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `ScreenBugs-Setup-<version>.exe` — an NSIS installer wrapping a self-contained win-x64 publish, which installs for all users or just the current one and collects four basic options during the wizard.

**Architecture:** The installer never writes user settings. It writes the chosen options to `install-defaults.json` in the install directory, and the app adopts that seed the first time it runs for a user who has no `settings.json`. The seeding rule is a pure function in Core (`FirstRunSeed.Decide`), so the whole first-run decision is unit-testable with no file system and no registry; the app side only does the reads and writes. Branding assets are generated from the app's own ant glyph by a small tool, so there is one copy of the geometry.

**Tech Stack:** .NET 10, C# 14, WPF, `System.Text.Json.Nodes`, xUnit 2.9, NSIS 3.11 (stock only — MUI2, MultiUser, nsDialogs; no third-party plugins), PowerShell 7.

**Spec:** `docs/superpowers/specs/2026-09-04-installer-design.md`. Section numbers below (for example "spec 5.1") refer to it.

**Conventions (from the user's global CLAUDE.md, mandatory):**
- Primary constructors; use the parameters directly, no `_field` copies, no null checks.
- One type per file, named for the type.
- Static classes with no state are fine and are the existing pattern for `SettingsStore`, `StartupRegistration` and `SettingsSerializer`.

**Commits:** every message ends with the trailer `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

**Branch:** create `feat/installer` from `main` before Task 1. All paths are relative to `C:\Users\AddamBoord\source\repos\ScreenSavers`.

**Build and test commands.** `dotnet test` intermittently hangs on this machine when it also has to build, and piping its output makes it worse. Always build first, redirect to a file, and pass `-nodeReuse:false`:

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
dotnet test tests/ScreenBugs.Tests -nologo -v q --no-build -nodeReuse:false
dotnet build src/ScreenBugs -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
```

**makensis, and passing switches from bash.** `C:\Program Files (x86)\NSIS\makensis.exe`.
Git Bash runs under the MSYS2 runtime, which rewrites arguments that look like POSIX paths
*before* the program sees them, and quoting does not stop it — the rewriting happens below the
shell. Two rules follow, both verified on this machine:

1. **Use `-D`, never `/D`, for makensis defines.** `"/DVERSION=1.0.0"` arrives as
   `C:/Program Files/Git/DVERSION=1.0.0` and makensis fails with `Can't open script`.
   Confusingly, `/DASSETS_DIR=C:\...` *does* survive, because a Windows-path value stops the
   conversion — so `/D` appears to work until the first define with a plain value. `-D` is
   immune and means the same thing to makensis.
2. **Never launch the setup executable with `/S`-style switches from bash.** `/S` arrives as
   `S:/` and `/CURRENTUSER` as `C:/Program Files/Git/CURRENTUSER`, so the installer would open
   its GUI and ignore the options. Go through PowerShell, which passes them intact:

```bash
MAKENSIS="/c/Program Files (x86)/NSIS/makensis.exe"
"$MAKENSIS" -V2 "-DASSETS_DIR=C:\Users\AddamBoord\source\repos\ScreenSavers\assets" installer/ScreenBugs.nsi

# Running a built setup with switches:
pwsh -NoProfile -c "Start-Process -FilePath 'C:\path\to\Setup.exe' ""`
  -ArgumentList '/S','/CURRENTUSER','/BUGCOUNT=12','/D=C:\Temp\sb' -Wait"
```

**Starting point:** 92 tests pass on `main`. This plan adds 16 test methods (23 cases with `[Theory]` data), three new C# files in Core, two in the app, one new tool project, two NSIS scripts and two PowerShell scripts.

**Verified before writing this plan** — do not re-litigate these, they were checked by compiling probes on this machine:
- Every NSIS mechanism used below compiles clean under stock NSIS 3.11, including `MULTIUSER_USE_PROGRAMFILES64`, both `MULTIUSER_INSTALLMODE_*_REGISTRY_*` pairs, `${NSD_CreateAutoUpDown}`, `${NSD_UD_SetRange32}`, `${NSD_CB_GetSelectionIndex}`, `${AtLeastBuild}`, `${GetSize}`, `un.GetOptions`, `SHCTX`, and reading `$MultiUser.Privileges` from a finish-page run function.
- Passing `-D` defines to makensis works; `/D` is mangled by MSYS2 unless the value happens to
  be a Windows path, and so are `/S`-style switches to the setup executable (see above).
- `${__FILEDIR__}` resolves an `!include` against the script's own directory.
- `Var Desktop` does **not** compile: `$DESKTOP` is a built-in constant. The variable below is `$DesktopShortcut`.
- `dotnet msbuild Directory.Build.props -getProperty:Version` prints the bare version.
- `.gitignore` already covers `build/publish/` via its `publish/` rule; `assets/*.ico` and `assets/*.bmp` are **not** ignored; `build/*.exe` is not ignored and must be added.
- `src/ScreenBugs/AssemblyInfo.cs` declares only `ThemeInfo`, so a root `Directory.Build.props` setting `Version`/`Company`/`Product`/`Copyright` cannot collide with it.

---

## File structure

```
Directory.Build.props                              new   version, company, product for every project
.gitignore                                         mod   /build/*.exe
assets/ScreenBugs.ico                              new   generated, checked in
assets/wizard-side.bmp                             new   generated, checked in
assets/wizard-header.bmp                           new   generated, checked in
src/ScreenBugs.Core/Settings/
  InstallDefaults.cs                               new   the seed record + total parser
  SeedOutcome.cs                                   new   what a launch starts with
  FirstRunSeed.cs                                  new   the pure first-run rule
src/ScreenBugs/Settings/
  SettingsStore.cs                                 mod   Load() -> TryRead()
  SettingsBootstrap.cs                             new   the IO around FirstRunSeed
  StartupRegistration.cs                           mod   + Refresh()
src/ScreenBugs/Tray/
  AntGlyph.cs                                      new   the glyph, at any size
  TrayIconFactory.cs                               mod   delegates to AntGlyph
src/ScreenBugs/
  App.xaml.cs                                      mod   SettingsBootstrap.Load()
  ScreenBugs.csproj                                mod   ApplicationIcon
tools/IconGen/
  IconGen.csproj                                   new   net10.0-windows console tool
  IcoWriter.cs                                     new   packs a multi-size .ico
  WizardBitmaps.cs                                 new   MUI2 panel and header images
  Program.cs                                       new   writes the three assets
installer/
  ScreenBugs.nsi                                   new   defines, pages, sections
  options-page.nsh                                 new   the options page + validation
build/
  build-installer.ps1                              new   test -> publish -> makensis
  verify-install.ps1                               new   silent install/uninstall assertions
tests/ScreenBugs.Tests/
  InstallDefaultsTests.cs                          new   9 test methods
  FirstRunSeedTests.cs                             new   7 test methods
ScreenBugs.slnx                                    mod   tools/IconGen
```

---

## Chunk 1: Seeding

The whole install-time-options mechanism, testable with no installer in sight. At the end of this chunk you can drop a hand-written `install-defaults.json` next to a debug build and watch the app adopt it.

### Task 0: Branch

- [ ] **Step 1: Create the branch**

```bash
git switch -c feat/installer
git status
```

Expected: `On branch feat/installer` and a clean tree. If the tree is not clean, stop and deal
with that first — every task below commits.

---

### Task 1: The seed record

**Files:**
- Create: `src/ScreenBugs.Core/Settings/InstallDefaults.cs`
- Test: `tests/ScreenBugs.Tests/InstallDefaultsTests.cs`

Spec 2.3 and 3.1. The one thing to get right: `StartAtLogin` is **three-state**. `true`, `false`, and absent are three different answers, because absent has to mean "leave the `Run` key alone" — a damaged seed must neither register startup behind the user's back nor unregister what they chose.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenBugs.Tests/InstallDefaultsTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class InstallDefaultsTests
{
    private const string FullSeed = """
        {
          "TypeSlots": [ { "Type": "HouseSpider", "Speed": 1 } ],
          "BugCount": 12,
          "FrameRate": 60,
          "OnTypeChange": "RespawnAll",
          "StartAtLogin": true
        }
        """;

    [Fact]
    public void A_full_seed_parses_to_its_options_and_startup_choice()
    {
        var defaults = InstallDefaults.Parse(FullSeed);

        Assert.Equal([SlotSetting.For(SpeciesId.HouseSpider)], defaults.Options.TypeSlots);
        Assert.Equal(12, defaults.Options.BugCount);
        Assert.Equal(60, defaults.Options.FrameRate);
        Assert.Equal(TypeChangeBehavior.RespawnAll, defaults.Options.OnTypeChange);
        Assert.True(defaults.StartAtLogin);
    }

    [Fact]
    public void Startup_false_is_distinct_from_the_field_being_absent()
    {
        Assert.False(InstallDefaults.Parse("""{"StartAtLogin": false}""").StartAtLogin);
    }

    [Fact]
    public void An_absent_startup_field_leaves_startup_alone()
    {
        Assert.Null(InstallDefaults.Parse("""{"BugCount": 3}""").StartAtLogin);
    }

    [Theory]
    [InlineData("""{"StartAtLogin": "yes"}""")]
    [InlineData("""{"StartAtLogin": "true"}""")]
    [InlineData("""{"StartAtLogin": 1}""")]
    [InlineData("""{"StartAtLogin": null}""")]
    public void A_non_boolean_startup_field_leaves_startup_alone(string json)
    {
        Assert.Null(InstallDefaults.Parse(json).StartAtLogin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    public void Unusable_input_yields_the_defaults(string json)
    {
        Assert.Equal(InstallDefaults.Default, InstallDefaults.Parse(json));
    }

    [Fact]
    public void Random_reads_as_a_random_slot_at_the_default_speed()
    {
        var defaults = InstallDefaults.Parse("""{"TypeSlots":[{"Type":"Random","Speed":1}]}""");

        Assert.Equal([new SlotSetting(BugTypeSlot.Random, SlotSetting.DefaultSpeed)], defaults.Options.TypeSlots);
    }

    [Fact]
    public void An_unknown_species_falls_back_to_the_default_slots()
    {
        var defaults = InstallDefaults.Parse("""{"TypeSlots":[{"Type":"Wasp","Speed":1}]}""");

        Assert.Equal(BugOptions.Default.TypeSlots, defaults.Options.TypeSlots);
    }

    [Fact]
    public void Out_of_range_numbers_are_repaired_by_the_settings_serializer()
    {
        var defaults = InstallDefaults.Parse("""{"BugCount": 999, "FrameRate": 7}""");

        Assert.Equal(50, defaults.Options.BugCount);
        Assert.Equal(60, defaults.Options.FrameRate);
    }

    [Fact]
    public void The_file_name_is_the_one_the_installer_writes()
    {
        Assert.Equal("install-defaults.json", InstallDefaults.FileName);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error" /tmp/b.log | head -3
```

Expected: build FAILS with `error CS0103: The name 'InstallDefaults' does not exist` (several times).

- [ ] **Step 3: Create the record**

Create `src/ScreenBugs.Core/Settings/InstallDefaults.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScreenBugs.Core.Settings;

/// <summary>
/// The installer's seed, read from install-defaults.json beside the executable: the options to
/// start a new user with, and what to do about running at sign-in.
/// </summary>
/// <param name="StartAtLogin">Null when the seed does not say, which means leave startup as it is.</param>
public sealed record InstallDefaults(BugOptions Options, bool? StartAtLogin)
{
    public const string FileName = "install-defaults.json";

    public static InstallDefaults Default { get; } = new(BugOptions.Default, StartAtLogin: null);

    /// <summary>Total: any input at all yields a valid record.</summary>
    public static InstallDefaults Parse(string json) =>
        new(SettingsSerializer.Deserialize(json), ReadStartAtLogin(json));

    /// <summary>
    /// Three-state on purpose. A missing or non-boolean field reads as null, so a damaged seed
    /// neither registers startup behind the user's back nor unregisters what they chose. The JSON
    /// is parsed a second time here to keep this field out of <see cref="BugOptions"/>, which
    /// describes what the Options dialog controls and nothing else.
    /// </summary>
    private static bool? ReadStartAtLogin(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject root
                && root["StartAtLogin"] is JsonValue value
                && value.TryGetValue(out bool startAtLogin)
                    ? startAtLogin
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
dotnet test tests/ScreenBugs.Tests -nologo -v q --no-build -nodeReuse:false
```

Expected: `Passed!  - Failed: 0, Passed: 108` — 92 existing plus 16 cases from these 9 methods (7 `[Fact]` plus a 4-case and a 5-case `[Theory]`).

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs.Core/Settings/InstallDefaults.cs tests/ScreenBugs.Tests/InstallDefaultsTests.cs
git commit -m "$(cat <<'EOF'
feat(settings): read the installer's seed file

StartAtLogin is three-state: true, false and absent are different
answers, because absent has to mean "leave the Run key alone" so a
damaged seed cannot silently change what the user chose.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: The first-run rule

**Files:**
- Create: `src/ScreenBugs.Core/Settings/SeedOutcome.cs`
- Create: `src/ScreenBugs.Core/Settings/FirstRunSeed.cs`
- Test: `tests/ScreenBugs.Tests/FirstRunSeedTests.cs`

Spec 3.2. Pure, so the whole rule is testable without touching a file or the registry — which is the only reason it lives in Core rather than the app.

Test 3 below is the regression test for a real bug found in spec review: if the seed's startup choice were a flag acted on only when true, an install with startup switched **off** would silently leave a stale `Run` value, and the Options dialog would then show the box checked. It is a state to *apply*, not a flag.

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenBugs.Tests/FirstRunSeedTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class FirstRunSeedTests
{
    private const string Saved = """{"TypeSlots":[{"Type":"Centipede","Speed":1}],"BugCount":9}""";
    private const string Seed = """{"TypeSlots":[{"Type":"HouseSpider","Speed":1}],"BugCount":12,"StartAtLogin":true}""";

    [Fact]
    public void Saved_settings_win_over_the_seed_and_leave_startup_alone()
    {
        var outcome = FirstRunSeed.Decide(Saved, Seed);

        Assert.Equal([SlotSetting.For(SpeciesId.Centipede)], outcome.Options.TypeSlots);
        Assert.Equal(9, outcome.Options.BugCount);
        Assert.Null(outcome.StartAtLogin);
    }

    [Fact]
    public void With_no_saved_settings_the_seed_is_adopted()
    {
        var outcome = FirstRunSeed.Decide(savedSettingsJson: null, Seed);

        Assert.Equal([SlotSetting.For(SpeciesId.HouseSpider)], outcome.Options.TypeSlots);
        Assert.Equal(12, outcome.Options.BugCount);
        Assert.True(outcome.StartAtLogin);
    }

    [Fact]
    public void A_seed_that_switches_startup_off_switches_it_off()
    {
        Assert.False(FirstRunSeed.Decide(null, """{"StartAtLogin":false}""").StartAtLogin);
    }

    [Fact]
    public void A_seed_that_says_nothing_about_startup_leaves_it_alone()
    {
        Assert.Null(FirstRunSeed.Decide(null, """{"BugCount":3}""").StartAtLogin);
    }

    [Fact]
    public void With_neither_file_the_app_starts_on_its_own_defaults()
    {
        var outcome = FirstRunSeed.Decide(null, null);

        Assert.Equal(BugOptions.Default, outcome.Options);
        Assert.Null(outcome.StartAtLogin);
    }

    [Fact]
    public void A_malformed_seed_yields_the_defaults_and_leaves_startup_alone()
    {
        var outcome = FirstRunSeed.Decide(null, "not json");

        Assert.Equal(BugOptions.Default, outcome.Options);
        Assert.Null(outcome.StartAtLogin);
    }

    [Fact]
    public void A_corrupt_saved_file_is_still_not_a_first_run()
    {
        var outcome = FirstRunSeed.Decide("not json", Seed);

        Assert.Equal(BugOptions.Default, outcome.Options);
        Assert.Null(outcome.StartAtLogin);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error" /tmp/b.log | head -3
```

Expected: build FAILS with `error CS0103: The name 'FirstRunSeed' does not exist`.

- [ ] **Step 3: Create the outcome**

Create `src/ScreenBugs.Core/Settings/SeedOutcome.cs`:

```csharp
namespace ScreenBugs.Core.Settings;

/// <summary>What a launch starts with, and what to do about startup registration.</summary>
/// <param name="StartAtLogin">Null to leave the Run key alone; otherwise the state to apply.</param>
public readonly record struct SeedOutcome(BugOptions Options, bool? StartAtLogin);
```

- [ ] **Step 4: Create the rule**

Create `src/ScreenBugs.Core/Settings/FirstRunSeed.cs`:

```csharp
namespace ScreenBugs.Core.Settings;

/// <summary>
/// Decides what a launch starts with: the user's own settings when they have any, otherwise the
/// installer's seed. Pure, so the whole rule is testable with no file system and no registry.
/// </summary>
public static class FirstRunSeed
{
    /// <param name="savedSettingsJson">The user's settings file content, or null when there is no file.</param>
    /// <param name="installDefaultsJson">The installer's seed content, or null when there is no file.</param>
    public static SeedOutcome Decide(string? savedSettingsJson, string? installDefaultsJson)
    {
        // A file that exists but is corrupt still counts as "not a first run". Otherwise the seed
        // would resurrect itself and re-apply a startup choice the user has since changed.
        if (savedSettingsJson is not null)
        {
            return new SeedOutcome(SettingsSerializer.Deserialize(savedSettingsJson), StartAtLogin: null);
        }

        if (installDefaultsJson is not null)
        {
            var defaults = InstallDefaults.Parse(installDefaultsJson);
            return new SeedOutcome(defaults.Options, defaults.StartAtLogin);
        }

        // No installer: running from a build output folder behaves as it always has.
        return new SeedOutcome(BugOptions.Default, StartAtLogin: null);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
dotnet test tests/ScreenBugs.Tests -nologo -v q --no-build -nodeReuse:false
```

Expected: `Passed!  - Failed: 0, Passed: 115` (108 plus these 7).

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Settings/SeedOutcome.cs src/ScreenBugs.Core/Settings/FirstRunSeed.cs tests/ScreenBugs.Tests/FirstRunSeedTests.cs
git commit -m "$(cat <<'EOF'
feat(settings): decide what a first run starts with

Saved settings always win over the installer's seed, and a saved file
that exists but is corrupt still counts as "not a first run" so the
seed cannot re-apply a startup choice the user has since changed.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Wire the seed into the app

**Files:**
- Modify: `src/ScreenBugs/Settings/SettingsStore.cs` — replace `Load()` with `TryRead()`
- Modify: `src/ScreenBugs/Settings/StartupRegistration.cs` — add `Refresh()`
- Create: `src/ScreenBugs/Settings/SettingsBootstrap.cs`
- Modify: `src/ScreenBugs/App.xaml.cs:40`

Spec 3.3 and 3.6. `SettingsStore.Load()` goes away because the decision now needs to tell "no file" apart from "unreadable file", which a `BugOptions` return cannot express. `App.xaml.cs:40` is its only caller.

`Refresh()` is the app-side half of the relocation fix in spec 5.7: an install that moves the app leaves the `Run` value pointing at the old path, and only the app can repair it. It runs on **every** launch, never creates a value that was absent, and nothing races the Options dialog because `SettingsBootstrap.Load` runs in `OnStartup` long before a dialog can exist.

- [ ] **Step 1: Replace `Load` with `TryRead`**

In `src/ScreenBugs/Settings/SettingsStore.cs`, delete the whole `Load()` method and put this in its place (keep `FilePath` and `Save` exactly as they are):

```csharp
    /// <summary>The file's text, or null when there is no file or it cannot be read.</summary>
    public static string? TryRead()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return null;
        }
    }
```

Also update the class summary, which currently says "Loads and saves the options file":

```csharp
/// <summary>Reads and writes the options file beside the crash log. Never throws.</summary>
```

- [ ] **Step 2: Add `Refresh` to `StartupRegistration`**

In `src/ScreenBugs/Settings/StartupRegistration.cs`, add this after `SetEnabled`:

```csharp
    /// <summary>
    /// Re-points an existing value at this executable, which is what keeps startup working after
    /// an install moves the app. Does nothing when startup is off, so it can never turn it on.
    /// </summary>
    public static void Refresh()
    {
        try
        {
            if (Environment.ProcessPath is not { } current)
            {
                return;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string existing)
            {
                return;
            }

            // SetEnabled writes the path quoted, so the quotes come off before comparing:
            // against the raw value this would never match and would rewrite on every launch.
            if (!string.Equals(existing.Trim('"'), current, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(ValueName, $"\"{current}\"");
            }
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
        }
    }
```

- [ ] **Step 3: Create the bootstrap**

Create `src/ScreenBugs/Settings/SettingsBootstrap.cs`:

```csharp
using System.IO;
using ScreenBugs.Diagnostics;

namespace ScreenBugs.Settings;

/// <summary>
/// Loads the options a launch starts with, seeding them from the installer's defaults the first
/// time a user runs the app. Owns the file reads; <see cref="FirstRunSeed"/> owns the rule.
/// The explicit <c>using System.IO</c> is required, as in <see cref="CrashLog"/>.
/// </summary>
public static class SettingsBootstrap
{
    public static BugOptions Load()
    {
        string? saved = SettingsStore.TryRead();
        var outcome = FirstRunSeed.Decide(saved, ReadInstallDefaults());

        if (saved is null)
        {
            SettingsStore.Save(outcome.Options);
            if (outcome.StartAtLogin is { } startAtLogin)
            {
                StartupRegistration.SetEnabled(startAtLogin);
            }
        }

        // Every launch, not just the first: an install that moved the app leaves the Run value
        // pointing at the old path, and only the app is in a position to repair it.
        StartupRegistration.Refresh();

        return outcome.Options;
    }

    /// <summary>The installer's seed, from beside the executable, or null when running unpackaged.</summary>
    private static string? ReadInstallDefaults()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, InstallDefaults.FileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return null;
        }
    }
}
```

- [ ] **Step 4: Point `App.OnStartup` at it**

In `src/ScreenBugs/App.xaml.cs`, one line changes:

```csharp
        current = SettingsBootstrap.Load();
```

- [ ] **Step 5: Build the app and the tests**

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet build src/ScreenBugs -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
dotnet test tests/ScreenBugs.Tests -nologo -v q --no-build -nodeReuse:false
```

Expected: both builds `0 Error(s)`, and still `Passed: 115`. If the app build fails with `CS0117: 'SettingsStore' does not contain a definition for 'Load'`, Step 4 was missed.

- [ ] **Step 6: Smoke-test the seeding by hand**

This is the only end-to-end check of the seed path until the installer exists, and it is worth
doing carefully. Note the seed below sets `"StartAtLogin": false`, which makes the app
**delete** `HKCU\...\Run\ScreenBugs` — on a machine where you actually use Screen Bugs that is
your own startup setting, so the commands save and restore both it and `settings.json`.

```bash
# Save your real settings and Run value out of the way first.
mv "$LOCALAPPDATA/ScreenBugs/settings.json" "$LOCALAPPDATA/ScreenBugs/settings.json.bak" 2>/dev/null
pwsh -NoProfile -c "(Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name ScreenBugs -ErrorAction SilentlyContinue).ScreenBugs | Set-Content \"\$env:TEMP\run-value.bak\""

cat > src/ScreenBugs/bin/Debug/net10.0-windows/install-defaults.json <<'JSON'
{
  "TypeSlots": [ { "Type": "HouseSpider", "Speed": 1 } ],
  "BugCount": 3,
  "FrameRate": 60,
  "OnTypeChange": "RespawnAll",
  "StartAtLogin": false
}
JSON

./src/ScreenBugs/bin/Debug/net10.0-windows/ScreenBugs.exe &
```

Confirm: three house spiders appear, not five black ants. Open Options from the tray — it shows House spider and 3, with "Run at Windows startup" unchecked. Then:

```bash
cat "$LOCALAPPDATA/ScreenBugs/settings.json"
```

Confirm it now holds `HouseSpider` and `"BugCount": 3` — the seed was adopted and saved. Exit the app, restart it, and confirm it still starts with three spiders (this time from `settings.json`, with the seed ignored).

Finally, restore your own settings:

```bash
rm src/ScreenBugs/bin/Debug/net10.0-windows/install-defaults.json
rm "$LOCALAPPDATA/ScreenBugs/settings.json"
mv "$LOCALAPPDATA/ScreenBugs/settings.json.bak" "$LOCALAPPDATA/ScreenBugs/settings.json" 2>/dev/null
# IsNullOrWhiteSpace, not .Trim(): with startup previously off the backup is a 0-byte file,
# Get-Content -Raw returns null, and calling a method on it throws a red error.
pwsh -NoProfile -c "
\$backup = \"\$env:TEMP\run-value.bak\"
\$saved = if (Test-Path \$backup) { Get-Content \$backup -Raw } else { \$null }
if (-not [string]::IsNullOrWhiteSpace(\$saved)) {
  Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name ScreenBugs -Value \$saved.Trim()
  'Run value restored'
} else { 'no Run value to restore' }
Remove-Item \$backup -ErrorAction SilentlyContinue"
```

- [ ] **Step 7: Commit**

```bash
git add src/ScreenBugs/Settings/ src/ScreenBugs/App.xaml.cs
git commit -m "$(cat <<'EOF'
feat(settings): adopt the installer's seed on a first run

SettingsStore.Load is replaced by TryRead, because the first-run rule
has to tell "no file" apart from "unreadable file" and a BugOptions
return cannot say which.

StartupRegistration.Refresh re-points an existing Run value at this
executable on every launch. An install that moves the app would
otherwise leave the value naming the old path, with nothing able to
repair it; IsEnabled deliberately reports true whatever path it holds,
so nothing would have noticed.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

## Chunk 2: Branding assets

The app has no icon file today — the ant is drawn at runtime, so `ScreenBugs.exe` carries the generic .NET icon and its shortcuts would too. This chunk extracts the glyph so one copy of the geometry serves the tray, the executable and the installer.

### Task 4: Extract the glyph and add version metadata

**Files:**
- Create: `src/ScreenBugs/Tray/AntGlyph.cs`
- Modify: `src/ScreenBugs/Tray/TrayIconFactory.cs`
- Create: `Directory.Build.props`

Spec 3.4 and 3.5. `TrayIconFactory.Draw()` currently hard-codes 32×32; the geometry moves out unchanged, parameterized by size and colour. GDI+ scales the pen width with the world transform, so the glyph keeps its proportions from 16px to 256px.

- [ ] **Step 1: Create `AntGlyph`**

Create `src/ScreenBugs/Tray/AntGlyph.cs`. The coordinates are copied verbatim from `TrayIconFactory.Draw`; the only additions are the size parameter and the scale transform. `ScreenBugs.csproj` removes the implicit `System.Drawing` using, so both usings below are required:

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScreenBugs.Tray;

/// <summary>
/// The ant the app identifies itself with, drawn at any size. One copy of the geometry, shared by
/// the tray icon, the window title bars and the installer's icon generator.
/// </summary>
public static class AntGlyph
{
    /// <summary>The coordinate space the geometry below is written in.</summary>
    public const int DesignSize = 32;

    public static Bitmap Draw(int size, Color color)
    {
        var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        using (var brush = new SolidBrush(color))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // The world transform scales the pen width along with the geometry, so the legs stay
            // proportional at every size.
            float scale = size / (float)DesignSize;
            graphics.ScaleTransform(scale, scale);

            graphics.DrawLine(pen, 12, 11, 4, 6);
            graphics.DrawLine(pen, 20, 11, 28, 6);
            graphics.DrawLine(pen, 12, 15, 3, 16);
            graphics.DrawLine(pen, 20, 15, 29, 16);
            graphics.DrawLine(pen, 12, 19, 5, 26);
            graphics.DrawLine(pen, 20, 19, 27, 26);
            graphics.DrawLine(pen, 14, 5, 10, 1);
            graphics.DrawLine(pen, 18, 5, 22, 1);

            graphics.FillEllipse(brush, 11, 3, 10, 9);
            graphics.FillEllipse(brush, 12, 11, 8, 9);
            graphics.FillEllipse(brush, 10, 19, 12, 12);
        }

        return bitmap;
    }
}
```

- [ ] **Step 2: Point `TrayIconFactory` at it**

In `src/ScreenBugs/Tray/TrayIconFactory.cs`, replace the whole `Draw()` method with:

```csharp
    /// <summary>
    /// A black ant reads well on a light taskbar but vanishes on Windows 11's default dark one,
    /// so the dark theme gets a red ant instead.
    /// </summary>
    private static Bitmap Draw() =>
        AntGlyph.Draw(
            AntGlyph.DesignSize,
            TaskbarIsLight() ? Color.FromArgb(24, 24, 24) : Color.FromArgb(216, 50, 31));
```

Then delete the now-unused `using System.Drawing.Drawing2D;` from the top of the file. Keep `using System.Drawing;` and `using Microsoft.Win32;`.

- [ ] **Step 3: Create `Directory.Build.props`**

Create `Directory.Build.props` at the repository root. This is the single version source for the app, the installer and the file properties Windows shows. `Company` is what Add/Remove Programs displays as Publisher:

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

- [ ] **Step 4: Build and check the version flowed through**

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet build src/ScreenBugs -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
dotnet msbuild Directory.Build.props -getProperty:Version -nologo
pwsh -NoProfile -c "(Get-Item src/ScreenBugs/bin/Debug/net10.0-windows/ScreenBugs.exe).VersionInfo | Format-List ProductName,ProductVersion,CompanyName"
```

Expected: `0 Error(s)`; `1.0.0`; and the executable reporting ProductName `Screen Bugs`, ProductVersion `1.0.0`, CompanyName `Addam Boord`.

- [ ] **Step 5: Confirm the tray icon still looks right**

```bash
./src/ScreenBugs/bin/Debug/net10.0-windows/ScreenBugs.exe &
```

The tray icon must be the same ant as before — this step exists because the scale transform is new code in a path that had none. Exit the app afterwards.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs/Tray/AntGlyph.cs src/ScreenBugs/Tray/TrayIconFactory.cs Directory.Build.props
git commit -m "$(cat <<'EOF'
refactor(tray): draw the ant glyph at any size

The geometry moves out of TrayIconFactory behind AntGlyph.Draw(size,
color) so the icon generator can reuse it instead of copying the
coordinates. A world transform scales the pen with the geometry.

Directory.Build.props becomes the single version source for the app,
the installer and the properties Windows shows.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Generate the icon and wizard images

**Files:**
- Create: `tools/IconGen/IconGen.csproj`
- Create: `tools/IconGen/IcoWriter.cs`
- Create: `tools/IconGen/WizardBitmaps.cs`
- Create: `tools/IconGen/Program.cs`
- Create (generated): `assets/ScreenBugs.ico`, `assets/wizard-side.bmp`, `assets/wizard-header.bmp`
- Modify: `ScreenBugs.slnx`

Spec 4. `System.Drawing` cannot save a multi-size icon — `Bitmap.Save` with `ImageFormat.Icon` writes one low-colour image — so `IcoWriter` writes the container by hand. This runs once and its output is checked in; building the installer does not require running it.

Order matters: the assets must exist **before** Task 6 sets `ApplicationIcon`, because `IconGen` project-references `ScreenBugs.csproj` and would otherwise fail to build against a missing icon.

- [ ] **Step 1: Create the project**

Create `tools/IconGen/IconGen.csproj`. `UseWindowsForms` is what brings in the Windows Desktop framework reference that `System.Drawing` needs; the project reference is what gives it `AntGlyph`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>IconGen</RootNamespace>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ScreenBugs\ScreenBugs.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the ICO writer**

Create `tools/IconGen/IcoWriter.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace IconGen;

/// <summary>
/// Packs bitmaps into a multi-size .ico, which System.Drawing cannot save: Bitmap.Save with
/// ImageFormat.Icon writes a single low-colour image. Each entry carries a PNG payload, which
/// Windows has accepted since Vista.
/// </summary>
public static class IcoWriter
{
    private const int DirectoryEntrySize = 16;
    private const int HeaderSize = 6;

    public static void Write(string path, IReadOnlyList<Bitmap> images)
    {
        var payloads = new List<byte[]>(images.Count);
        foreach (var image in images)
        {
            using var buffer = new MemoryStream();
            image.Save(buffer, ImageFormat.Png);
            payloads.Add(buffer.ToArray());
        }

        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);

        writer.Write((short)0);                 // reserved
        writer.Write((short)1);                 // resource type: icon
        writer.Write((short)images.Count);

        int offset = HeaderSize + (DirectoryEntrySize * images.Count);
        for (int i = 0; i < images.Count; i++)
        {
            // The width and height fields are one byte each, so 256 is stored as 0.
            writer.Write((byte)(images[i].Width == 256 ? 0 : images[i].Width));
            writer.Write((byte)(images[i].Height == 256 ? 0 : images[i].Height));
            writer.Write((byte)0);              // palette size: none, it is a PNG
            writer.Write((byte)0);              // reserved
            writer.Write((short)1);             // colour planes
            writer.Write((short)32);            // bits per pixel
            writer.Write(payloads[i].Length);
            writer.Write(offset);
            offset += payloads[i].Length;
        }

        foreach (byte[] payload in payloads)
        {
            writer.Write(payload);
        }
    }
}
```

- [ ] **Step 3: Create the wizard bitmaps**

Create `tools/IconGen/WizardBitmaps.cs`. Both images are 24-bit: a 32bpp save carries an alpha channel that MUI2 composites against black instead of the page background:

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ScreenBugs.Tray;

namespace IconGen;

/// <summary>The MUI2 wizard images: the welcome/finish side panel and the inner page header.</summary>
public static class WizardBitmaps
{
    private static readonly Color Ant = Color.FromArgb(216, 50, 31);
    private static readonly Color Panel = Color.FromArgb(250, 250, 250);

    /// <summary>The welcome and finish panel: one large ant, centred, low on the panel.</summary>
    public static Bitmap Side(int width, int height) =>
        Compose(width, height, glyphSize: 132, x: (width - 132) / 2, y: height - 168);

    /// <summary>The inner page header strip: a small ant at the right, clear of the title.</summary>
    public static Bitmap Header(int width, int height) =>
        Compose(width, height, glyphSize: 44, x: width - 52, y: (height - 44) / 2);

    private static Bitmap Compose(int width, int height, int glyphSize, int x, int y)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var glyph = AntGlyph.Draw(glyphSize, Ant))
        {
            graphics.Clear(Panel);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(glyph, x, y);
        }

        return bitmap;
    }
}
```

- [ ] **Step 4: Create the entry point**

Create `tools/IconGen/Program.cs`. The repository root is found by walking up for `ScreenBugs.slnx`, so the tool works whatever the working directory or build configuration:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using IconGen;
using ScreenBugs.Tray;

// Regenerates the checked-in branding assets from the app's own ant glyph.
// Run: dotnet run --project tools/IconGen

// The file icon cannot follow the system theme the way the tray glyph does, so it is always the
// dark-theme red: legible on both light and dark backgrounds, where near-black disappears.
var color = Color.FromArgb(216, 50, 31);
int[] sizes = [16, 24, 32, 48, 64, 128, 256];

string assets = Path.Combine(FindRepositoryRoot(), "assets");
Directory.CreateDirectory(assets);

var images = sizes.Select(size => AntGlyph.Draw(size, color)).ToList();
try
{
    IcoWriter.Write(Path.Combine(assets, "ScreenBugs.ico"), images);
}
finally
{
    foreach (var image in images)
    {
        image.Dispose();
    }
}

using (var side = WizardBitmaps.Side(164, 314))
{
    side.Save(Path.Combine(assets, "wizard-side.bmp"), ImageFormat.Bmp);
}

using (var header = WizardBitmaps.Header(150, 57))
{
    header.Save(Path.Combine(assets, "wizard-header.bmp"), ImageFormat.Bmp);
}

Console.WriteLine($"Wrote ScreenBugs.ico ({sizes.Length} sizes), wizard-side.bmp and wizard-header.bmp to {assets}");

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScreenBugs.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName
        ?? throw new InvalidOperationException("Could not find ScreenBugs.slnx in any parent directory.");
}
```

- [ ] **Step 5: Add the tool to the solution**

In `ScreenBugs.slnx`, add a `/tools/` folder after the `/tests/` one:

```xml
  <Folder Name="/tools/">
    <Project Path="tools/IconGen/IconGen.csproj" />
  </Folder>
```

- [ ] **Step 6: Run it**

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet run --project tools/IconGen
ls -la assets/
```

Expected: the three files exist, at these sizes. All three were measured on this machine; they are not estimates:

- `ScreenBugs.ico` roughly 11–12 KB. The glyph is flat single-colour, so its seven PNG payloads
  compress hard. Much above ~30 KB suggests the images are not PNG-compressed.
- `wizard-side.bmp` **exactly 154,542** bytes and `wizard-header.bmp` **exactly 25,818** bytes.
  A 24-bit BMP is a fixed size for its dimensions — a 54-byte header plus a 4-byte-aligned
  stride times the height (164×3 = 492, already aligned, so 54 + 492×314; 150×3 = 450 padded
  to 452, so 54 + 452×57). Any other size means the pixel format is not `Format24bppRgb`.

- [ ] **Step 7: Look at the icon**

Read the icon through WIC, **not** `System.Drawing.Icon`: GDI+ cannot decode a PNG-compressed
256px entry and silently hands back the 128px one instead, which would make a correct icon look
like a failed write.

```bash
pwsh -NoProfile -c "
Add-Type -AssemblyName PresentationCore
\$stream = [System.IO.File]::OpenRead((Resolve-Path 'assets/ScreenBugs.ico'))
\$decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(\$stream, 'None', 'OnLoad')
'frames: ' + ((\$decoder.Frames | ForEach-Object { \"\$(\$_.PixelWidth)x\$(\$_.PixelHeight)\" }) -join ' ')
\$largest = \$decoder.Frames | Sort-Object PixelWidth -Descending | Select-Object -First 1
\$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
\$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create(\$largest))
\$out = [System.IO.File]::Create(\"\$env:TEMP\icon-largest.png\")
\$encoder.Save(\$out); \$out.Close(); \$stream.Close()
\"wrote \$env:TEMP\icon-largest.png at \$(\$largest.PixelWidth)px\"
"
```

Expected: seven frames — `16x16 24x24 32x32 48x48 64x64 128x128 256x256` — and a 256px PNG
written. Open that PNG and `assets/wizard-side.bmp` and confirm a recognisable red ant,
antialiased and proportional, on a transparent (icon) and near-white (bitmap) background. If the
legs look hairline-thin at 256px the scale transform is not being applied.

- [ ] **Step 8: Commit**

```bash
git add tools/IconGen/ assets/ ScreenBugs.slnx
git commit -m "$(cat <<'EOF'
feat(assets): generate the ant icon and wizard images

IcoWriter packs the container by hand because System.Drawing cannot
save a multi-size icon. The wizard bitmaps are 24-bit: a 32bpp save
carries an alpha channel MUI2 composites against black.

The tool reuses AntGlyph rather than copying the coordinates, so the
installer's ant cannot drift from the tray's.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Give the executable its icon

**Files:**
- Modify: `src/ScreenBugs/ScreenBugs.csproj`

- [ ] **Step 1: Add `ApplicationIcon`**

In `src/ScreenBugs/ScreenBugs.csproj`, add to the main `PropertyGroup` next to `ApplicationManifest`. The path is resolved relative to the project directory:

```xml
    <ApplicationIcon>..\..\assets\ScreenBugs.ico</ApplicationIcon>
```

- [ ] **Step 2: Build and confirm the icon is embedded**

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet build src/ScreenBugs -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
pwsh -NoProfile -c "Add-Type -AssemblyName System.Drawing; \$i = [System.Drawing.Icon]::ExtractAssociatedIcon((Resolve-Path 'src/ScreenBugs/bin/Debug/net10.0-windows/ScreenBugs.exe')); \$i.ToBitmap().Save(\"\$env:TEMP\exe-icon.png\"); \$i.Dispose(); \"wrote \$env:TEMP\exe-icon.png\""
```

Expected: `0 Error(s)` and the path it wrote. Open that PNG — it must be the ant, not the generic
.NET icon. `ExtractAssociatedIcon` returns the 32px entry, which GDI+ handles fine; only the
PNG-compressed 256px entry defeats it. Also check `src/ScreenBugs/bin/Debug/net10.0-windows/` in Explorer with large icons.

- [ ] **Step 3: Commit**

```bash
git add src/ScreenBugs/ScreenBugs.csproj
git commit -m "$(cat <<'EOF'
feat(app): give ScreenBugs.exe the ant icon

Without this the executable, its shortcuts and the Add/Remove Programs
entry all show the generic .NET icon.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

## Chunk 3: The installer

Four tasks, each ending in a clean `makensis` compile. NSIS has no unit-test framework, so the compile is the fast feedback loop and `verify-install.ps1` (Chunk 4) is the real test.

**Compiling during development.** A solid-LZMA compress of the real 155 MB payload takes minutes, which is far too slow for the inner loop. Tasks 7–9 therefore compile against a scratch payload. Create it once, now, and use it for every compile in this chunk:

```bash
SCRATCH=/tmp/sb-payload
mkdir -p "$SCRATCH" && echo "placeholder" > "$SCRATCH/ScreenBugs.exe"
```

The compile command used throughout this chunk. Note `-D`, not `/D`, per the header — with `/D`
the `VERSION` define is mangled by MSYS2 and makensis fails with `Can't open script`:

```bash
MAKENSIS="/c/Program Files (x86)/NSIS/makensis.exe"
REPO="C:\Users\AddamBoord\source\repos\ScreenSavers"
TMPW="C:\Users\ADDAMB~1\AppData\Local\Temp"
"$MAKENSIS" -V2 "-DVERSION=1.0.0" "-DASSETS_DIR=$REPO\assets" \
  "-DPUBLISH_DIR=$TMPW\sb-payload" \
  "-DOUT_FILE=$TMPW\ScreenBugs-Setup-dev.exe" \
  installer/ScreenBugs.nsi
```

### Task 7: Script skeleton, scope and pages

**Files:**
- Create: `installer/ScreenBugs.nsi`
- Create: `installer/options-page.nsh`

Spec 5.1 to 5.4 and 5.6. Both files are created together because neither compiles without the other.

Three things here are load-bearing and easy to get subtly wrong:

1. **`SetRegView 64` must run before `${MULTIUSER_INIT}`.** The macro does the `HKLM` reads that pre-select a prior install's scope and directory, and those reads obey the current view. The installer stub is 32-bit, so in the default view they resolve under `WOW6432Node` — not where the uninstall key is written — and every all-users upgrade silently falls back to the defaults with no error. `SetRegView` touches neither `$INSTDIR` nor `$MultiUser.InstallMode`, so it is the one exception to rule 2.
2. **Everything else in `.onInit` must run after `${MULTIUSER_INIT}`**, which overwrites `$INSTDIR` and `$MultiUser.InstallMode`.
3. **The finish-page launcher branches on `$MultiUser.Privileges`, not install mode.** An administrator is elevated *before* the mode page, so one who then picks "only for me" is still elevated; branching on mode would hand the app an elevated token, which is the whole thing this avoids.

- [ ] **Step 1: Create the options page**

Create `installer/options-page.nsh`:

```nsi
; The custom options page (spec 5.3) and the validation shared with silent mode (spec 5.4).
; Included by ScreenBugs.nsi, which declares the $BugType/$BugCount/$Startup/$DesktopShortcut vars.

Var Dialog
Var TypeBox
Var CountBox
Var CountUpDown
Var StartupBox
Var DesktopBox

Function OptionsPage
  !insertmacro MUI_HEADER_TEXT "Options" "Choose what Screen Bugs starts with."

  nsDialogs::Create 1018
  Pop $Dialog
  ${If} $Dialog == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 2u 60u 12u "Bug type"
  Pop $R0
  ${NSD_CreateDroplist} 62u 0 120u 12u ""
  Pop $TypeBox
  ; Random first, then the nine species in SpeciesId order. BugTypeNameFor below maps the
  ; selected index back to the enum name, and the two lists sit next to each other on purpose.
  ${NSD_CB_AddString} $TypeBox "Random"
  ${NSD_CB_AddString} $TypeBox "Hissing cockroach"
  ${NSD_CB_AddString} $TypeBox "Black garden ant"
  ${NSD_CB_AddString} $TypeBox "Red fire ant"
  ${NSD_CB_AddString} $TypeBox "Praying mantis"
  ${NSD_CB_AddString} $TypeBox "Seven-spot ladybug"
  ${NSD_CB_AddString} $TypeBox "Stag beetle"
  ${NSD_CB_AddString} $TypeBox "House spider"
  ${NSD_CB_AddString} $TypeBox "Centipede"
  ${NSD_CB_AddString} $TypeBox "Stink bug"
  ${NSD_CB_SelectString} $TypeBox "Black garden ant"

  ${NSD_CreateLabel} 0 22u 60u 12u "Bugs on screen"
  Pop $R0
  ; A spinner, not a slider: nsDialogs routes WM_NOTIFY but not the WM_HSCROLL a trackbar
  ; sends, so a live value label beside a slider would need dialog subclassing.
  ${NSD_CreateNumber} 62u 20u 30u 12u "$BugCount"
  Pop $CountBox
  ${NSD_CreateAutoUpDown} $CountBox
  Pop $CountUpDown
  ${NSD_UD_SetRange32} $CountUpDown 1 50

  ${NSD_CreateCheckBox} 0 42u 100% 12u "Run Screen Bugs when I sign in to Windows"
  Pop $StartupBox
  ${If} $Startup == "1"
    ${NSD_SetState} $StartupBox ${BST_CHECKED}
  ${EndIf}

  ${NSD_CreateCheckBox} 0 56u 100% 12u "Create a desktop shortcut"
  Pop $DesktopBox
  ${If} $DesktopShortcut == "1"
    ${NSD_SetState} $DesktopBox ${BST_CHECKED}
  ${EndIf}

  ; Unconditional on purpose. Detecting whether *this* user already has a settings.json means
  ; reading a per-user path from a possibly elevated installer, which under over-the-shoulder
  ; elevation reads the wrong profile and would show a misleading hint. This sentence is always true.
  ${NSD_CreateLabel} 0 76u 100% 24u "These apply the first time each user runs Screen Bugs. If you've used it before, your saved settings are kept — change them from Options in the tray menu."
  Pop $R0

  nsDialogs::Show
FunctionEnd

Function OptionsPageLeave
  ${NSD_GetText} $CountBox $BugCount
  ${NSD_GetState} $StartupBox $Startup
  ${NSD_GetState} $DesktopBox $DesktopShortcut
  ${NSD_CB_GetSelectionIndex} $TypeBox $R0
  Call BugTypeNameFor
  Call ValidateOptions
FunctionEnd

; Index in the droplist above -> the SpeciesId name the seed file needs.
Function BugTypeNameFor
  ${Switch} $R0
    ${Case} 0
      StrCpy $BugType "Random"
      ${Break}
    ${Case} 1
      StrCpy $BugType "HissingCockroach"
      ${Break}
    ${Case} 2
      StrCpy $BugType "BlackGardenAnt"
      ${Break}
    ${Case} 3
      StrCpy $BugType "RedFireAnt"
      ${Break}
    ${Case} 4
      StrCpy $BugType "PrayingMantis"
      ${Break}
    ${Case} 5
      StrCpy $BugType "SevenSpotLadybug"
      ${Break}
    ${Case} 6
      StrCpy $BugType "StagBeetle"
      ${Break}
    ${Case} 7
      StrCpy $BugType "HouseSpider"
      ${Break}
    ${Case} 8
      StrCpy $BugType "Centipede"
      ${Break}
    ${Case} 9
      StrCpy $BugType "StinkBug"
      ${Break}
    ${Default}
      StrCpy $BugType "BlackGardenAnt"
      ${Break}
  ${EndSwitch}
FunctionEnd

; Silent-mode values come off the command line, so they need the same clamping the page does.
; An unknown type falls back to the default rather than failing a deployment over a cosmetic
; option; verify-install.ps1 asserts the written seed, so a typo in a script surfaces there.
Function ValidateOptions
  ${If} $BugType != "Random"
  ${AndIf} $BugType != "HissingCockroach"
  ${AndIf} $BugType != "BlackGardenAnt"
  ${AndIf} $BugType != "RedFireAnt"
  ${AndIf} $BugType != "PrayingMantis"
  ${AndIf} $BugType != "SevenSpotLadybug"
  ${AndIf} $BugType != "StagBeetle"
  ${AndIf} $BugType != "HouseSpider"
  ${AndIf} $BugType != "Centipede"
  ${AndIf} $BugType != "StinkBug"
    StrCpy $BugType "BlackGardenAnt"
  ${EndIf}

  ; A non-numeric value compares as 0 here, so it clamps to 1 rather than reaching the seed.
  ${If} $BugCount < 1
    StrCpy $BugCount "1"
  ${ElseIf} $BugCount > 50
    StrCpy $BugCount "50"
  ${EndIf}

  ${If} $Startup != "0"
    StrCpy $Startup "1"
  ${EndIf}
  ${If} $DesktopShortcut != "1"
    StrCpy $DesktopShortcut "0"
  ${EndIf}
FunctionEnd
```

- [ ] **Step 2: Create the main script**

Create `installer/ScreenBugs.nsi`. **Save it as UTF-8 with a BOM.** makensis reads a script as
ANSI unless it finds one — it announces this as `(ACP)` in its log — and without the BOM the `©`
in the copyright string reaches the built executable as `Â©`, and the em dash in the options
page's label is mangled on screen. Step 5 checks both. If your editor writes no BOM:

```bash
pwsh -NoProfile -c "
foreach (\$f in 'installer/ScreenBugs.nsi','installer/options-page.nsh') {
  \$text = Get-Content \$f -Raw
  [System.IO.File]::WriteAllText((Resolve-Path \$f), \$text, (New-Object System.Text.UTF8Encoding \$true))
}
'BOMs written'"
```

```nsi
; Screen Bugs installer. Built by build/build-installer.ps1, which supplies VERSION,
; ASSETS_DIR, PUBLISH_DIR and OUT_FILE as absolute paths.
; Design: docs/superpowers/specs/2026-09-04-installer-design.md

Unicode true

!ifndef VERSION
  !error "VERSION must be defined, e.g. makensis -DVERSION=1.0.0"
!endif
!ifndef ASSETS_DIR
  !error "ASSETS_DIR must be defined: the directory holding ScreenBugs.ico and the wizard bitmaps."
!endif
!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR must be defined: the self-contained publish to package."
!endif
!ifndef OUT_FILE
  !error "OUT_FILE must be defined: the setup executable to write."
!endif

Name "Screen Bugs"
OutFile "${OUT_FILE}"
SetCompressor /SOLID lzma

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "Screen Bugs"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "Addam Boord"
VIAddVersionKey "LegalCopyright" "Copyright © 2026 Addam Boord"
VIAddVersionKey "FileDescription" "Screen Bugs setup"

!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define MUTEX_NAME "Local\ScreenBugs.SingleInstance"

; --- Scope. Stock MultiUser does the per-scope defaulting, including reading a prior
;     install's location back off the uninstall key, so none of that is hand-written.
;     MULTIUSER_USE_PROGRAMFILES64 is not optional: without it the all-users default is
;     32-bit Program Files, which is wrong for a win-x64 payload.
!define MULTIUSER_EXECUTIONLEVEL Highest
!define MULTIUSER_MUI
!define MULTIUSER_INSTALLMODE_COMMANDLINE
!define MULTIUSER_INSTALLMODE_INSTDIR "ScreenBugs"
!define MULTIUSER_USE_PROGRAMFILES64
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "${UNINSTALL_KEY}"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallLocation"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "${UNINSTALL_KEY}"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallLocation"

!define MUI_ABORTWARNING
!define MUI_ICON "${ASSETS_DIR}\ScreenBugs.ico"
!define MUI_UNICON "${ASSETS_DIR}\ScreenBugs.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP "${ASSETS_DIR}\wizard-header.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${ASSETS_DIR}\wizard-side.bmp"
; MUI_UNWELCOMEFINISHPAGE_BITMAP is deliberately unset: the uninstaller has no welcome or
; finish page for it to appear on.

; An empty MUI_FINISHPAGE_RUN is what makes the checkbox appear at all; the FUNCTION alone
; renders no checkbox and never runs.
!define MUI_FINISHPAGE_RUN ""
!define MUI_FINISHPAGE_RUN_TEXT "Run Screen Bugs"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp

!include MUI2.nsh
!include MultiUser.nsh
!include nsDialogs.nsh
!include LogicLib.nsh
!include FileFunc.nsh
!include WinVer.nsh
!include x64.nsh

!insertmacro GetParameters
!insertmacro GetOptions
!insertmacro GetSize
!insertmacro un.GetParameters
!insertmacro un.GetOptions

; $DesktopShortcut, not $Desktop: "Desktop" is in use by the $DESKTOP constant and will
; not compile.
Var BugType
Var BugCount
Var Startup
Var DesktopShortcut
Var Upgrade
Var DeleteData
Var LocalData

!include "${__FILEDIR__}\options-page.nsh"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MULTIUSER_PAGE_INSTALLMODE
!insertmacro MUI_PAGE_DIRECTORY
Page custom OptionsPage OptionsPageLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
; The custom uninstall page is inserted here in Task 9, once its functions exist. Referencing
; them now would fail the compile with: resolving create-page function "un.OptionsPage".
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ; Before MULTIUSER_INIT, deliberately. The macro performs the HKLM reads that pre-select a
  ; prior install's scope and directory, and those reads obey the registry view; the stub is
  ; 32-bit, so in the default view they resolve under WOW6432Node instead of where the
  ; uninstall key is written, and every all-users upgrade silently falls back to the defaults.
  SetRegView 64

  ; NSIS fills $INSTDIR from /D= before .onInit runs, and MULTIUSER_INIT then overwrites it with
  ; the per-scope default -- which would make /D= silently do nothing. This script sets no
  ; InstallDir, so a non-empty $INSTDIR here can only have come from /D=: keep it and put it back.
  StrCpy $R9 $INSTDIR
  !insertmacro MULTIUSER_INIT
  ${If} $R9 != ""
    StrCpy $INSTDIR $R9
  ${EndIf}

  ; Everything below must stay after the macro, which overwrites $INSTDIR and
  ; $MultiUser.InstallMode.
  ; Spec 5.5 lists these as the install section's first step; here they are, deliberately,
  ; in .onInit, so an unsupported machine is turned away before the wizard rather than after.
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "Screen Bugs requires 64-bit Windows."
    Abort
  ${EndIf}
  ; Windows 10 1607 is .NET 10's floor; ${AtLeastWin10} would admit earlier builds.
  ${IfNot} ${AtLeastBuild} 14393
    MessageBox MB_OK|MB_ICONSTOP "Screen Bugs requires Windows 10 version 1607 or later."
    Abort
  ${EndIf}

  StrCpy $BugType "BlackGardenAnt"
  StrCpy $BugCount "5"
  StrCpy $Startup "1"
  StrCpy $DesktopShortcut "0"

  ${GetParameters} $R0
  ${GetOptions} $R0 "/BUGTYPE=" $BugType
  ${GetOptions} $R0 "/BUGCOUNT=" $BugCount
  ${GetOptions} $R0 "/STARTUP=" $Startup
  ${GetOptions} $R0 "/DESKTOP=" $DesktopShortcut
  Call ValidateOptions
FunctionEnd

Function un.onInit
  SetRegView 64
  !insertmacro MULTIUSER_UNINIT

  StrCpy $Upgrade "0"
  StrCpy $DeleteData "0"
  ${un.GetParameters} $R0
  ${un.GetOptions} $R0 "/UPGRADE=" $Upgrade
  ${un.GetOptions} $R0 "/DELETEDATA=" $DeleteData

  ; With SetShellVarContext all, $LOCALAPPDATA resolves to C:\ProgramData, so the real
  ; per-user path is captured here and the context put back where MULTIUSER_UNINIT left it.
  SetShellVarContext current
  StrCpy $LocalData "$LOCALAPPDATA\ScreenBugs"
  ${If} $MultiUser.InstallMode == "AllUsers"
    SetShellVarContext all
  ${EndIf}
FunctionEnd

Function LaunchApp
  ; Privileges, NOT install mode. An administrator is elevated before the mode page, so one
  ; who then picks "only for me" is still elevated; an elevated tray app would run its
  ; click-through overlay elevated and write its HKCU values into the wrong hive.
  ${If} $MultiUser.Privileges == "Admin"
  ${OrIf} $MultiUser.Privileges == "Power"
    Exec '"$WINDIR\explorer.exe" "$INSTDIR\ScreenBugs.exe"'
  ${Else}
    Exec '"$INSTDIR\ScreenBugs.exe"'
  ${EndIf}
FunctionEnd

Section "Install"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  ${If} ${FileExists} "$INSTDIR\ScreenBugs.exe"
    RMDir /r "$INSTDIR"
  ${EndIf}
SectionEnd
```

- [ ] **Step 3: Compile**

```bash
MAKENSIS="/c/Program Files (x86)/NSIS/makensis.exe"
REPO="C:\Users\AddamBoord\source\repos\ScreenSavers"
TMPW="C:\Users\ADDAMB~1\AppData\Local\Temp"
"$MAKENSIS" -V2 "-DVERSION=1.0.0" "-DASSETS_DIR=$REPO\assets" \
  "-DPUBLISH_DIR=$TMPW\sb-payload" \
  "-DOUT_FILE=$TMPW\ScreenBugs-Setup-dev.exe" \
  installer/ScreenBugs.nsi
echo "exit: $?"
```

Expected: exit 0, no errors. Unused-variable warnings are expected at this stage — `$Upgrade`,
`$DeleteData` and `$LocalData` are not read until Task 9. An `!error` about a missing define
means one of the four `-D` arguments was dropped.

- [ ] **Step 4: Check the copyright string survived the encoding**

```bash
pwsh -NoProfile -c "(Get-Item \"\$env:TEMP\ScreenBugs-Setup-dev.exe\").VersionInfo.LegalCopyright"
```

Expected: `Copyright © 2026 Addam Boord`. If it reads `Copyright Â© 2026 Addam Boord`, the script
has no UTF-8 BOM — redo Step 2's BOM command and recompile.

- [ ] **Step 5: Click through the wizard**

```bash
pwsh -NoProfile -c "Start-Process -FilePath \"\$env:TEMP\ScreenBugs-Setup-dev.exe\" -Wait"
```

This installs the placeholder payload, so install it somewhere disposable — take the current-user option and set the directory to `C:\Temp\sb-dev`. Confirm: the ant appears on the welcome panel and in the page headers; the mode page offers both scopes; the options page shows the droplist defaulting to Black garden ant, the spinner at 5 with working arrows that refuse to pass 1 or 50, both checkboxes with startup checked, and the note (its em dash must render as a dash, not `â€”`); the finish page shows a checked
"Run Screen Bugs". Untick it before finishing, then delete `C:\Temp\sb-dev`.

- [ ] **Step 6: Commit**

```bash
git add installer/
git commit -m "$(cat <<'EOF'
feat(installer): NSIS skeleton, dual scope and the options page

SetRegView 64 runs before MULTIUSER_INIT on purpose: the macro's HKLM
reads pre-select a prior install's scope and directory, and in the
32-bit stub's default view they would resolve under WOW6432Node, so
every all-users upgrade would silently fall back to the defaults.

The finish-page launcher branches on $MultiUser.Privileges rather than
install mode, because an admin who picks "only for me" is still
elevated and would otherwise hand the app an elevated token.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: The install section

**Files:**
- Modify: `installer/ScreenBugs.nsi` — replace the placeholder `Section "Install"`

Spec 5.5 and 5.7. The seed write is the point of the whole exercise; everything around it is housekeeping.

`/UPGRADE=1` on the relocation uninstall is not optional. Without it the old uninstaller deletes the `HKCU` `Run` value, and nothing puts it back — a returning user has a `settings.json`, so `FirstRunSeed` takes rule 1 and leaves the `Run` key alone by design. The user's startup choice would silently vanish.

- [ ] **Step 1: Replace the install section**

In `installer/ScreenBugs.nsi`, replace the placeholder `Section "Install" ... SectionEnd` with:

```nsi
Section "Install"
  ; --- Close a running instance, detected through the app's own single-instance mutex.
  ;     The mutex is session-local, so an elevated installer in the same session still sees
  ;     it. An instance under a different user is invisible here and will instead lock the
  ;     files, which NSIS's standard retry prompt covers.
  System::Call 'kernel32::OpenMutex(i 0x00100000, i 0, t "${MUTEX_NAME}") p .r0'
  ${If} $0 <> 0
    System::Call 'kernel32::CloseHandle(p r0)'
    ${IfNot} ${Silent}
      MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
        "Screen Bugs is running and must be closed to continue." IDOK +2
      Abort "Installation cancelled: Screen Bugs is still running."
    ${EndIf}
    ; /F denies the app its OnExit, which is harmless: settings are saved when the Options
    ; dialog is accepted, not at exit, and SingleInstanceGuard treats an abandoned mutex as
    ; a free slot.
    nsExec::ExecToStack 'taskkill /F /IM ScreenBugs.exe'
    Pop $0
    Pop $1
    Sleep 500
  ${EndIf}

  ; --- A prior install somewhere else: remove it so the machine cannot end up with two
  ;     copies. _?= keeps the uninstaller from relocating itself to $TEMP, which is what
  ;     makes ExecWait actually wait; it also stops it deleting its own file, hence the two
  ;     lines after. /UPGRADE=1 keeps the user's Run value, which only the app can re-point.
  StrCpy $R0 ""
  ReadRegStr $R0 HKLM "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $R0 == ""
    ReadRegStr $R0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${EndIf}
  ${If} $R0 != ""
  ${AndIf} $R0 != "$INSTDIR"
  ${AndIf} ${FileExists} "$R0\Uninstall.exe"
    DetailPrint "Removing the previous installation in $R0..."
    ExecWait '"$R0\Uninstall.exe" /S /UPGRADE=1 _?=$R0'
    Delete "$R0\Uninstall.exe"
    RMDir "$R0"
  ${EndIf}

  ; --- Files
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"

  ; --- The seed (spec 2.3). One slot at speed 1, 60 fps: install time offers no control
  ;     over slot count, per-slot speed, frame rate or type-change behaviour.
  DetailPrint "Writing install-defaults.json..."
  FileOpen $0 "$INSTDIR\install-defaults.json" w
  FileWrite $0 '{$\r$\n'
  FileWrite $0 '  "TypeSlots": [ { "Type": "$BugType", "Speed": 1 } ],$\r$\n'
  FileWrite $0 '  "BugCount": $BugCount,$\r$\n'
  FileWrite $0 '  "FrameRate": 60,$\r$\n'
  FileWrite $0 '  "OnTypeChange": "RespawnAll",$\r$\n'
  ${If} $Startup == "1"
    FileWrite $0 '  "StartAtLogin": true$\r$\n'
  ${Else}
    FileWrite $0 '  "StartAtLogin": false$\r$\n'
  ${EndIf}
  FileWrite $0 '}$\r$\n'
  FileClose $0

  ; --- Shortcuts. Start Menu always; desktop opt-in, because Screen Bugs lives in the tray
  ;     and is rarely relaunched by hand.
  CreateShortcut "$SMPROGRAMS\Screen Bugs.lnk" "$INSTDIR\ScreenBugs.exe"
  ${If} $DesktopShortcut == "1"
    CreateShortcut "$DESKTOP\Screen Bugs.lnk" "$INSTDIR\ScreenBugs.exe"
  ${EndIf}

  ; --- Uninstaller and Add/Remove Programs. InstallLocation is also what the
  ;     MULTIUSER_INSTALLMODE_*_REGISTRY_* defines read back on the next upgrade.
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayName" "Screen Bugs"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "Publisher" "Addam Boord"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\ScreenBugs.exe,0"
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr SHCTX "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "EstimatedSize" "$0"
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD SHCTX "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd
```

- [ ] **Step 2: Compile**

Same command as Task 7 Step 3.

Expected: exit 0. If `File /r` reports "no files found", the scratch payload directory from the top of this chunk is missing.

- [ ] **Step 3: Install silently and check the seed**

Through PowerShell, per the header: from bash `/S` arrives as `S:/` and the installer would
open its GUI instead.

```bash
pwsh -NoProfile -c "Start-Process -FilePath \"\$env:TEMP\ScreenBugs-Setup-dev.exe\" -ArgumentList '/S','/CURRENTUSER','/BUGTYPE=HouseSpider','/BUGCOUNT=12','/STARTUP=0','/DESKTOP=1','/D=C:\Temp\sb-dev' -Wait"
cat "/c/Temp/sb-dev/install-defaults.json"
```

Expected exactly:

```json
{
  "TypeSlots": [ { "Type": "HouseSpider", "Speed": 1 } ],
  "BugCount": 12,
  "FrameRate": 60,
  "OnTypeChange": "RespawnAll",
  "StartAtLogin": false
}
```

Then confirm the switch validation and the registry:

```bash
pwsh -NoProfile -c "Start-Process -FilePath \"\$env:TEMP\ScreenBugs-Setup-dev.exe\" -ArgumentList '/S','/CURRENTUSER','/BUGTYPE=Wasp','/BUGCOUNT=999','/D=C:\Temp\sb-dev2' -Wait"
grep -E "Type|BugCount|StartAtLogin" "/c/Temp/sb-dev2/install-defaults.json"
pwsh -NoProfile -c "Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs' | Format-List DisplayName,DisplayVersion,Publisher,InstallLocation,QuietUninstallString,EstimatedSize"
```

Expected: `BlackGardenAnt` (unknown type fell back), `"BugCount": 50` (clamped),
`"StartAtLogin": true` (the default), and every Add/Remove Programs value populated with
`DisplayVersion` `1.0.0`.

Check `InstallLocation` reads back as `C:\Temp\sb-dev2` specifically. If it names
`%LocalAppData%\Programs\ScreenBugs` instead, `/D=` was ignored — Task 7's `$R9` save and restore
around `${MULTIUSER_INIT}` is missing, and every later step that installs to a temporary
directory, `verify-install.ps1` included, would silently target the production location.

- [ ] **Step 4: Clean up the dev installs**

```bash
rm -rf "/c/Temp/sb-dev" "/c/Temp/sb-dev2"
pwsh -NoProfile -c "Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs' -Recurse -Force -ErrorAction SilentlyContinue"
rm -f "$USERPROFILE/Desktop/Screen Bugs.lnk" "$APPDATA/Microsoft/Windows/Start Menu/Programs/Screen Bugs.lnk"
```

- [ ] **Step 5: Commit**

```bash
git add installer/ScreenBugs.nsi
git commit -m "$(cat <<'EOF'
feat(installer): install files, write the seed, register uninstall

The relocation path passes /UPGRADE=1 to the old uninstaller so it
keeps the HKCU Run value. Without it a returning user's startup choice
vanishes silently: they have a settings.json, so FirstRunSeed leaves
the Run key alone by design and nothing puts the value back.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: The uninstall section

**Files:**
- Modify: `installer/ScreenBugs.nsi` — replace the placeholder `Section "Uninstall"`
- Modify: `installer/options-page.nsh` — add the uninstaller's option page

Spec 5.8. Order matters: the app is closed *before* its data is deleted, or a live app re-creates `error.log` after the delete.

`RMDir /r "$INSTDIR"` is safe for the uninstaller's own file because NSIS relocates a normally-launched uninstaller to `$TEMP` before running it. It is guarded on `ScreenBugs.exe` being present so a bad `$INSTDIR` cannot delete something unrelated.

- [ ] **Step 1: Add the uninstaller's page**

Append to `installer/options-page.nsh`:

```nsi
Var un.Dialog
Var un.DeleteDataBox

Function un.OptionsPage
  !insertmacro MUI_HEADER_TEXT "Remove Screen Bugs" "Choose whether to keep your settings."

  nsDialogs::Create 1018
  Pop $un.Dialog
  ${If} $un.Dialog == error
    Abort
  ${EndIf}

  ${NSD_CreateCheckBox} 0 0 100% 12u "Also delete my Screen Bugs settings"
  Pop $un.DeleteDataBox
  ${NSD_CreateLabel} 0 18u 100% 32u "Leave this unticked to keep your options and crash log in case you reinstall. Ticking it removes $LocalData."
  Pop $R0

  nsDialogs::Show
FunctionEnd

Function un.OptionsPageLeave
  ${NSD_GetState} $un.DeleteDataBox $DeleteData
FunctionEnd
```

- [ ] **Step 2: Insert the page**

In `installer/ScreenBugs.nsi`, replace the placeholder comment left in Task 7 with the page
itself, so the uninstaller runs Confirm, then this, then the progress page:

```nsi
!insertmacro MUI_UNPAGE_CONFIRM
UninstPage custom un.OptionsPage un.OptionsPageLeave
!insertmacro MUI_UNPAGE_INSTFILES
```

- [ ] **Step 3: Replace the uninstall section**

In `installer/ScreenBugs.nsi`, replace the placeholder `Section "Uninstall" ... SectionEnd` with:

```nsi
Section "Uninstall"
  ; --- Close a running instance first, so it cannot re-create error.log after the delete.
  System::Call 'kernel32::OpenMutex(i 0x00100000, i 0, t "${MUTEX_NAME}") p .r0'
  ${If} $0 <> 0
    System::Call 'kernel32::CloseHandle(p r0)'
    nsExec::ExecToStack 'taskkill /F /IM ScreenBugs.exe'
    Pop $0
    Pop $1
    Sleep 500
  ${EndIf}

  ; --- Optional data removal. $LocalData was captured in un.onInit with the current-user
  ;     context, because under SetShellVarContext all, $LOCALAPPDATA is C:\ProgramData.
  ${If} $DeleteData == "1"
    RMDir /r "$LocalData"
  ${EndIf}

  ; --- The Run value. The app may have created it, and leaving it behind would make Windows
  ;     try to launch a deleted executable at every sign-in. Kept only under /UPGRADE=1,
  ;     where the installer that invoked this is about to install a copy the app will
  ;     re-point the value at.
  ${If} $Upgrade != "1"
    DeleteRegValue HKCU "${RUN_KEY}" "ScreenBugs"
  ${EndIf}

  Delete "$SMPROGRAMS\Screen Bugs.lnk"
  Delete "$DESKTOP\Screen Bugs.lnk"

  ; Guarded so a bad $INSTDIR cannot delete an unrelated folder. Uninstall.exe goes with it:
  ; NSIS relocates a normally-launched uninstaller to $TEMP, so the original is not in use.
  ${If} ${FileExists} "$INSTDIR\ScreenBugs.exe"
    RMDir /r "$INSTDIR"
  ${Else}
    DetailPrint "Skipping $INSTDIR: it does not look like a Screen Bugs installation."
  ${EndIf}

  DeleteRegKey SHCTX "${UNINSTALL_KEY}"
SectionEnd
```

- [ ] **Step 4: Compile**

Same command as the chunk preamble. Expected: exit 0, and this time with no unused-variable
warnings, since `$Upgrade`, `$DeleteData` and `$LocalData` are all read now.

- [ ] **Step 5: Round-trip a silent install and uninstall**

```bash
pwsh -NoProfile -c "Start-Process -FilePath \"\$env:TEMP\ScreenBugs-Setup-dev.exe\" -ArgumentList '/S','/CURRENTUSER','/DESKTOP=1','/D=C:\Temp\sb-dev' -Wait"
ls "/c/Temp/sb-dev/" && ls "$USERPROFILE/Desktop/Screen Bugs.lnk"

# _?= is required: without it the launched process relocates itself to $TEMP and returns
# immediately, so these assertions would race an uninstall still in progress.
pwsh -NoProfile -c "Start-Process -FilePath 'C:\Temp\sb-dev\Uninstall.exe' -ArgumentList '/S','_?=C:\Temp\sb-dev' -Wait"

# _?= also stops the uninstaller deleting its own file, so only Uninstall.exe should remain.
ls "/c/Temp/sb-dev/"
pwsh -NoProfile -c "Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs'"
ls "$USERPROFILE/Desktop/Screen Bugs.lnk" 2>&1
rm -rf "/c/Temp/sb-dev"
```

Expected: after the uninstall, `C:\Temp\sb-dev` holds `Uninstall.exe` and nothing else, `Test-Path` prints `False`, and the desktop shortcut is gone.

- [ ] **Step 6: Check the uninstaller's page and the data checkbox**

```bash
pwsh -NoProfile -c "Start-Process -FilePath \"\$env:TEMP\ScreenBugs-Setup-dev.exe\" -ArgumentList '/S','/CURRENTUSER','/D=C:\Temp\sb-dev' -Wait"
mkdir -p "$LOCALAPPDATA/ScreenBugs" && echo "probe" > "$LOCALAPPDATA/ScreenBugs/probe.txt"
pwsh -NoProfile -c "Start-Process -FilePath 'C:\Temp\sb-dev\Uninstall.exe' -Wait"
```

Tick "Also delete my Screen Bugs settings" and finish. Confirm `%LocalAppData%\ScreenBugs` is gone — including your own `settings.json`, so back it up first if you care about it. Then repeat without ticking and confirm the folder survives.

- [ ] **Step 7: Commit**

```bash
git add installer/
git commit -m "$(cat <<'EOF'
feat(installer): uninstall, with opt-in settings removal

The app is closed before its data is deleted, or a live app re-creates
error.log immediately after. The install directory removal is guarded
on ScreenBugs.exe being present so a bad $INSTDIR cannot delete an
unrelated folder.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 10: The build script

**Files:**
- Create: `build/build-installer.ps1`
- Modify: `.gitignore`

Spec 6. `PublishTrimmed` is deliberately absent: trimming is unsupported for WPF and fails the publish with `NETSDK1175`. ReadyToRun is on because the app is registered to launch at sign-in, where cold-start time is what the user notices.

- [ ] **Step 1: Write the script**

Create `build/build-installer.ps1`:

```powershell
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
```

- [ ] **Step 2: Ignore the build output**

Append to `.gitignore` (`build/publish/` is already covered by the existing `publish/` rule):

```gitignore

# Installer output
/build/*.exe
```

- [ ] **Step 3: Run it**

This one is slow — a full Release publish plus a solid-LZMA compress of ~155 MB. Expect several minutes.

```bash
pwsh -NoProfile -File build/build-installer.ps1
```

Expected: `Built .../build/ScreenBugs-Setup-1.0.0.exe`, roughly 60–75 MB, from ~254 published files.

- [ ] **Step 4: Install the real thing**

```bash
"C:\Users\AddamBoord\source\repos\ScreenSavers\build\ScreenBugs-Setup-1.0.0.exe"
```

Take the current-user option into the default directory, choose House spider and 3 bugs, leave startup checked, finish with "Run Screen Bugs" ticked. Three house spiders must appear — a real self-contained app, launched from a real install, seeded by the wizard. Leave it installed for Task 11.

- [ ] **Step 5: Commit**

```bash
git add build/build-installer.ps1 .gitignore
git commit -m "$(cat <<'EOF'
build(installer): one command from a clean tree to setup.exe

PublishTrimmed is deliberately absent: trimming is unsupported for WPF
and fails the publish with NETSDK1175. ReadyToRun is on because the
app is registered to launch at sign-in, where cold start is what the
user notices.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

## Chunk 4: Verification

### Task 11: Automated install verification

**Files:**
- Create: `build/verify-install.ps1`

Spec 7.2. This drives the real setup executable silently and asserts, so the option plumbing is checked end to end without GUI automation.

Two safety rules it must honour. It installs **per-user into a temporary directory**, so it never writes to Program Files. And it uses the production per-user uninstall key, so it **refuses to run** when one already exists — otherwise it would overwrite and then delete a real installation's Add/Remove Programs entry.

- [ ] **Step 1: Uninstall the copy from Task 10**

The script refuses to run while a per-user install exists, which is the point. Remove it through Add/Remove Programs, or:

The stored `UninstallString` includes its own quotes, so `&` would treat the whole quoted
string as a command name and fail. Trim them:

```bash
pwsh -NoProfile -c "
\$path = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenBugs').UninstallString.Trim('\"')
Start-Process -FilePath \$path -Wait"
```

- [ ] **Step 2: Write the script**

Create `build/verify-install.ps1`:

```powershell
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

Write-Host ""
if ($failures -eq 0) {
    Write-Host 'All checks passed.' -ForegroundColor Green
    exit 0
}

Write-Host "$failures check(s) failed." -ForegroundColor Red
exit 1
```

- [ ] **Step 3: Run it**

```bash
pwsh -NoProfile -File build/verify-install.ps1
echo "exit: $?"
```

Expected: `All checks passed.` and exit 0. Every `FAIL` line names the claim that broke — fix the installer, rebuild with `build/build-installer.ps1 -SkipTests`, and re-run.

- [ ] **Step 4: Commit**

```bash
git add build/verify-install.ps1
git commit -m "$(cat <<'EOF'
test(installer): assert the option plumbing end to end

Drives the real setup silently in three cases and checks the written
seed, the Add/Remove Programs values and the shortcuts, then uninstalls
and checks the cleanup.

Uninstall is run with _?= so the assertions cannot race: without it the
launched uninstaller relocates itself to $TEMP and returns immediately.
The script refuses to run when a per-user install already exists, since
it writes the same registry key.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 12: Manual checklist

**Files:** none — this is spec 7.3, the part no script covers.

Work through it in order and note anything that differs. Items 5, 7 and 11 are the ones that catch the bugs found in spec review, so do not skip them.

- [ ] **Step 1: Wizard presentation**

Run `build/ScreenBugs-Setup-1.0.0.exe`. Confirm the page order (welcome, install mode, directory, options, install, finish), the ant on the welcome and finish panels and in the inner headers, and that the options page opens with Black garden ant, 5, startup checked, desktop unchecked. Type 99 into the spinner and confirm the install writes 50.

- [ ] **Step 2: Elevation**

An all-users install raises exactly one UAC prompt; a current-user install raises none.

- [ ] **Step 3: A fresh profile, startup on**

With no `settings.json` (move yours aside), install with startup checked. The app starts with the chosen type and count; the Options dialog shows them; its "Run at Windows startup" box is checked; and the `Run` value holds the quoted install path:

```bash
pwsh -NoProfile -c "(Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').ScreenBugs"
```

- [ ] **Step 4: A fresh profile, startup off, with a stale `Run` value**

This is the case `FirstRunSeed`'s three-state startup exists for. Plant a stale value, remove `settings.json`, then install with startup **unchecked**:

```bash
pwsh -NoProfile -c "Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name ScreenBugs -Value '\"C:\Nowhere\ScreenBugs.exe\"'"
rm -f "$LOCALAPPDATA/ScreenBugs/settings.json"
```

After the first launch the value must be **gone** and the Options dialog's box unchecked. If the value survives, `SettingsBootstrap` is treating the seed's startup choice as a flag rather than a state.

- [ ] **Step 5: An existing user is left alone**

With a `settings.json` in place, install again with different options. Neither the settings nor the `Run` value change, and the Options dialog shows exactly what it showed before.

- [ ] **Step 6: Installing over a running instance**

With the app running, run setup. It asks, the tray icon disappears, the install completes, and the finish checkbox brings it back. For an all-users install, add the Elevated column in Task Manager's Details tab and confirm the relaunched `ScreenBugs.exe` is **not** elevated.

- [ ] **Step 7: A relocating upgrade keeps the startup choice**

The case `/UPGRADE=1` and `StartupRegistration.Refresh` exist for. The precondition matters: the
user must have **both** a `settings.json` and a `Run` value, so delete the settings file first and
let the install's startup choice create the value. Skip that and step 4 has already left a
`settings.json` behind, `FirstRunSeed` takes rule 1, no `Run` value is ever created, and the final
assertion passes whether or not `/UPGRADE=1` works at all.

```bash
rm -f "$LOCALAPPDATA/ScreenBugs/settings.json"
```

Then: install **current-user** with startup checked, launch once (which seeds `settings.json` and
writes the `Run` value), and exit. Confirm the value exists and names the current-user path:

```bash
pwsh -NoProfile -c "(Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run').ScreenBugs"
```

Now install **all-users**. Afterwards: only one Add/Remove Programs entry, the old directory
gone, and after launching the new copy the Options dialog still shows startup checked, with the
`Run` value naming the **new** Program Files path — re-run the command above to confirm.

- [ ] **Step 8: Uninstall, both ways**

Unticked: `%LocalAppData%\ScreenBugs` survives. Ticked: it is gone. Either way the `Run` value and both shortcuts are gone.

- [ ] **Step 9: Icons and Add/Remove Programs**

The shortcut, taskbar and Add/Remove Programs entries show the ant. The ARP entry shows name, version and publisher, and its Uninstall button works.

- [ ] **Step 10: Platform guards**

Setup refuses, with a clear message, on 32-bit Windows or a build below 14393. Without such a
machine to hand, invert the guards in `.onInit` — but **one at a time**: the first to fire calls
`Abort`, which quits the installer, so inverting both only ever shows the first message. Compile,
confirm the message, revert, then repeat for the other. Do not commit either inversion.

- [ ] **Step 11: Final full-suite run**

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "Error\(s\)" /tmp/b.log
dotnet test tests/ScreenBugs.Tests -nologo -v q --no-build -nodeReuse:false
pwsh -NoProfile -File build/verify-install.ps1
```

Expected: `Passed: 115`, and `All checks passed.`

- [ ] **Step 12: Note anything outstanding**

Record any manual item that did not behave as described, with what you saw, before handing the branch over. Then use superpowers:finishing-a-development-branch.
