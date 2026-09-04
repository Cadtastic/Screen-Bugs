# Bug Options Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tray-launched Options dialog that controls which bug types appear (as slots, each a species or Random), how many bugs, the frame rate, startup registration, and what happens to existing bugs when types change — all persisted between launches.

**Architecture:** Species choice moves out of `BugSimulation` behind a one-method `ISpeciesSource`, implemented by `SlotSpeciesSource` which reads the configured slots. Settings live in a `BugOptions` record with value equality, serialized by pure Core code and stored as JSON by the app. A small `OptionsApplier` diffs old against new options and pokes only what changed, which is what makes live preview and Cancel both work.

**Tech Stack:** .NET 10, C# 14, WPF, WinForms `NotifyIcon`, `System.Text.Json.Nodes`, xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-09-03-bug-options-design.md`. Section numbers below (for example "spec 2.3") refer to it.

**Conventions (from the user's global CLAUDE.md, mandatory):**
- Primary constructors; use the parameters directly, no `_field` copies, no null checks.
- One type per file, named for the type.
- Exception: a class whose constructor does real wiring (WPF `InitializeComponent`, WinForms component setup) uses an explicit constructor with fields. `TrayIcon` is the existing precedent.

**Commits:** every message ends with the trailer `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

**Branch:** `feat/bug-options`, already created from `main`. All paths are relative to `C:\Users\AddamBoord\source\repos\ScreenSavers`.

**Build and test commands.** `dotnet test` intermittently hangs on this machine when it also has to build, and piping its output makes it worse. Always build first, redirect to a file, and pass `-nodeReuse:false`:

```bash
export MSBUILDDISABLENODEREUSE=1
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
dotnet test tests/ScreenBugs.Tests -nologo -v q --no-build -nodeReuse:false
dotnet build src/ScreenBugs -nologo -v q -nodeReuse:false > /tmp/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" /tmp/b.log
```

**Starting point:** 37 tests pass on `main`. This plan adds roughly 30 more and modifies two existing files' call sites.

---

## File structure

```
src/ScreenBugs.Core/
  Simulation/BugTypeSlot.cs          new    one slot: a species or Random
  Simulation/BugTypeSlots.cs         new    pure slot-list rules (choices, resize, sanitize)
  Simulation/ISpeciesSource.cs       new    Next() -> BugSpecies
  Simulation/SlotSpeciesSource.cs    new    picks a slot, then a species
  Simulation/BugSimulation.cs        mod    constructor, SpawnFromEdge, RespawnAll
  Settings/TypeChangeBehavior.cs     new    RespawnAll | AgeOut
  Settings/BugOptions.cs             new    the settings record, value equality
  Settings/SettingsSerializer.cs     new    JSON in and out, total on bad input
src/ScreenBugs/
  Overlay/FrameLoop.cs               mod    TargetFrameRate replaces the const
  Settings/SettingsStore.cs          new    load/save %LocalAppData%\ScreenBugs\settings.json
  Settings/StartupRegistration.cs    new    HKCU Run key
  Settings/OptionsApplier.cs         new    diff and apply to the running app
  Options/BugTypeChoice.cs           new    ComboBox item with display label
  Options/OptionsWindow.xaml(.cs)    new    the dialog
  Tray/TrayIcon.cs                   mod    Pause / Options / Exit
  App.xaml.cs                        mod    load, wire, ShowOptions
  ScreenBugs.csproj                  mod    Core.Settings global using
tests/ScreenBugs.Tests/
  ScreenBugs.Tests.csproj            mod    Core.Settings global using
  SimulationSteps.cs                 mod    pass a SlotSpeciesSource
  ScriptedRandomSource.cs            new    test helper, exact draws
  BugTypeSlotsTests.cs               new
  SlotSpeciesSourceTests.cs          new
  BugOptionsTests.cs                 new
  SettingsSerializerTests.cs         new
  BugSpawnTests.cs                   mod    RespawnAll tests
```

Chunks: **1** is all of Core with its tests (the app is untouched and keeps building). **2** is the app-side plumbing, each piece independently buildable. **3** is the dialog and composition, ending in the manual checklist.

---

## Chunk 1: Core — slots, species source, options, serialization

### Task 1: Bug type slots

**Files:**
- Create: `src/ScreenBugs.Core/Simulation/BugTypeSlot.cs`
- Create: `src/ScreenBugs.Core/Simulation/BugTypeSlots.cs`
- Test: `tests/ScreenBugs.Tests/BugTypeSlotsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenBugs.Tests/BugTypeSlotsTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class BugTypeSlotsTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Roach = new(SpeciesId.HissingCockroach);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    [Fact]
    public void AllChoices_is_random_then_the_nine_species_in_catalog_order()
    {
        var choices = BugTypeSlots.AllChoices;

        Assert.Equal(10, choices.Count);
        Assert.True(choices[0].IsRandom);
        Assert.Equal(SpeciesCatalog.All.Select(s => s.Id), choices.Skip(1).Select(c => c.Species));
    }

    [Fact]
    public void AvailableFor_excludes_choices_held_by_other_slots_but_keeps_its_own()
    {
        var slots = new[] { Ant, Mantis };

        var forFirst = BugTypeSlots.AvailableFor(slots, 0);

        Assert.Contains(Ant, forFirst);
        Assert.DoesNotContain(Mantis, forFirst);
        Assert.Equal(9, forFirst.Count);
    }

    [Fact]
    public void AvailableFor_excludes_random_when_another_slot_holds_it()
    {
        var slots = new[] { Ant, BugTypeSlot.Random };

        var forFirst = BugTypeSlots.AvailableFor(slots, 0);

        Assert.DoesNotContain(BugTypeSlot.Random, forFirst);
        Assert.Contains(Ant, forFirst);
    }

    [Fact]
    public void Resize_growing_appends_the_first_unused_choices()
    {
        var grown = BugTypeSlots.Resize([Ant], 3);

        Assert.Equal([Ant, BugTypeSlot.Random, Roach], grown);
    }

    [Fact]
    public void Resize_shrinking_keeps_the_leading_slots()
    {
        var shrunk = BugTypeSlots.Resize([Ant, Mantis, Roach], 2);

        Assert.Equal([Ant, Mantis], shrunk);
    }

    [Fact]
    public void Resize_clamps_the_count_between_one_and_max()
    {
        Assert.Single(BugTypeSlots.Resize([Ant, Mantis], 0));
        Assert.Equal(BugTypeSlots.MaxSlots, BugTypeSlots.Resize([Ant], 99).Count);
    }

    [Fact]
    public void Resize_to_max_produces_every_distinct_choice()
    {
        var all = BugTypeSlots.Resize([Ant], BugTypeSlots.MaxSlots);

        Assert.Equal(BugTypeSlots.MaxSlots, all.Distinct().Count());
    }

    [Fact]
    public void Sanitize_drops_duplicates_and_never_returns_empty()
    {
        Assert.Equal([Ant, Mantis], BugTypeSlots.Sanitize([Ant, Mantis, Ant]));
        Assert.Equal([BugTypeSlot.Random], BugTypeSlots.Sanitize([]));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run the build command for the test project.
Expected: FAIL with `CS0246: The type or namespace name 'BugTypeSlot' could not be found`.

- [ ] **Step 3: Create BugTypeSlot**

Create `src/ScreenBugs.Core/Simulation/BugTypeSlot.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

/// <summary>
/// One bug-type slot in the options: a specific species, or Random when
/// <see cref="Species"/> is null, meaning any of the nine at spawn time.
/// </summary>
public readonly record struct BugTypeSlot(SpeciesId? Species)
{
    public static readonly BugTypeSlot Random = new(null);

    public bool IsRandom => Species is null;
}
```

- [ ] **Step 4: Create BugTypeSlots**

Create `src/ScreenBugs.Core/Simulation/BugTypeSlots.cs`. These are pure functions so the dialog stays dumb and the rules are testable (spec 2.3).

```csharp
namespace ScreenBugs.Core.Simulation;

/// <summary>Rules for a list of <see cref="BugTypeSlot"/>: what a slot may hold, and how the list resizes.</summary>
public static class BugTypeSlots
{
    /// <summary>Ten distinct choices exist, so a duplicate-free list can never be longer.</summary>
    public const int MaxSlots = 10;

    public static IReadOnlyList<BugTypeSlot> AllChoices { get; } =
        [BugTypeSlot.Random, .. SpeciesCatalog.All.Select(species => new BugTypeSlot(species.Id))];

    /// <summary>Every choice not held by a different slot, plus this slot's own current value.</summary>
    public static IReadOnlyList<BugTypeSlot> AvailableFor(IReadOnlyList<BugTypeSlot> slots, int index) =>
        AllChoices.Where(choice => choice == slots[index] || !HeldByOther(slots, index, choice)).ToList();

    /// <summary>Clamps to [1, MaxSlots]. Shrinking keeps the prefix; growing appends unused choices.</summary>
    public static IReadOnlyList<BugTypeSlot> Resize(IReadOnlyList<BugTypeSlot> slots, int count)
    {
        count = Math.Clamp(count, 1, MaxSlots);
        var resized = slots.Take(count).ToList();
        while (resized.Count < count)
        {
            // Slots appended earlier in this loop count as taken.
            resized.Add(AllChoices.First(choice => !resized.Contains(choice)));
        }

        return resized;
    }

    /// <summary>Drops duplicates keeping the first occurrence; an empty result becomes a single Random slot.</summary>
    public static IReadOnlyList<BugTypeSlot> Sanitize(IReadOnlyList<BugTypeSlot> slots)
    {
        var unique = new List<BugTypeSlot>();
        foreach (var slot in slots)
        {
            if (!unique.Contains(slot))
            {
                unique.Add(slot);
            }
        }

        return unique.Count == 0 ? [BugTypeSlot.Random] : unique;
    }

    private static bool HeldByOther(IReadOnlyList<BugTypeSlot> slots, int index, BugTypeSlot choice)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i != index && slots[i] == choice)
            {
                return true;
            }
        }

        return false;
    }
}
```

- [ ] **Step 5: Run the tests**

Expected: `Passed!` with 45 total (37 existing plus 8 new).

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/BugTypeSlot.cs src/ScreenBugs.Core/Simulation/BugTypeSlots.cs tests/ScreenBugs.Tests/BugTypeSlotsTests.cs
git commit -m "feat(core): bug type slots and their selection rules

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 2: Species source

**Files:**
- Create: `src/ScreenBugs.Core/Simulation/ISpeciesSource.cs`
- Create: `src/ScreenBugs.Core/Simulation/SlotSpeciesSource.cs`
- Create: `tests/ScreenBugs.Tests/ScriptedRandomSource.cs`
- Test: `tests/ScreenBugs.Tests/SlotSpeciesSourceTests.cs`

- [ ] **Step 1: Write the test helper**

Create `tests/ScreenBugs.Tests/ScriptedRandomSource.cs`. Tests that need exact draws use this instead of a seed.

```csharp
namespace ScreenBugs.Tests;

/// <summary>An <see cref="IRandomSource"/> returning queued integers, so a test can force exact draws.</summary>
internal sealed class ScriptedRandomSource(params int[] values) : IRandomSource
{
    private readonly Queue<int> queued = new(values);

    public float NextFloat() => 0f;

    public float NextFloat(float min, float max) => min;

    public int NextInt(int maxExclusive)
    {
        if (queued.Count == 0)
        {
            throw new InvalidOperationException("ScriptedRandomSource ran out of queued values.");
        }

        return Math.Clamp(queued.Dequeue(), 0, maxExclusive - 1);
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ScreenBugs.Tests/SlotSpeciesSourceTests.cs`. The draw order fixed in spec 3 is what makes the scripted tests possible.

```csharp
namespace ScreenBugs.Tests;

public sealed class SlotSpeciesSourceTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    private static SlotSpeciesSource Seeded(params BugTypeSlot[] slots) =>
        new(new SystemRandomSource(1234)) { Slots = slots };

    [Fact]
    public void A_single_species_slot_always_yields_that_species()
    {
        var source = Seeded(Ant);

        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Id);
        }
    }

    [Fact]
    public void A_single_random_slot_yields_every_species()
    {
        var source = Seeded(BugTypeSlot.Random);

        var seen = new HashSet<SpeciesId>();
        for (int i = 0; i < 2000; i++)
        {
            seen.Add(source.Next().Id);
        }

        Assert.Equal(9, seen.Count);
    }

    [Fact]
    public void Two_species_slots_yield_only_those_two()
    {
        var source = Seeded(Ant, Mantis);

        var seen = new HashSet<SpeciesId>();
        for (int i = 0; i < 500; i++)
        {
            seen.Add(source.Next().Id);
        }

        Assert.Equal([SpeciesId.BlackGardenAnt, SpeciesId.PrayingMantis], seen.Order());
    }

    [Fact]
    public void A_random_slot_may_repeat_a_species_another_slot_holds()
    {
        // Draw 1 picks slot index 1 (Random); draw 2 picks catalog index 1 (black garden ant).
        var source = new SlotSpeciesSource(new ScriptedRandomSource(1, 1)) { Slots = [Ant, BugTypeSlot.Random] };

        Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Id);
    }

    [Fact]
    public void A_single_species_slot_makes_no_random_draw_at_all()
    {
        var source = new SlotSpeciesSource(new ScriptedRandomSource()) { Slots = [Ant] };

        Assert.Equal(SpeciesId.BlackGardenAnt, source.Next().Id);
    }

    [Fact]
    public void A_single_random_slot_draws_only_the_species_index()
    {
        var source = new SlotSpeciesSource(new ScriptedRandomSource(3)) { Slots = [BugTypeSlot.Random] };

        Assert.Equal(SpeciesCatalog.All[3].Id, source.Next().Id);
    }

    [Fact]
    public void Setting_slots_sanitizes_them()
    {
        var source = Seeded(Ant);

        source.Slots = [];
        Assert.Equal([BugTypeSlot.Random], source.Slots);

        source.Slots = [Ant, Ant];
        Assert.Equal([Ant], source.Slots);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Expected: FAIL with `CS0246: The type or namespace name 'SlotSpeciesSource' could not be found`.

- [ ] **Step 4: Create the interface and implementation**

Create `src/ScreenBugs.Core/Simulation/ISpeciesSource.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

/// <summary>Decides which species each new bug is. Lets the simulation stay ignorant of the options.</summary>
public interface ISpeciesSource
{
    BugSpecies Next();
}
```

Create `src/ScreenBugs.Core/Simulation/SlotSpeciesSource.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

/// <summary>Chooses a species from the configured slots (spec 2.1): pick a slot, then resolve it.</summary>
public sealed class SlotSpeciesSource(IRandomSource rng) : ISpeciesSource
{
    private IReadOnlyList<BugTypeSlot> slots = [BugTypeSlot.Random];

    /// <summary>Assigning runs <see cref="BugTypeSlots.Sanitize"/>, so this is never empty and never has duplicates.</summary>
    public IReadOnlyList<BugTypeSlot> Slots
    {
        get => slots;
        set => slots = BugTypeSlots.Sanitize(value);
    }

    public BugSpecies Next()
    {
        // With one slot no draw is made, which keeps seeded runs identical to the pre-options behavior.
        BugTypeSlot slot = slots.Count == 1 ? slots[0] : slots[rng.NextInt(slots.Count)];

        return slot.Species is { } species
            ? SpeciesCatalog.Get(species)
            : SpeciesCatalog.All[rng.NextInt(SpeciesCatalog.All.Count)];
    }
}
```

- [ ] **Step 5: Run the tests**

Expected: `Passed!` with 52 total.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/ISpeciesSource.cs src/ScreenBugs.Core/Simulation/SlotSpeciesSource.cs tests/ScreenBugs.Tests/ScriptedRandomSource.cs tests/ScreenBugs.Tests/SlotSpeciesSourceTests.cs
git commit -m "feat(core): slot-driven species source with a scripted-random test helper

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3: Simulation uses the species source and can respawn everything

**Files:**
- Modify: `src/ScreenBugs.Core/Simulation/BugSimulation.cs`
- Modify: `tests/ScreenBugs.Tests/SimulationSteps.cs`
- Test: `tests/ScreenBugs.Tests/BugSpawnTests.cs`

- [ ] **Step 1: Add the failing RespawnAll tests**

Append inside the `BugSpawnTests` class in `tests/ScreenBugs.Tests/BugSpawnTests.cs`:

```csharp
    [Fact]
    public void RespawnAll_replaces_every_alive_bug_and_clears_the_respawn_timer()
    {
        var sim = SimulationSteps.Create(3);
        sim.TrySquashAt(sim.Bugs[0].Position);
        sim.Step(SimulationSteps.Dt, null);
        Assert.NotNull(sim.RespawnTimer);
        int maxIdBefore = sim.Bugs.Max(b => b.Id);
        var squashed = sim.Bugs.Single(b => !b.IsAlive);

        sim.RespawnAll();

        Assert.Equal(3, SimulationSteps.AliveCount(sim));
        Assert.All(sim.Bugs.Where(b => b.IsAlive), b => Assert.True(b.Id > maxIdBefore));
        Assert.Contains(squashed, sim.Bugs);
        Assert.Null(sim.RespawnTimer);
    }

    [Fact]
    public void RespawnAll_brings_the_new_bugs_in_from_the_edges()
    {
        var sim = SimulationSteps.Create(4);
        SimulationSteps.StepFor(sim, 3f);

        sim.RespawnAll();

        foreach (var bug in sim.Bugs.Where(b => b.IsAlive))
        {
            Assert.False(SimulationSteps.Screen.Contains(bug.Position));
            Assert.False(bug.HasEnteredScreen);
            var toCenter = SimulationSteps.Screen.Center - bug.Position;
            Assert.True(Vector2.Dot(SimulationSteps.Direction(bug.Heading), toCenter) > 0f);
        }
    }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL with `CS1061: 'BugSimulation' does not contain a definition for 'RespawnAll'`.

- [ ] **Step 3: Change the constructor and the spawn call site**

In `src/ScreenBugs.Core/Simulation/BugSimulation.cs`, change the class declaration line:

```csharp
public sealed class BugSimulation(Bounds bounds, IRandomSource rng, ISpeciesSource speciesSource)
```

and in `SpawnFromEdge`, replace the first line of the method body:

```csharp
        var species = SpeciesCatalog.All[rng.NextInt(SpeciesCatalog.All.Count)];
```

with:

```csharp
        var species = speciesSource.Next();
```

Everything else in `SpawnFromEdge` (position, heading, seed, speed) is unchanged.

- [ ] **Step 4: Add RespawnAll**

Add this method to `BugSimulation`, directly after the `TrySquashAt` method:

```csharp
    /// <summary>
    /// Removes every alive bug and walks a fresh population in from the edges. Squashed bugs are
    /// left to finish fading. Used when the selected bug types change (spec 4).
    /// </summary>
    public void RespawnAll()
    {
        bugs.RemoveAll(bug => bug.IsAlive);
        respawnTimer = null;
        while (AliveCount < targetCount)
        {
            SpawnFromEdge();
        }
    }
```

- [ ] **Step 5: Update the test factory**

In `tests/ScreenBugs.Tests/SimulationSteps.cs`, replace the `Create` method:

```csharp
    public static BugSimulation Create(int count, int seed = 1234)
    {
        var rng = new SystemRandomSource(seed);
        return new BugSimulation(Screen, rng, new SlotSpeciesSource(rng)) { TargetCount = count };
    }
```

A default `SlotSpeciesSource` holds one Random slot, which reproduces the previous uniform choice, so the existing tests are unaffected.

- [ ] **Step 6: Run the tests**

Expected: `Passed!` with 54 total, and all 37 original tests still passing.

- [ ] **Step 7: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/BugSimulation.cs tests/ScreenBugs.Tests/SimulationSteps.cs tests/ScreenBugs.Tests/BugSpawnTests.cs
git commit -m "feat(core): drive species choice through ISpeciesSource and add RespawnAll

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4: Options record

**Files:**
- Create: `src/ScreenBugs.Core/Settings/TypeChangeBehavior.cs`
- Create: `src/ScreenBugs.Core/Settings/BugOptions.cs`
- Modify: `tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj`
- Test: `tests/ScreenBugs.Tests/BugOptionsTests.cs`

- [ ] **Step 1: Add the global using**

In `tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj`, add one line to the `ItemGroup` that holds the other `Using` entries:

```xml
    <Using Include="ScreenBugs.Core.Settings" />
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ScreenBugs.Tests/BugOptionsTests.cs`. A record compares an `IReadOnlyList` member by reference, so without the override two identical option sets would be unequal and the applier would respawn spuriously (spec 2.2).

```csharp
namespace ScreenBugs.Tests;

public sealed class BugOptionsTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Mantis = new(SpeciesId.PrayingMantis);

    [Fact]
    public void Default_equals_a_second_default_despite_a_fresh_slot_list()
    {
        Assert.Equal(BugOptions.Default, BugOptions.Default);
        Assert.Equal(BugOptions.Default.GetHashCode(), BugOptions.Default.GetHashCode());
    }

    [Fact]
    public void Defaults_are_one_black_ant_five_bugs_sixty_fps_respawn_all()
    {
        var options = BugOptions.Default;

        Assert.Equal([Ant], options.TypeSlots);
        Assert.Equal(5, options.BugCount);
        Assert.Equal(60, options.FrameRate);
        Assert.Equal(TypeChangeBehavior.RespawnAll, options.OnTypeChange);
    }

    [Fact]
    public void Records_with_equal_slot_contents_in_different_lists_are_equal()
    {
        var a = BugOptions.Default with { TypeSlots = new List<BugTypeSlot> { Ant, Mantis } };
        var b = BugOptions.Default with { TypeSlots = new[] { Ant, Mantis } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Changing_any_member_breaks_equality()
    {
        var baseline = BugOptions.Default with { TypeSlots = new[] { Ant, Mantis } };

        Assert.NotEqual(baseline, baseline with { TypeSlots = new[] { Ant } });
        Assert.NotEqual(baseline, baseline with { BugCount = 6 });
        Assert.NotEqual(baseline, baseline with { FrameRate = 30 });
        Assert.NotEqual(baseline, baseline with { OnTypeChange = TypeChangeBehavior.AgeOut });
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Expected: FAIL with `CS0246: The type or namespace name 'BugOptions' could not be found`.

- [ ] **Step 4: Create the enum and the record**

Create `src/ScreenBugs.Core/Settings/TypeChangeBehavior.cs`:

```csharp
namespace ScreenBugs.Core.Settings;

/// <summary>What happens to bugs already on screen when the selected types change.</summary>
public enum TypeChangeBehavior
{
    /// <summary>Clear the screen and walk a fresh population in.</summary>
    RespawnAll,

    /// <summary>Leave them; only replacements use the new types.</summary>
    AgeOut,
}
```

Create `src/ScreenBugs.Core/Settings/BugOptions.cs`:

```csharp
using ScreenBugs.Core.Simulation;

namespace ScreenBugs.Core.Settings;

/// <summary>Everything the Options dialog controls, except the startup registration (spec 2.2).</summary>
public sealed record BugOptions(
    IReadOnlyList<BugTypeSlot> TypeSlots,
    int BugCount,
    int FrameRate,
    TypeChangeBehavior OnTypeChange)
{
    public static BugOptions Default => new(
        [new BugTypeSlot(SpeciesId.BlackGardenAnt)],
        BugCount: 5,
        FrameRate: 60,
        TypeChangeBehavior.RespawnAll);

    /// <summary>Compares slots element by element; the synthesized version would compare the list by reference.</summary>
    public bool Equals(BugOptions? other) =>
        other is not null
        && BugCount == other.BugCount
        && FrameRate == other.FrameRate
        && OnTypeChange == other.OnTypeChange
        && TypeSlots.SequenceEqual(other.TypeSlots);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BugCount);
        hash.Add(FrameRate);
        hash.Add(OnTypeChange);
        foreach (var slot in TypeSlots)
        {
            hash.Add(slot);
        }

        return hash.ToHashCode();
    }
}
```

- [ ] **Step 5: Run the tests**

Expected: `Passed!` with 58 total.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Settings tests/ScreenBugs.Tests/BugOptionsTests.cs tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj
git commit -m "feat(core): BugOptions record with value equality on the slot list

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 5: Settings serialization

**Files:**
- Create: `src/ScreenBugs.Core/Settings/SettingsSerializer.cs`
- Test: `tests/ScreenBugs.Tests/SettingsSerializerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/ScreenBugs.Tests/SettingsSerializerTests.cs`. `Deserialize` must be total: `JsonNode.Parse` throws on malformed input and returns null for the literal `null`, and reading a wrong-typed field must not discard the good ones (spec 5).

```csharp
namespace ScreenBugs.Tests;

public sealed class SettingsSerializerTests
{
    private static readonly BugTypeSlot Ant = new(SpeciesId.BlackGardenAnt);
    private static readonly BugTypeSlot Centipede = new(SpeciesId.Centipede);

    [Fact]
    public void Round_trip_preserves_every_field()
    {
        var original = new BugOptions(
            [Ant, BugTypeSlot.Random, new BugTypeSlot(SpeciesId.PrayingMantis)],
            BugCount: 7,
            FrameRate: 120,
            TypeChangeBehavior.AgeOut);

        Assert.Equal(original, SettingsSerializer.Deserialize(SettingsSerializer.Serialize(original)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("{")]
    [InlineData("{}")]
    public void Unusable_input_yields_the_defaults(string json)
    {
        Assert.Equal(BugOptions.Default, SettingsSerializer.Deserialize(json));
    }

    [Fact]
    public void A_wrong_typed_field_does_not_discard_the_others()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede"],"BugCount":"5"}""");

        Assert.Equal([Centipede], options.TypeSlots);
        Assert.Equal(5, options.BugCount);
    }

    [Fact]
    public void Unknown_and_null_slot_names_are_dropped()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede","99","Unicorn",null,5]}""");

        Assert.Equal([Centipede], options.TypeSlots);
    }

    [Fact]
    public void Slot_names_are_case_insensitive()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["random","blackgardenant"]}""");

        Assert.Equal([BugTypeSlot.Random, Ant], options.TypeSlots);
    }

    [Fact]
    public void An_all_unknown_slot_list_falls_back_to_the_default_slots()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Unicorn"]}""");

        Assert.Equal(BugOptions.Default.TypeSlots, options.TypeSlots);
    }

    [Fact]
    public void Duplicate_slots_are_sanitized_away()
    {
        var options = SettingsSerializer.Deserialize("""{"TypeSlots":["Centipede","Centipede"]}""");

        Assert.Equal([Centipede], options.TypeSlots);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(999, 50)]
    [InlineData(7, 7)]
    public void Bug_count_is_clamped(int written, int expected)
    {
        Assert.Equal(expected, SettingsSerializer.Deserialize($$"""{"BugCount":{{written}}}""").BugCount);
    }

    [Theory]
    [InlineData(45, 60)]
    [InlineData(30, 30)]
    [InlineData(120, 120)]
    public void Only_the_three_allowed_frame_rates_survive(int written, int expected)
    {
        Assert.Equal(expected, SettingsSerializer.Deserialize($$"""{"FrameRate":{{written}}}""").FrameRate);
    }

    [Fact]
    public void An_unknown_type_change_becomes_respawn_all()
    {
        var options = SettingsSerializer.Deserialize("""{"OnTypeChange":"Sideways"}""");

        Assert.Equal(TypeChangeBehavior.RespawnAll, options.OnTypeChange);
    }

    [Fact]
    public void An_unknown_property_is_ignored()
    {
        var options = SettingsSerializer.Deserialize("""{"BugCount":9,"FutureSetting":true}""");

        Assert.Equal(9, options.BugCount);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL with `CS0103: The name 'SettingsSerializer' does not exist in the current context`.

- [ ] **Step 3: Create the serializer**

Create `src/ScreenBugs.Core/Settings/SettingsSerializer.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ScreenBugs.Core.Simulation;

namespace ScreenBugs.Core.Settings;

/// <summary>
/// Reads and writes <see cref="BugOptions"/> as JSON. Reading is total: any input at all yields a
/// valid record, and each field falls back independently so one bad value cannot lose the rest.
/// </summary>
public static class SettingsSerializer
{
    private const string RandomName = "Random";
    private const int MinBugCount = 1;
    private const int MaxBugCount = 50;
    private static readonly int[] AllowedFrameRates = [30, 60, 120];

    public static string Serialize(BugOptions options)
    {
        var slots = new JsonArray();
        foreach (var slot in options.TypeSlots)
        {
            slots.Add(slot.Species is { } species ? species.ToString() : RandomName);
        }

        var root = new JsonObject
        {
            ["TypeSlots"] = slots,
            ["BugCount"] = options.BugCount,
            ["FrameRate"] = options.FrameRate,
            ["OnTypeChange"] = options.OnTypeChange.ToString(),
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static BugOptions Deserialize(string json)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return BugOptions.Default;
        }

        if (root is null)
        {
            return BugOptions.Default;
        }

        return new BugOptions(
            ReadSlots(root["TypeSlots"]),
            ReadBugCount(root["BugCount"]),
            ReadFrameRate(root["FrameRate"]),
            ReadTypeChange(root["OnTypeChange"]));
    }

    private static IReadOnlyList<BugTypeSlot> ReadSlots(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return BugOptions.Default.TypeSlots;
        }

        var slots = new List<BugTypeSlot>();
        foreach (var element in array)
        {
            if (TryReadSlot(element, out var slot))
            {
                slots.Add(slot);
            }
        }

        return slots.Count == 0 ? BugOptions.Default.TypeSlots : BugTypeSlots.Sanitize(slots);
    }

    private static bool TryReadSlot(JsonNode? node, out BugTypeSlot slot)
    {
        slot = BugTypeSlot.Random;
        if (node is not JsonValue value || !value.TryGetValue(out string? name) || name is null)
        {
            return false;
        }

        if (string.Equals(name, RandomName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // TryParse alone accepts numeric strings such as "99", so IsDefined must confirm it.
        if (Enum.TryParse(name, ignoreCase: true, out SpeciesId species) && Enum.IsDefined(species))
        {
            slot = new BugTypeSlot(species);
            return true;
        }

        return false;
    }

    private static int ReadBugCount(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out int count)
            ? Math.Clamp(count, MinBugCount, MaxBugCount)
            : BugOptions.Default.BugCount;

    private static int ReadFrameRate(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out int rate) && AllowedFrameRates.Contains(rate)
            ? rate
            : BugOptions.Default.FrameRate;

    private static TypeChangeBehavior ReadTypeChange(JsonNode? node) =>
        node is JsonValue value
        && value.TryGetValue(out string? name)
        && name is not null
        && Enum.TryParse(name, ignoreCase: true, out TypeChangeBehavior behavior)
        && Enum.IsDefined(behavior)
            ? behavior
            : BugOptions.Default.OnTypeChange;
}
```

- [ ] **Step 4: Run the tests**

Expected: `Passed!` with 76 total (the `Theory` cases each count).

If `Only_the_three_allowed_frame_rates_survive` or `Bug_count_is_clamped` fails on a value like `5.0`, that is `JsonValue.TryGetValue<int>` refusing a non-integer number, which is the intended strictness; check the test input rather than loosening the reader.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs.Core/Settings/SettingsSerializer.cs tests/ScreenBugs.Tests/SettingsSerializerTests.cs
git commit -m "feat(core): total JSON serializer for BugOptions

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

**Note on the build between Tasks 3 and 6.** Task 3 changes the `BugSimulation`
constructor, which the app calls, so from that point the **app project does not
compile** until Task 6 Step 3 repairs the call site. This is expected and
short-lived: build and test only `tests/ScreenBugs.Tests` during Tasks 3 to 5,
then Task 6 restores the whole solution. Do not start Chunk 2 out of order, and
do not "fix" the app project early by guessing at the final composition — Task
12 does that properly.

Chunk 1 is complete when `dotnet test tests/ScreenBugs.Tests` reports 76 passing.

<!-- end of chunk 1 -->

## Chunk 2: App plumbing

### Task 6: Keep the app compiling and make the frame rate settable

**Files:**
- Modify: `src/ScreenBugs/App.xaml.cs`
- Modify: `src/ScreenBugs/Overlay/FrameLoop.cs`
- Modify: `src/ScreenBugs/ScreenBugs.csproj`

- [ ] **Step 1: Add the Core.Settings global using**

In `src/ScreenBugs/ScreenBugs.csproj`, add one line to the `ItemGroup` holding the other `Using` entries:

```xml
    <Using Include="ScreenBugs.Core.Settings" />
```

- [ ] **Step 2: Make the frame rate settable**

In `src/ScreenBugs/Overlay/FrameLoop.cs`, delete the line:

```csharp
    private const double Interval = 1.0 / 60.0;
```

and add these members immediately after the `accumulator` field:

```csharp
    private int targetFrameRate = 60;

    /// <summary>Ticks per second: 30, 60 or 120. Setting it clears the accumulator so the next tick is not distorted.</summary>
    public int TargetFrameRate
    {
        get => targetFrameRate;
        set
        {
            targetFrameRate = value;
            accumulator = 0;
        }
    }

    private double Interval => 1.0 / targetFrameRate;
```

The two existing uses of `Interval` inside `OnRendering` need no change.

- [ ] **Step 3: Repair the simulation construction**

In `src/ScreenBugs/App.xaml.cs`, replace this line inside `OnStartup`:

```csharp
        var simulation = new BugSimulation(bounds, new SystemRandomSource()) { TargetCount = InitialBugCount };
```

with:

```csharp
        var rng = new SystemRandomSource();
        var speciesSource = new SlotSpeciesSource(rng);
        var simulation = new BugSimulation(bounds, rng, speciesSource) { TargetCount = InitialBugCount };
```

Task 13 replaces this again with the loaded options; this keeps the build green in between.

- [ ] **Step 4: Build both projects**

Run the app build command and the test build command.
Expected: both succeed with 0 warnings and 0 errors, and the suite still passes.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs/ScreenBugs.csproj src/ScreenBugs/Overlay/FrameLoop.cs src/ScreenBugs/App.xaml.cs
git commit -m "feat(overlay): settable TargetFrameRate; wire the app to the new simulation constructor

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 7: Settings file and startup registration

**Files:**
- Create: `src/ScreenBugs/Settings/SettingsStore.cs`
- Create: `src/ScreenBugs/Settings/StartupRegistration.cs`

Neither is unit-tested: both are thin wrappers over the file system and the registry, and the manual checklist in Task 13 covers them.

- [ ] **Step 1: Write SettingsStore**

Create `src/ScreenBugs/Settings/SettingsStore.cs`:

```csharp
using System.IO;
using ScreenBugs.Diagnostics;

namespace ScreenBugs.Settings;

/// <summary>Loads and saves the options file beside the crash log. Never throws (spec 5).</summary>
public static class SettingsStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenBugs",
        "settings.json");

    public static BugOptions Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? SettingsSerializer.Deserialize(File.ReadAllText(FilePath))
                : BugOptions.Default;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return BugOptions.Default;
        }
    }

    public static void Save(BugOptions options)
    {
        try
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);

            // Write beside the target and move over it, so a crash mid-write cannot truncate the file.
            string temporary = Path.Combine(directory, "settings.json.tmp");
            File.WriteAllText(temporary, SettingsSerializer.Serialize(options));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
        }
    }
}
```

- [ ] **Step 2: Write StartupRegistration**

Create `src/ScreenBugs/Settings/StartupRegistration.cs`:

```csharp
using Microsoft.Win32;
using ScreenBugs.Diagnostics;

namespace ScreenBugs.Settings;

/// <summary>Owns this app's value under the per-user Run key (spec 6). Never throws.</summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenBugs";

    /// <summary>True when a value exists, whatever path it holds: that is what Windows will try to launch.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return false;
        }
    }

    /// <summary>Enabling always rewrites the current executable path, which repairs a stale entry.</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
        }
    }
}
```

- [ ] **Step 3: Build and commit**

Run the app build command. Expected: 0 warnings, 0 errors.

```bash
git add src/ScreenBugs/Settings
git commit -m "feat(app): settings file store and Run-key startup registration

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 8: Options applier

**Files:**
- Create: `src/ScreenBugs/Settings/OptionsApplier.cs`

- [ ] **Step 1: Write the applier**

Create `src/ScreenBugs/Settings/OptionsApplier.cs`. The slot-change policy is a parameter rather than being read from `next`, so the dialog can revert differently than it previewed (spec 4 and 8.3).

```csharp
using ScreenBugs.Overlay;

namespace ScreenBugs.Settings;

/// <summary>Applies the difference between two option sets to the running overlay.</summary>
public sealed class OptionsApplier(BugSimulation simulation, SlotSpeciesSource species, FrameLoop frameLoop)
{
    /// <summary>
    /// Applies what differs, in the order slots, count, frame rate, so bugs spawned by a count
    /// increase already use the new slots. Returns true if the population was respawned.
    /// </summary>
    public bool Apply(BugOptions previous, BugOptions next, TypeChangeBehavior onSlotChange)
    {
        bool respawned = false;

        if (!previous.TypeSlots.SequenceEqual(next.TypeSlots))
        {
            species.Slots = next.TypeSlots;
            if (onSlotChange == TypeChangeBehavior.RespawnAll)
            {
                simulation.RespawnAll();
                respawned = true;
            }
        }

        if (previous.BugCount != next.BugCount)
        {
            simulation.TargetCount = next.BugCount;
        }

        if (previous.FrameRate != next.FrameRate)
        {
            frameLoop.TargetFrameRate = next.FrameRate;
        }

        return respawned;
    }
}
```

- [ ] **Step 2: Build and commit**

Run the app build command. Expected: 0 warnings, 0 errors.

```bash
git add src/ScreenBugs/Settings/OptionsApplier.cs
git commit -m "feat(app): OptionsApplier diffs settings onto the running overlay

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 9: Tray menu

**Files:**
- Modify: `src/ScreenBugs/Tray/TrayIcon.cs`
- Modify: `src/ScreenBugs/App.xaml.cs`

- [ ] **Step 1: Replace the count submenu with Options**

In `src/ScreenBugs/Tray/TrayIcon.cs`:

Delete the `CountChoices` field, the `countItems` field, the `BugCountChanged` event and the `SelectCount` method.

Add this event beside the other two:

```csharp
    public event Action? OptionsRequested;
```

Replace the constructor signature `public TrayIcon(int initialCount)` with `public TrayIcon()`, and inside it replace the block that builds `countItems` and `bugsMenu`:

```csharp
        countItems = CountChoices
            .Select(count => new ToolStripMenuItem(count.ToString()) { Checked = count == initialCount, Tag = count })
            .ToArray();
        var bugsMenu = new ToolStripMenuItem("Bugs");
        foreach (var item in countItems)
        {
            item.Click += (_, _) => SelectCount(item);
            bugsMenu.DropDownItems.Add(item);
        }
```

with:

```csharp
        var optionsItem = new ToolStripMenuItem("Options...");
        optionsItem.Click += (_, _) => OptionsRequested?.Invoke();
```

and replace the menu assembly line `menu.Items.Add(bugsMenu);` with `menu.Items.Add(optionsItem);`.

Everything else — `RouteThreadExceptions`, `IsMenuOpen`, `SetPaused`, `Dispose` — is unchanged.

- [ ] **Step 2: Fix the construction call**

In `src/ScreenBugs/App.xaml.cs`, replace:

```csharp
        trayIcon = new TrayIcon(InitialBugCount);
```

with:

```csharp
        trayIcon = new TrayIcon();
```

and delete the now-dangling subscription line:

```csharp
        trayIcon.BugCountChanged += count => simulation.TargetCount = count;
```

- [ ] **Step 3: Build and commit**

Run the app build command. Expected: 0 warnings, 0 errors.

```bash
git add src/ScreenBugs/Tray/TrayIcon.cs src/ScreenBugs/App.xaml.cs
git commit -m "feat(tray): replace the bug-count submenu with Options

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

<!-- end of chunk 2 -->

## Chunk 3: Dialog and composition

### Task 10: ComboBox item type

**Files:**
- Create: `src/ScreenBugs/Options/BugTypeChoice.cs`

- [ ] **Step 1: Write BugTypeChoice**

Create `src/ScreenBugs/Options/BugTypeChoice.cs`:

```csharp
namespace ScreenBugs.Options;

/// <summary>A slot value plus the label shown for it in the dialog's dropdowns.</summary>
public sealed record BugTypeChoice(BugTypeSlot Slot, string Label)
{
    public static BugTypeChoice From(BugTypeSlot slot) => new(slot, LabelFor(slot));

    private static string LabelFor(BugTypeSlot slot) => slot.Species switch
    {
        null => "Random",
        SpeciesId.HissingCockroach => "Hissing cockroach",
        SpeciesId.BlackGardenAnt => "Black garden ant",
        SpeciesId.RedFireAnt => "Red fire ant",
        SpeciesId.PrayingMantis => "Praying mantis",
        SpeciesId.SevenSpotLadybug => "Seven-spot ladybug",
        SpeciesId.StagBeetle => "Stag beetle",
        SpeciesId.HouseSpider => "House spider",
        SpeciesId.Centipede => "Centipede",
        SpeciesId.StinkBug => "Stink bug",
        _ => slot.Species.Value.ToString(),
    };
}
```

- [ ] **Step 2: Build and commit**

Run the app build command. Expected: 0 warnings, 0 errors.

```bash
git add src/ScreenBugs/Options/BugTypeChoice.cs
git commit -m "feat(options): ComboBox choice type with display labels

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 11: Options dialog

**Files:**
- Create: `src/ScreenBugs/Options/OptionsWindow.xaml`
- Create: `src/ScreenBugs/Options/OptionsWindow.xaml.cs`

- [ ] **Step 1: Write the XAML**

Create `src/ScreenBugs/Options/OptionsWindow.xaml`. Slot rows are built in code because their number changes, so the panel is left empty here.

```xml
<Window x:Class="ScreenBugs.Options.OptionsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Screen Bugs options"
        Width="400"
        SizeToContent="Height"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterScreen"
        ShowInTaskbar="True">
    <StackPanel Margin="16">
        <TextBlock Text="Bug types" FontWeight="SemiBold" Margin="0,0,0,8" />

        <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Number of types" Width="130" VerticalAlignment="Center" />
            <ComboBox x:Name="SlotCountBox" Width="200" SelectionChanged="OnSlotCountChanged" />
        </StackPanel>

        <StackPanel x:Name="SlotPanel" Margin="0,0,0,16" />

        <StackPanel Orientation="Horizontal" Margin="0,0,0,16">
            <TextBlock Text="Bugs on screen" Width="130" VerticalAlignment="Center" />
            <Slider x:Name="CountSlider" Width="160" Minimum="1" Maximum="50"
                    IsSnapToTickEnabled="True" TickFrequency="1"
                    VerticalAlignment="Center" ValueChanged="OnCountChanged" />
            <TextBlock x:Name="CountText" Width="32" Margin="8,0,0,0" VerticalAlignment="Center" />
        </StackPanel>

        <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
            <TextBlock Text="Frame rate" Width="130" VerticalAlignment="Center" />
            <ComboBox x:Name="FrameRateBox" Width="200" SelectionChanged="OnFrameRateChanged" />
        </StackPanel>

        <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
            <TextBlock Text="When types change" Width="130" VerticalAlignment="Center" />
            <ComboBox x:Name="TypeChangeBox" Width="200" SelectionChanged="OnTypeChangeChanged" />
        </StackPanel>

        <CheckBox x:Name="StartupBox" Content="Run at Windows startup" Margin="0,0,0,20" />

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="OK" Width="84" IsDefault="True" Click="OnOk" Margin="0,0,8,0" />
            <Button Content="Cancel" Width="84" IsCancel="True" />
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 2: Write the code-behind**

Create `src/ScreenBugs/Options/OptionsWindow.xaml.cs`. An explicit constructor is required here (`InitializeComponent` plus control population), which is the documented exception to the primary-constructor rule.

```csharp
using System.Windows;
using System.Windows.Controls;
using ScreenBugs.Settings;

namespace ScreenBugs.Options;

/// <summary>
/// Live-preview options dialog. Every edit is applied to the overlay at once; Cancel restores the
/// snapshot taken when the window opened (spec 8).
/// </summary>
public partial class OptionsWindow : Window
{
    private static readonly int[] FrameRates = [30, 60, 120];

    private readonly BugOptions initial;
    private readonly OptionsApplier applier;
    private readonly bool startupWasEnabled;
    private BugOptions edited;
    private bool previewRespawned;
    private bool suppress;

    public OptionsWindow(BugOptions initial, OptionsApplier applier)
    {
        InitializeComponent();

        this.initial = initial;
        this.applier = applier;
        edited = initial;
        startupWasEnabled = StartupRegistration.IsEnabled();

        suppress = true;
        for (int count = 1; count <= BugTypeSlots.MaxSlots; count++)
        {
            SlotCountBox.Items.Add(count);
        }

        foreach (int rate in FrameRates)
        {
            FrameRateBox.Items.Add($"{rate} fps");
        }

        TypeChangeBox.Items.Add("Respawn all bugs");
        TypeChangeBox.Items.Add("Let existing bugs age out");

        SlotCountBox.SelectedItem = edited.TypeSlots.Count;
        FrameRateBox.SelectedIndex = Math.Max(0, Array.IndexOf(FrameRates, edited.FrameRate));
        TypeChangeBox.SelectedIndex = edited.OnTypeChange == TypeChangeBehavior.RespawnAll ? 0 : 1;
        CountSlider.Value = edited.BugCount;
        CountText.Text = edited.BugCount.ToString();
        StartupBox.IsChecked = startupWasEnabled;
        RebuildSlotRows();
        suppress = false;
    }

    /// <summary>The accepted options, or null if the dialog was cancelled or closed.</summary>
    public BugOptions? Result { get; private set; }

    /// <summary>Reverts a cancelled preview. Runs for OK too, but <see cref="Result"/> is set by then.</summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (Result is not null)
        {
            return;
        }

        // Respawn on revert only if the preview actually replaced the population, or if the
        // original setting would have; otherwise cancelling would churn the screen for nothing.
        var revert = previewRespawned || initial.OnTypeChange == TypeChangeBehavior.RespawnAll
            ? TypeChangeBehavior.RespawnAll
            : TypeChangeBehavior.AgeOut;
        applier.Apply(edited, initial, revert);
    }

    private void RebuildSlotRows()
    {
        SlotPanel.Children.Clear();
        for (int index = 0; index < edited.TypeSlots.Count; index++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock
            {
                Text = $"Type {index + 1}",
                Width = 130,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var box = new ComboBox { Width = 200, DisplayMemberPath = nameof(BugTypeChoice.Label), Tag = index };
            foreach (var choice in BugTypeSlots.AvailableFor(edited.TypeSlots, index))
            {
                box.Items.Add(BugTypeChoice.From(choice));
            }

            box.SelectedItem = box.Items.Cast<BugTypeChoice>().First(item => item.Slot == edited.TypeSlots[index]);
            box.SelectionChanged += OnSlotChanged;
            row.Children.Add(box);
            SlotPanel.Children.Add(row);
        }
    }

    private void OnSlotChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || sender is not ComboBox { Tag: int index, SelectedItem: BugTypeChoice choice })
        {
            return;
        }

        var slots = edited.TypeSlots.ToList();
        slots[index] = choice.Slot;
        UpdateEdited(edited with { TypeSlots = slots });
        RebuildRowsSuppressed();
    }

    private void OnSlotCountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || SlotCountBox.SelectedItem is not int count)
        {
            return;
        }

        UpdateEdited(edited with { TypeSlots = BugTypeSlots.Resize(edited.TypeSlots, count) });
        RebuildRowsSuppressed();
    }

    private void OnCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Slider coercion can fire this before the XAML fields are assigned.
        if (CountText is null)
        {
            return;
        }

        int count = (int)Math.Round(e.NewValue);
        CountText.Text = count.ToString();
        if (!suppress)
        {
            UpdateEdited(edited with { BugCount = count });
        }
    }

    private void OnFrameRateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || FrameRateBox.SelectedIndex < 0)
        {
            return;
        }

        UpdateEdited(edited with { FrameRate = FrameRates[FrameRateBox.SelectedIndex] });
    }

    private void OnTypeChangeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppress || TypeChangeBox.SelectedIndex < 0)
        {
            return;
        }

        var behavior = TypeChangeBox.SelectedIndex == 0 ? TypeChangeBehavior.RespawnAll : TypeChangeBehavior.AgeOut;
        UpdateEdited(edited with { OnTypeChange = behavior });
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        bool wantStartup = StartupBox.IsChecked == true;
        if (wantStartup)
        {
            // Always rewrite: cheap, and it repairs an entry left by an older install path.
            StartupRegistration.SetEnabled(true);
        }
        else if (startupWasEnabled)
        {
            StartupRegistration.SetEnabled(false);
        }

        Result = edited;
        Close();
    }

    private void UpdateEdited(BugOptions next)
    {
        var previous = edited;
        edited = next;
        if (applier.Apply(previous, next, next.OnTypeChange))
        {
            previewRespawned = true;
        }
    }

    private void RebuildRowsSuppressed()
    {
        suppress = true;
        RebuildSlotRows();
        suppress = false;
    }
}
```

- [ ] **Step 3: Build and commit**

Run the app build command. Expected: 0 warnings, 0 errors.

```bash
git add src/ScreenBugs/Options
git commit -m "feat(options): live-preview options dialog with slot rows

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 12: Composition

**Files:**
- Modify: `src/ScreenBugs/App.xaml.cs`

- [ ] **Step 1: Load settings and hold the applier**

In `src/ScreenBugs/App.xaml.cs`, delete the `InitialBugCount` constant and add these fields beside the existing ones:

```csharp
    private BugOptions current = BugOptions.Default;
    private OptionsApplier? applier;
    private OptionsWindow? optionsWindow;
```

Add these usings at the top:

```csharp
using ScreenBugs.Options;
using ScreenBugs.Settings;
```

- [ ] **Step 2: Build from the loaded options**

In `OnStartup`, replace the three lines added in Task 6:

```csharp
        var rng = new SystemRandomSource();
        var speciesSource = new SlotSpeciesSource(rng);
        var simulation = new BugSimulation(bounds, rng, speciesSource) { TargetCount = InitialBugCount };
```

with:

```csharp
        current = SettingsStore.Load();
        var rng = new SystemRandomSource();
        var speciesSource = new SlotSpeciesSource(rng) { Slots = current.TypeSlots };
        var simulation = new BugSimulation(bounds, rng, speciesSource) { TargetCount = current.BugCount };
```

- [ ] **Step 3: Apply the frame rate and expose the applier**

Still in `OnStartup`, replace the click-through line inside the `FrameLoop` lambda:

```csharp
            bool squashable = trayIcon?.IsMenuOpen != true && cursor is { } c && simulation.HitTest(c) is not null;
```

with:

```csharp
            bool squashable = trayIcon?.IsMenuOpen != true
                && optionsWindow is null
                && cursor is { } c
                && simulation.HitTest(c) is not null;
```

Then, immediately after the `frameLoop = new FrameLoop(...)` statement, add:

```csharp
        frameLoop.TargetFrameRate = current.FrameRate;
        applier = new OptionsApplier(simulation, speciesSource, frameLoop);
```

- [ ] **Step 4: Wire the Options menu item**

Replace the tray subscription block with:

```csharp
        trayIcon = new TrayIcon();
        trayIcon.PauseToggled += TogglePause;
        trayIcon.OptionsRequested += ShowOptions;
        trayIcon.ExitRequested += () => Shutdown();
```

- [ ] **Step 5: Add ShowOptions**

Add these two methods to `App`, after `TogglePause`:

```csharp
    /// <summary>
    /// Queued onto the dispatcher so the WinForms context menu finishes closing before a modal
    /// dialog opens on the same thread.
    /// </summary>
    private void ShowOptions() => Dispatcher.BeginInvoke(ShowOptionsDialog);

    private void ShowOptionsDialog()
    {
        if (optionsWindow is not null)
        {
            optionsWindow.Activate();
            return;
        }

        var window = new OptionsWindow(current, applier!);
        optionsWindow = window;
        try
        {
            window.ShowDialog();
            if (window.Result is { } accepted)
            {
                current = accepted;
                SettingsStore.Save(current);
            }
        }
        finally
        {
            optionsWindow = null;
        }
    }
```

- [ ] **Step 6: Build everything and run the suite**

Run the app build, the test build, and the tests.
Expected: 0 warnings, 0 errors, and all tests passing (76 or more).

- [ ] **Step 7: Commit**

```bash
git add src/ScreenBugs/App.xaml.cs
git commit -m "feat(app): load, apply and persist bug options from the tray dialog

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 13: Manual verification

**Files:** none changed unless a check fails.

- [ ] **Step 1: Start from no settings file**

```bash
rm -f "$LOCALAPPDATA/ScreenBugs/settings.json"
dotnet run --project src/ScreenBugs -c Release
```

Expected: five black garden ants, and only black garden ants, walk in.

- [ ] **Step 2: Walk the checklist (spec 11)**

With the app running, confirm each:

1. The tray menu (in the Windows 11 hidden-icons overflow, behind the taskbar chevron) reads Pause, Options..., Exit. Options opens a centred dialog.
2. Set "Number of types" to 3: two more dropdowns appear, preset to Random and Hissing cockroach. Black garden ant is missing from them, and Random is missing from the third.
3. With "When types change" on "Respawn all bugs", change a slot: the population is replaced at once. Switch to "Let existing bugs age out" and change a slot again: the bugs on screen are left alone.
4. Drag "Bugs on screen": bugs are added or removed live.
5. Choose 30 fps: motion is visibly less smooth. Back to 60: smooth again.
6. Press Cancel after several changes: slots, count and frame rate return to what they were when the dialog opened.
7. Reopen, make changes, press OK, then Exit and relaunch: the settings survived, and `%LocalAppData%\ScreenBugs\settings.json` matches what you chose.
8. Tick "Run at Windows startup" and press OK, then check the Run key holds the quoted exe path:

```bash
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v ScreenBugs
```

   Untick it, press OK, and confirm the same command reports the value is missing.
9. Open Options twice from the tray: the existing window comes forward rather than a second one opening.
10. Let a bug crawl over the dialog and click a control underneath it: the control responds and the bug is not squashed.
11. Confirm `%LocalAppData%\ScreenBugs\error.log` did not grow during any of this.

- [ ] **Step 3: Commit any fixes**

If a check failed, fix it, re-run the affected checks, and commit with a message naming the check.

Chunk 3, and the plan, is complete when every checklist item passes and `git status` is clean.

<!-- end of chunk 3 -->
