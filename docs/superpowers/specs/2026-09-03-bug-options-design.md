# Bug Options: design spec

Date: 2026-09-03
Status: approved for planning
Builds on: `2026-09-02-screen-bugs-design.md` (the v1 spec). Where the two
disagree, this spec wins; it replaces section 8 (tray) and amends sections 5.5
(species choice at spawn), 7.4 (frame rate) and 9 (composition).

## 1. Overview

Screen Bugs v1 spawns a random species from all nine every time and offers only
a fixed 1/3/5/10 bug-count submenu on the tray. This feature adds an Options
dialog, opened from the tray, that controls:

- which bug types appear, as a list of one to ten "type slots" each holding a
  species or Random;
- how many bugs are on screen;
- the overlay frame rate;
- whether the app runs at Windows startup;
- what happens to bugs already on screen when the type slots change.

Settings persist between launches. Changes preview live on the overlay while
the dialog is open; Cancel reverts them, OK keeps them.

Defaults: one slot set to Black garden ant, 5 bugs, 60 fps, startup off,
respawn all bugs on a type change. This replaces v1's "3 random bugs" default.

Non-goals: per-species speed or size tuning, importing or exporting settings,
multi-monitor, hot keys.

## 2. Settings model

### 2.1 Type slots

A slot is either a specific species or Random.

```csharp
// ScreenBugs.Core/Simulation/BugTypeSlot.cs
public readonly record struct BugTypeSlot(SpeciesId? Species)
{
    public static readonly BugTypeSlot Random = new(null);
    public bool IsRandom => Species is null;
}
```

Rules:

- There are ten distinct choices: Random plus the nine species, in the order
  Random first, then `SpeciesCatalog.All` order.
- No choice may appear in more than one slot. Random counts as a choice, so at
  most one slot is Random. Therefore the slot count is 1 to 10.
- When a bug spawns, one slot is chosen uniformly at random. If it holds a
  species, that species spawns. If it is Random, any of the nine species
  spawns with equal probability, including species held by other slots.

### 2.2 Options record

```csharp
// ScreenBugs.Core/Settings/BugOptions.cs
public sealed record BugOptions(
    IReadOnlyList<BugTypeSlot> TypeSlots,   // 1 to 10, no duplicates
    int BugCount,                            // 1 to 50
    int FrameRate,                           // 30, 60 or 120
    TypeChangeBehavior OnTypeChange)
{
    public static BugOptions Default => new(
        [new BugTypeSlot(SpeciesId.BlackGardenAnt)], BugCount: 5, FrameRate: 60,
        TypeChangeBehavior.RespawnAll);
}

// ScreenBugs.Core/Settings/TypeChangeBehavior.cs
public enum TypeChangeBehavior { RespawnAll, AgeOut }
```

Equality: a record compares an `IReadOnlyList` member by reference, so
`BugOptions` overrides `Equals(BugOptions?)` and `GetHashCode` to compare
`TypeSlots` element by element (`SequenceEqual`) and the other members by value.
`Default` returning a fresh list each call must therefore still satisfy
`Default.Equals(Default)`. Everything that asks "did the slots change" (the
applier in section 4, the tests in section 11) relies on this.

"Run at Windows startup" is deliberately not part of `BugOptions`: the Windows
Run registry key is its single source of truth (section 6), so the settings file
never disagrees with what Windows will actually do.

### 2.3 Slot helpers

Pure functions in `ScreenBugs.Core/Simulation/BugTypeSlots.cs` so the dialog
stays thin and the rules are unit-tested:

- `const int MaxSlots = 10`.
- `IReadOnlyList<BugTypeSlot> AllChoices`: Random, then the nine species in
  catalog order.
- `AvailableFor(slots, index)`: every choice not held by a *different* slot. The
  slot's own current value is always included so the dropdown can show it.
- `Resize(slots, count)`: clamps `count` to `[1, MaxSlots]`. Shrinking keeps the
  first `count` slots. Growing appends slots one at a time, each taking the first
  choice in `AllChoices` order that no slot holds at that moment, including
  slots appended earlier in the same call. There are always enough choices
  because there are ten and at most ten slots.
- `Sanitize(slots)`: removes duplicates keeping the first occurrence and returns
  `[BugTypeSlot.Random]` if the result is empty. Because only ten distinct
  choices exist, a deduplicated list can never exceed `MaxSlots`. Used when
  loading from disk and by `SlotSpeciesSource`.

## 3. Species selection in the simulation

`BugSimulation` currently picks a species inline in `SpawnFromEdge`. That choice
moves behind an interface so the app can drive it from the slots.

```csharp
// ScreenBugs.Core/Simulation/ISpeciesSource.cs
public interface ISpeciesSource
{
    BugSpecies Next();
}

// ScreenBugs.Core/Simulation/SlotSpeciesSource.cs
public sealed class SlotSpeciesSource(IRandomSource rng) : ISpeciesSource
{
    // Setter runs BugTypeSlots.Sanitize, so the list is never empty and never has duplicates.
    public IReadOnlyList<BugTypeSlot> Slots { get; set; } = [BugTypeSlot.Random];
    public BugSpecies Next();   // implements 2.1
}
```

`Next()` draws from `rng` in a fixed order so tests can script it:

1. If `Slots.Count > 1`, `rng.NextInt(Slots.Count)` picks the slot. With a single
   slot no draw is made, which keeps seeded v1 runs (one Random slot) bit-for-bit
   identical to before.
2. If the chosen slot is Random, `rng.NextInt(SpeciesCatalog.All.Count)` picks
   the species by catalog index. Otherwise the slot's species is returned with
   no further draw.

`BugSimulation` changes:

- Constructor becomes `BugSimulation(Bounds bounds, IRandomSource rng,
  ISpeciesSource species)`. `SpawnFromEdge` calls `species.Next()` instead of
  indexing the catalog. Everything else about spawning (edge, position, heading)
  is unchanged.
- New method `RespawnAll()`: removes every alive bug, cancels any pending respawn
  timer, then spawns `TargetCount` bugs from the edges. Squashed bugs are left to
  finish fading.

Implication for v1 tests: `SimulationSteps.Create` passes a `SlotSpeciesSource`
left at its default single Random slot, which reproduces v1's uniform choice.
Tests never depended on which species spawned, so they need no other changes.

## 4. Applying options to the running app

`ScreenBugs/Settings/OptionsApplier.cs` (primary constructor taking the
`BugSimulation`, the `SlotSpeciesSource` and the `FrameLoop`) has one method:

```csharp
/// Returns true if RespawnAll was invoked.
bool Apply(BugOptions previous, BugOptions next, TypeChangeBehavior onSlotChange)
```

It diffs `previous` and `next` (slots compared with `SequenceEqual`) and touches
only what changed, in this order:

| Changed | Effect |
|---|---|
| `TypeSlots` | `source.Slots = next.TypeSlots`; then if `onSlotChange` is `RespawnAll`, `simulation.RespawnAll()`. With `AgeOut`, existing bugs stay and only future spawns use the new slots. |
| `BugCount` | `simulation.TargetCount = next.BugCount` (v1 semantics: spawn up or trim down immediately). Applied after slots so newly spawned bugs use the new slots. |
| `FrameRate` | `frameLoop.TargetFrameRate = next.FrameRate`. |
| `OnTypeChange` | Nothing immediate; it only governs future slot changes. |

The behavior for a slot change is passed in rather than read from `next`, so the
dialog decides the policy: live preview passes `edited.OnTypeChange`, and Cancel
passes the value described in 8.3. The applier itself stays a dumb diff. Startup
does not use it; `App` constructs the simulation directly from the loaded
options (section 10).

## 5. Persistence

Serialization is pure and lives in Core so it is unit-tested; file access lives
in the app.

```csharp
// ScreenBugs.Core/Settings/SettingsSerializer.cs
public static class SettingsSerializer
{
    public static string Serialize(BugOptions options);      // indented JSON
    public static BugOptions Deserialize(string json);        // never throws
}
```

The JSON shape is an object with four properties: `TypeSlots` (array of strings,
each `"Random"` or a `SpeciesId` name), `BugCount` (integer), `FrameRate`
(integer), `OnTypeChange` (a `TypeChangeBehavior` name). There is no DTO class:
`Deserialize` reads the document field by field with `System.Text.Json.Nodes`
so one bad field cannot discard the good ones.

`Deserialize` is total: any input yields a valid `BugOptions`, and it never
throws. `JsonNode.Parse` is wrapped in a try/catch; a parse failure, a `null`
result, or a root that is not an object yields `BugOptions.Default`. Otherwise
each field is read independently and falls back on its own:

- `TypeSlots`: must be an array; each element must be a string that
  `Enum.TryParse<SpeciesId>(name, ignoreCase: true)` accepts **and**
  `Enum.IsDefined` confirms (so `"99"` is rejected even though `TryParse`
  accepts it), or the string `"Random"` case-insensitively. Anything else,
  including `null` elements, is dropped. The survivors go through
  `BugTypeSlots.Sanitize`; if the field is missing, not an array, or nothing
  survives, `Default.TypeSlots` is used.
- `BugCount`: must be a JSON number convertible to `int`; clamped into
  `[1, 50]`. Missing or wrong kind uses 5.
- `FrameRate`: must be a JSON number equal to 30, 60 or 120; anything else,
  including missing, becomes 60.
- `OnTypeChange`: parsed like the species names (`TryParse` with `ignoreCase`
  plus `IsDefined`); unknown or missing becomes `RespawnAll`.
- Unknown properties are ignored, so a file written by a future version loads.

Example file:

```json
{
  "TypeSlots": [ "BlackGardenAnt", "Random", "PrayingMantis" ],
  "BugCount": 5,
  "FrameRate": 60,
  "OnTypeChange": "RespawnAll"
}
```

`ScreenBugs/Settings/SettingsStore.cs` (static):

- `string FilePath`: `%LocalAppData%\ScreenBugs\settings.json`, beside the v1
  crash log.
- `BugOptions Load()`: returns `Default` if the file is missing or unreadable,
  otherwise `Deserialize(File.ReadAllText(...))`. Never throws.
- `void Save(BugOptions)`: creates the directory, writes to a temporary file in
  the same directory, then `File.Move` over the target with overwrite, so a
  crash mid-write cannot leave a truncated file. A failure to save is written to
  `CrashLog` and otherwise ignored; the running options stay applied.

## 6. Run at Windows startup

`ScreenBugs/Settings/StartupRegistration.cs` (static) owns the value
`ScreenBugs` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`:

- `bool IsEnabled()`: true when the value exists, whatever path it holds. A
  stale value from an older install location therefore shows as enabled, which
  is what Windows will attempt to do at login.
- `void SetEnabled(bool)`: `true` always writes the current executable path
  (`Environment.ProcessPath`) in quotes, even if a value already exists, so a
  stale path is repaired; `false` deletes the value. Both swallow and
  `CrashLog.Write` any exception, since a locked-down registry must not crash
  the app.

The dialog's checkbox is initialized from `IsEnabled()` and applied only on OK,
not live: it has no visible effect on the overlay and should not churn the
registry while the user experiments. On OK, a checked box calls
`SetEnabled(true)` unconditionally (cheap, and it repairs a stale path); an
unchecked box calls `SetEnabled(false)` only if `IsEnabled()` is currently true.

## 7. Frame rate

`FrameLoop` replaces its `Interval` constant with
`int TargetFrameRate { get; set; }` (default 60). The interval is
`1.0 / TargetFrameRate`. Setting it resets the accumulator so the next tick is
not distorted. The accumulator-with-carry behavior from v1 spec 7.4 is
unchanged. At 120 on a 60 Hz monitor the loop simply ticks once per
`CompositionTarget.Rendering`, since WPF renders no faster than the display.

## 8. Options dialog

`ScreenBugs/Options/OptionsWindow.xaml` is an ordinary WPF window: standard
chrome, `ShowInTaskbar=True`, `ResizeMode=NoResize`, `SizeToContent=Height`,
fixed width, `WindowStartupLocation=CenterScreen`, title "Screen Bugs options".
It is not topmost, so bugs crawl over it, which is the point of a live preview.

While the dialog is open the overlay stays fully click-through, using the same
mechanism v1 uses while the tray menu is open (`IsMenuOpen`), so a bug sitting on
a control can never steal the click. Squashing resumes when the dialog closes.
This flag is needed even though `ShowDialog` disables the overlay window: a
disabled window that lacks `WS_EX_TRANSPARENT` swallows clicks rather than
passing them through to the dialog underneath.

### 8.1 Layout, top to bottom

1. **Bug types**
   - "Number of types": ComboBox with 1 to 10.
   - One row per slot: label "Type N" and a ComboBox whose items are
     `BugTypeSlots.AvailableFor(currentSlots, n)`, displayed with the names in
     8.2. Rows are added or removed as the number changes, using
     `BugTypeSlots.Resize`.
2. **Bug count**: Slider 1 to 50, integer snapping, with the current value shown
   beside it.
3. **Frame rate**: ComboBox with 30, 60, 120 (labelled "30 fps" etc.).
4. **When types change**: ComboBox with "Respawn all bugs" and "Let existing bugs
   age out".
5. **Run at Windows startup**: CheckBox.
6. **OK** and **Cancel** buttons, right-aligned. OK is the default button, Cancel
   is the cancel button (Escape). Closing with the title-bar X is Cancel.

### 8.2 Display names

`ScreenBugs/Options/BugTypeChoice.cs` is `record BugTypeChoice(BugTypeSlot Slot,
string Label)`, the ComboBox item type. Labels: Random, Hissing cockroach, Black
garden ant, Red fire ant, Praying mantis, Seven-spot ladybug, Stag beetle, House
spider, Centipede, Stink bug.

### 8.3 Behavior

- Constructor `OptionsWindow(BugOptions initial, OptionsApplier applier)`.
  `initial` is the snapshot used by Cancel. The window keeps an `edited` copy and
  a `previewRespawned` flag, initially false.
- Any change to a slot, the slot count, the bug count, the frame rate or the
  type-change behavior produces a new `edited` record and calls
  `applier.Apply(previousEdited, edited, edited.OnTypeChange)`, so the overlay
  reflects it at once. If that call returns true, `previewRespawned` is set.
- After any slot or slot-count change, every slot ComboBox's items are rebuilt
  from `AvailableFor` and its selection restored. A reentrancy guard suppresses
  the selection-changed handlers during the rebuild.
- OK: sets `Result = edited`, applies the startup checkbox as described in
  section 6, and closes. The caller saves `Result`.
- Cancel or X: restores the snapshot with
  `applier.Apply(edited, initial, revertBehavior)` where `revertBehavior` is
  `RespawnAll` if `previewRespawned` is true or `initial.OnTypeChange` is
  `RespawnAll`, otherwise `AgeOut`. That way a population replaced during
  preview is replaced again with the original slots, and a preview that never
  touched the population (age-out throughout) reverts without churning the
  screen. `Result` stays null, startup is untouched, the window closes.
- Opening Options while the dialog is already open activates the existing window
  instead of creating another.
- Pause and Resume remain reachable from the tray while the dialog is open,
  because `ShowDialog` disables only WPF windows. Both work normally: while
  paused, the frame loop is stopped and the overlay hidden, but `Apply` still
  mutates the simulation, and the changes are visible on Resume. Options may
  also be opened while paused; the live preview is simply not visible until
  Resume.
- Exit from the tray while the dialog is open ends the app without saving: only
  OK persists edits. `Shutdown()` makes `ShowDialog` return null, so the
  caller's post-dialog code sees no `Result` and saves nothing.

## 9. Tray

The v1 menu becomes: **Pause** (or **Resume**), **Options...**, separator,
**Exit**. The Bugs count submenu, `CountChoices`, `BugCountChanged`,
`SelectCount` and the `countItems` field are removed, and the constructor loses
its `initialCount` parameter, becoming `TrayIcon()`. A new event
`OptionsRequested` is raised by the Options item. `RouteThreadExceptions`,
`IsMenuOpen`, `SetPaused` and `Dispose` are unchanged.

## 10. Application composition

`App.OnStartup` changes to:

1. `options = SettingsStore.Load()`.
2. `speciesSource = new SlotSpeciesSource(rng) { Slots = options.TypeSlots }`.
3. `simulation = new BugSimulation(bounds, rng, speciesSource) { TargetCount = options.BugCount }`.
4. `frameLoop.TargetFrameRate = options.FrameRate` before `Start()`.
5. `applier = new OptionsApplier(simulation, speciesSource, frameLoop)`.
6. `trayIcon = new TrayIcon()` (no count parameter any more);
   `trayIcon.OptionsRequested += ShowOptions`.

`ShowOptions`: if a dialog is open, `Activate()` it; otherwise construct
`OptionsWindow(current, applier)`, set the overlay's suppress-squash flag while
it is open, `ShowDialog()`, and on a non-null `Result` set `current = Result`
and `SettingsStore.Save(current)`. A null `Result` (Cancel, X, or Exit during
the dialog) saves nothing.

The click-through decision in the frame tick becomes
`!trayIcon.IsMenuOpen && !optionsOpen && cursor over a bug`.

Both `ScreenBugs.csproj` and `ScreenBugs.Tests.csproj` add
`<Using Include="ScreenBugs.Core.Settings" />` beside the existing
`ScreenBugs.Core.Simulation` global using.

## 11. Testing

Unit tests in `ScreenBugs.Tests` (Core only, seeded random):

- `ScriptedRandomSource` (test helper): an `IRandomSource` that returns a queued
  sequence of integers from `NextInt` (each clamped below `maxExclusive`) and
  throws if the queue runs dry, so a test can force exact draws.
- `SlotSpeciesSourceTests`
  - A single species slot always yields that species (200 seeded draws).
  - A single Random slot yields every one of the nine species within 2000
    seeded draws.
  - Two species slots yield only those two species, both appearing.
  - Random may repeat a selected species: with slots [BlackGardenAnt, Random]
    and a `ScriptedRandomSource` queued `[1, 1]`, `Next()` returns
    BlackGardenAnt (slot index 1 is Random; catalog index 1 is BlackGardenAnt).
  - With a single slot, `Next()` makes no slot draw: a `ScriptedRandomSource`
    queued with nothing does not throw for a species slot, and queued `[3]`
    returns catalog index 3 for a Random slot.
  - Setting `Slots` to an empty list leaves it as `[Random]`; setting it to
    [Ant, Ant] leaves `[Ant]`.
- `BugTypeSlotsTests`
  - `AllChoices` has 10 entries, Random first, then catalog order.
  - `AvailableFor` excludes choices held by other slots and includes the slot's
    own choice; with Random held elsewhere, Random is excluded.
  - `Resize` growing from [BlackGardenAnt] to 3 gives [BlackGardenAnt, Random,
    HissingCockroach]; shrinking keeps the prefix; count is clamped to 1 and 10.
  - `Sanitize` removes a duplicate and turns empty into [Random].
- `BugSimulation.RespawnAll` (in `BugSpawnTests`)
  - Arrange: create with 3, squash one, step once so `RespawnTimer` is running.
    After `RespawnAll`: alive count equals `TargetCount`, every alive bug has an
    Id higher than any before, the squashed bug is still present and fading,
    and `RespawnTimer` is null.
  - All respawned bugs are outside the bounds heading inward.
- `BugOptionsTests`
  - `Default.Equals(Default)` is true; two records with equal slot contents but
    distinct list instances are equal; changing one slot makes them unequal.
- `SettingsSerializerTests`
  - Round trip of a non-default `BugOptions` (3 slots, count 7, 120 fps,
    AgeOut) returns an equal record.
  - `Deserialize("")`, `Deserialize("not json")`, `Deserialize("null")`,
    `Deserialize("[1,2]")` and `Deserialize("{}")` all equal `Default`.
  - A wrong-typed field does not discard the others: `{"TypeSlots":["Centipede"],
    "BugCount":"5"}` yields slots [Centipede] and count 5.
  - `"99"`, `"Unicorn"` and `null` slot entries are dropped; `"random"` and
    `"blackgardenant"` are accepted case-insensitively; an all-unknown list
    falls back to default slots.
  - `BugCount` 0 becomes 1, 999 becomes 50; `FrameRate` 45 becomes 60;
    `OnTypeChange` "Sideways" becomes `RespawnAll`; an unknown top-level
    property is ignored.
- Existing 37 v1 tests still pass with the new constructor.

Manual checklist (run before calling the work done):

1. First launch with no settings file: 5 black garden ants.
2. Tray menu reads Pause, Options..., Exit; Options opens a centered dialog.
3. Set number of types to 3: two new dropdowns appear preset to Random and
   Hissing cockroach; the black ant is absent from them and Random is absent
   from the third.
4. Change a slot: with "Respawn all" the population is replaced immediately;
   with "Let existing bugs age out" it is not.
5. Drag the count slider: bugs are added or removed live.
6. Pick 30 fps: motion is visibly less smooth; 60 restores it.
7. Cancel after several changes: slots, count and frame rate are back to what
   they were when the dialog opened. If a preview replaced the population, a
   fresh population of the original mix walks in from the edges; if every
   preview used "age out", the bugs on screen are left alone.
8. OK, Exit, relaunch: settings are as saved; `settings.json` matches 5.
9. Check "Run at Windows startup", OK: the `Run` key holds the quoted exe path;
   uncheck, OK: the value is gone.
10. Open Options twice: the same window comes forward.
11. A bug crawling over the dialog does not intercept a click on a control.

## 12. File layout

```
src/ScreenBugs.Core/
  Simulation/BugTypeSlot.cs            new
  Simulation/BugTypeSlots.cs           new
  Simulation/ISpeciesSource.cs         new
  Simulation/SlotSpeciesSource.cs      new
  Simulation/BugSimulation.cs          modified: constructor, SpawnFromEdge, RespawnAll
  Settings/BugOptions.cs               new
  Settings/TypeChangeBehavior.cs       new
  Settings/SettingsSerializer.cs       new
src/ScreenBugs/
  Settings/SettingsStore.cs            new
  Settings/StartupRegistration.cs      new
  Settings/OptionsApplier.cs           new
  Options/OptionsWindow.xaml(.cs)      new
  Options/BugTypeChoice.cs             new
  Overlay/FrameLoop.cs                 modified: TargetFrameRate
  Tray/TrayIcon.cs                     modified: menu, OptionsRequested
  App.xaml.cs                          modified: load, wire, ShowOptions
tests/ScreenBugs.Tests/
  SimulationSteps.cs                   modified: pass SlotSpeciesSource
  ScriptedRandomSource.cs              new: test helper
  SlotSpeciesSourceTests.cs            new
  BugTypeSlotsTests.cs                 new
  BugSpawnTests.cs                     modified: RespawnAll tests
  BugOptionsTests.cs                   new
  SettingsSerializerTests.cs           new
```
