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
  first `count` slots. Growing appends slots, each taking the first choice in
  `AllChoices` order that no existing slot holds.
- `Sanitize(slots)`: removes duplicates keeping the first occurrence, truncates
  to `MaxSlots`, and returns `[BugTypeSlot.Random]` if the result is empty. Used
  when loading from disk.

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
    // Setter sanitizes: an empty list becomes [Random].
    public IReadOnlyList<BugTypeSlot> Slots { get; set; } = [BugTypeSlot.Random];
    public BugSpecies Next();   // implements 2.1
}
```

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
`BugSimulation`, the `SlotSpeciesSource` and the `FrameLoop`) has one method,
`Apply(BugOptions previous, BugOptions next)`, which diffs the two and touches
only what changed:

| Changed | Effect |
|---|---|
| `TypeSlots` | `source.Slots = next.TypeSlots`; then if `next.OnTypeChange` is `RespawnAll`, `simulation.RespawnAll()`. With `AgeOut`, existing bugs stay and only future spawns use the new slots. |
| `BugCount` | `simulation.TargetCount = next.BugCount` (v1 semantics: spawn up or trim down immediately). |
| `FrameRate` | `frameLoop.TargetFrameRate = next.FrameRate`. |
| `OnTypeChange` | Nothing immediate; it only governs future slot changes. |

The same method serves live preview, Cancel (apply the snapshot over the edited
state) and startup (apply defaults-to-loaded is unnecessary; startup constructs
directly from the loaded options).

## 5. Persistence

Serialization is pure and lives in Core so it is unit-tested; file access lives
in the app.

```csharp
// ScreenBugs.Core/Settings/SettingsDocument.cs   (the JSON shape)
public sealed class SettingsDocument
{
    public List<string>? TypeSlots { get; set; }   // "Random" or a SpeciesId name
    public int? BugCount { get; set; }
    public int? FrameRate { get; set; }
    public string? OnTypeChange { get; set; }      // TypeChangeBehavior name
}

// ScreenBugs.Core/Settings/SettingsSerializer.cs
public static class SettingsSerializer
{
    public static string Serialize(BugOptions options);      // indented JSON
    public static BugOptions Deserialize(string json);        // never throws
}
```

`Deserialize` is total: any input yields a valid `BugOptions`.

- Unparseable JSON, or a document with every field missing: `BugOptions.Default`.
- `TypeSlots`: unknown names are dropped, then `BugTypeSlots.Sanitize`; a missing
  or empty result uses `Default.TypeSlots`.
- `BugCount`: missing or out of range is clamped into `[1, 50]`; missing uses 5.
- `FrameRate`: anything other than 30, 60 or 120 becomes 60.
- `OnTypeChange`: unknown or missing becomes `RespawnAll`.

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

- `bool IsEnabled()`: true when the value exists and equals the current
  executable path in quotes (`Environment.ProcessPath`). A value pointing at a
  different path (an older install location) reads as false, and enabling then
  rewrites it to the current path.
- `void SetEnabled(bool)`: writes the quoted path, or deletes the value. Both
  swallow and `CrashLog.Write` any exception, since a locked-down registry must
  not crash the app.

The dialog's checkbox is initialized from `IsEnabled()` and applied only on OK,
not live: it has no visible effect on the overlay and should not churn the
registry while the user experiments.

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
  `initial` is the snapshot used by Cancel. The window keeps an `edited` copy.
- Any change to a slot, the slot count, the bug count, the frame rate or the
  type-change behavior produces a new `edited` record and calls
  `applier.Apply(previousEdited, edited)`, so the overlay reflects it at once.
- After any slot or slot-count change, every slot ComboBox's items are rebuilt
  from `AvailableFor` and its selection restored. A reentrancy guard suppresses
  the selection-changed handlers during the rebuild.
- OK: sets `Result = edited`, applies the startup checkbox through
  `StartupRegistration.SetEnabled` if it differs from the initial state, and
  closes. The caller saves `Result`.
- Cancel or X: `applier.Apply(edited, initial)` so the overlay returns to the
  snapshot, `Result` stays null, startup untouched, closes.
- Opening Options while the dialog is already open activates the existing window
  instead of creating another.

## 9. Tray

The v1 menu becomes: **Pause** (or **Resume**), **Options...**, separator,
**Exit**. The Bugs count submenu, `CountChoices`, `BugCountChanged` and
`SelectCount` are removed. A new event `OptionsRequested` is raised by the
Options item. `RouteThreadExceptions`, `IsMenuOpen`, `SetPaused` and `Dispose`
are unchanged.

## 10. Application composition

`App.OnStartup` changes to:

1. `options = SettingsStore.Load()`.
2. `speciesSource = new SlotSpeciesSource(rng) { Slots = options.TypeSlots }`.
3. `simulation = new BugSimulation(bounds, rng, speciesSource) { TargetCount = options.BugCount }`.
4. `frameLoop.TargetFrameRate = options.FrameRate` before `Start()`.
5. `applier = new OptionsApplier(simulation, speciesSource, frameLoop)`.
6. `trayIcon.OptionsRequested += ShowOptions`.

`ShowOptions`: if a dialog is open, `Activate()` it; otherwise construct
`OptionsWindow(current, applier)`, set the overlay's suppress-squash flag while
it is open, `ShowDialog()`, and on a non-null `Result` set `current = Result`
and `SettingsStore.Save(current)`.

The click-through decision in the frame tick becomes
`!trayIcon.IsMenuOpen && !optionsOpen && cursor over a bug`.

## 11. Testing

Unit tests in `ScreenBugs.Tests` (Core only, seeded random):

- `SlotSpeciesSourceTests`
  - A single species slot always yields that species (200 draws).
  - A single Random slot yields every one of the nine species within 2000 draws.
  - Two species slots yield only those two species, both appearing.
  - Slots [BlackGardenAnt, Random] yield BlackGardenAnt from the Random slot at
    least once in 2000 draws (Random may repeat a selected species).
  - Setting `Slots` to an empty list leaves it as `[Random]`.
- `BugTypeSlotsTests`
  - `AllChoices` has 10 entries, Random first, then catalog order.
  - `AvailableFor` excludes choices held by other slots and includes the slot's
    own choice; with Random held elsewhere, Random is excluded.
  - `Resize` growing from [BlackGardenAnt] to 3 gives [BlackGardenAnt, Random,
    HissingCockroach]; shrinking keeps the prefix; count is clamped to 1 and 10.
  - `Sanitize` removes a duplicate, truncates to 10, and turns empty into
    [Random].
- `BugSimulation.RespawnAll` (in `BugSpawnTests`)
  - After squashing one and calling `RespawnAll`, alive count equals
    `TargetCount`, every alive bug has a new Id, the squashed bug is still
    present and fading, and `RespawnTimer` is null.
  - All respawned bugs are outside the bounds heading inward.
- `SettingsSerializerTests`
  - Round trip of a non-default `BugOptions` is equal (slot by slot).
  - `Deserialize("")`, `Deserialize("not json")` and `Deserialize("{}")` all
    equal `Default`.
  - Unknown slot name dropped; all-unknown list falls back to default slots.
  - `BugCount` 0 becomes 1, 999 becomes 50; `FrameRate` 45 becomes 60;
    `OnTypeChange` "Sideways" becomes `RespawnAll`.
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
7. Cancel after several changes: the overlay returns to how it was when the
   dialog opened.
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
  Settings/SettingsDocument.cs         new
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
  SlotSpeciesSourceTests.cs            new
  BugTypeSlotsTests.cs                 new
  BugSpawnTests.cs                     modified: RespawnAll tests
  SettingsSerializerTests.cs           new
```
