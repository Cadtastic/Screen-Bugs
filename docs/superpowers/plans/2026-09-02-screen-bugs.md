# Screen Bugs Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows desktop toy where nine species of code-drawn bugs wander a click-through overlay on the primary monitor, flee the cursor, and can be squashed, controlled from a tray icon.

**Architecture:** A pure C# simulation library (`ScreenBugs.Core`) owns all bug behavior and is fully unit-tested. A WPF app (`ScreenBugs`) hosts a transparent, topmost, click-through window that redraws the simulation each frame with one vector painter per species, and a WinForms `NotifyIcon` provides the tray menu. The app depends on Core; nothing depends on the app.

**Tech Stack:** .NET 10, C# 14, WPF, Windows Forms (`NotifyIcon` only), xUnit 2.9, `System.Numerics.Vector2`.

**Spec:** `docs/superpowers/specs/2026-09-02-screen-bugs-design.md`. Section numbers below (for example "spec 5.4") refer to it. Species geometry comes from `docs/superpowers/specs/assets/bug-specimens.svg`.

**Conventions (from the user's global CLAUDE.md, mandatory):**
- Primary constructors; use the parameters directly, no `_field = param` copies and no null checks.
- One type per file, file named for the type.
- Exception allowed only where a constructor has real wiring logic (WPF/WinForms component setup) or a parameter is mutated after construction.

**Commits:** Every commit message ends with the trailer line `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. If `git config user.name` prints nothing, set `user.name` and `user.email` for this repo before the first commit.

**Working directory:** all paths and commands are relative to the repo root `C:\Users\AddamBoord\source\repos\ScreenSavers`. The repo is already a git repository on `main` with the spec committed.

**Verified toolchain facts (checked on this machine):**
- `dotnet new sln` produces a `.slnx` file, so the solution is `ScreenBugs.slnx`.
- `dotnet new xunit -f net10.0` produces xunit 2.9.3 with `Microsoft.NET.Test.Sdk`; `dotnet test` works as usual.
- `dotnet new wpf -n ScreenBugs -o src/ScreenBugs` (no `-f` flag) produces `App.xaml`, `App.xaml.cs`, `AssemblyInfo.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, and a `net10.0-windows` csproj.
- A csproj with both `UseWPF` and `UseWindowsForms` builds cleanly when the WinForms implicit usings are removed with `<Using Remove="System.Drawing" />` and `<Using Remove="System.Windows.Forms" />`.
- The complete Core and test code in Chunks 1 and 2 (the state after Task 9) was compiled and run on this machine: 0 compiler warnings, all 37 tests pass, and the seed-sensitive tests (edge steering, flee sequence, respawn, straggler) also pass for seeds 1 through 40. If a test in those chunks fails for you, compare your file against the plan text before changing the simulation rules.

---

## File structure

```
ScreenBugs.slnx
.gitignore                                    from `dotnet new gitignore`
src/ScreenBugs.Core/                          net10.0 class library (Chunks 1 and 2)
  ScreenBugs.Core.csproj
  Simulation/IRandomSource.cs                 random abstraction for determinism
  Simulation/SystemRandomSource.cs            System.Random adapter
  Simulation/Bounds.cs                        screen rectangle helpers
  Simulation/SpeciesId.cs                     enum of the nine species
  Simulation/BugSpecies.cs                    per-species tuning record
  Simulation/SpeciesCatalog.cs                the nine tuned species
  Simulation/BugState.cs                      Wandering / Pausing / Fleeing / Squashed
  Simulation/Bug.cs                           one bug's mutable state
  Simulation/BugSimulation.cs                 stepping, spawning, fleeing, squashing
tests/ScreenBugs.Tests/                       xUnit (Chunks 1 and 2)
  ScreenBugs.Tests.csproj
  SimulationSteps.cs                          test helpers (create, step for N seconds)
  BoundsTests.cs
  SpeciesCatalogTests.cs
  BugTests.cs
  BugSpawnTests.cs
  BugSimulationTests.cs
  BugSquashTests.cs
  BugFleeTests.cs
src/ScreenBugs/                               WPF app (Chunks 3 to 5)
  ScreenBugs.csproj
  app.manifest                                PerMonitorV2 DPI awareness
  App.xaml, App.xaml.cs                       composition root
  Overlay/NativeMethods.cs                    Win32 P/Invoke wrappers
  Overlay/OverlayWindow.xaml(.cs)             transparent topmost window
  Overlay/BugCanvas.cs                        draws all bugs each frame
  Overlay/FrameLoop.cs                        60 Hz tick from CompositionTarget.Rendering
  Overlay/CursorTracker.cs                    global cursor position in DIPs
  Overlay/ClickThroughController.cs           toggles WS_EX_TRANSPARENT
  Overlay/TopmostKeeper.cs                    re-asserts HWND_TOPMOST every 2 s
  Rendering/IBugPainter.cs                    Paint + BodyColor
  Rendering/BugPainterRegistry.cs             SpeciesId to painter
  Rendering/Shapes.cs                         frozen PathGeometry helpers
  Rendering/PainterPens.cs                    frozen brush/pen helpers with 1 DIP minimum
  Rendering/LegPainter.cs                     two-segment leg with swing, mirrored pairs
  Rendering/BodyMotion.cs                     body bob and antenna waggle helpers
  Rendering/SplatPainter.cs                   fading splat for squashed bugs
  Rendering/Painters/AntGeometry.cs           shared ant drawing (two colors)
  Rendering/Painters/BlackGardenAntPainter.cs
  Rendering/Painters/RedFireAntPainter.cs
  Rendering/Painters/HissingCockroachPainter.cs
  Rendering/Painters/PrayingMantisPainter.cs
  Rendering/Painters/SevenSpotLadybugPainter.cs
  Rendering/Painters/StagBeetlePainter.cs
  Rendering/Painters/HouseSpiderPainter.cs
  Rendering/Painters/CentipedePainter.cs
  Rendering/Painters/StinkBugPainter.cs
  Tray/TrayIcon.cs                            NotifyIcon + context menu + events
  Tray/TrayIconFactory.cs                     draws the 32x32 tray glyph in code
  Diagnostics/CrashLog.cs                     appends to %LocalAppData%\ScreenBugs\error.log
  Diagnostics/SingleInstanceGuard.cs          named mutex
```

Chunks follow the spec's build order (spec 11):

1. Chunk 1: solution scaffold, Core value types, and the simulation skeleton (spawn, hit test, squash).
2. Chunk 2: Core behavior (movement, pausing, fleeing, respawn) with tests. Core is finished here.
3. Chunk 3: WPF overlay, Win32 plumbing, rendering primitives, and one painter, so click-through and CPU cost are verified early.
4. Chunk 4: the remaining eight painters and the splat.
5. Chunk 5: tray icon, diagnostics, and application composition.

---

## Chunk 1: Solution scaffold and Core simulation

### Task 1: Solution, Core project, test project

**Files:**
- Create: `ScreenBugs.slnx`, `.gitignore`
- Create: `src/ScreenBugs.Core/ScreenBugs.Core.csproj`
- Create: `tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj`
- Delete: `src/ScreenBugs.Core/Class1.cs`, `tests/ScreenBugs.Tests/UnitTest1.cs`

- [ ] **Step 1: Scaffold the solution and projects**

Run from the repo root:

```bash
dotnet new gitignore
dotnet new sln -n ScreenBugs
dotnet new classlib -n ScreenBugs.Core -o src/ScreenBugs.Core -f net10.0
dotnet new xunit -n ScreenBugs.Tests -o tests/ScreenBugs.Tests -f net10.0
dotnet sln ScreenBugs.slnx add src/ScreenBugs.Core tests/ScreenBugs.Tests
dotnet add tests/ScreenBugs.Tests reference src/ScreenBugs.Core
```

Expected: each command prints a success line; `ScreenBugs.slnx` exists at the root.

- [ ] **Step 2: Remove template placeholders**

```bash
rm src/ScreenBugs.Core/Class1.cs tests/ScreenBugs.Tests/UnitTest1.cs
```

- [ ] **Step 3: Replace the Core csproj**

Overwrite `src/ScreenBugs.Core/ScreenBugs.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>ScreenBugs.Core</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ScreenBugs.Tests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Add a global using to the test project**

Edit `tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj`: keep everything the template generated (the four `PackageReference` lines, the `ProjectReference`, and `<Using Include="Xunit" />`) and add one more `Using` item inside the existing `<ItemGroup>` that holds the Xunit using. (A global using for `ScreenBugs.Core.Simulation` is added in Task 2, once that namespace exists; adding it now would fail the build with CS0246.)

```xml
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="System.Numerics" />
  </ItemGroup>
```

- [ ] **Step 5: Build to confirm the empty solution compiles**

Run: `dotnet build ScreenBugs.slnx -nologo -v q`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add .gitignore ScreenBugs.slnx src/ScreenBugs.Core tests/ScreenBugs.Tests
git commit -m "chore: scaffold ScreenBugs solution with Core library and test project

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 2: Random source and Bounds

**Files:**
- Create: `src/ScreenBugs.Core/Simulation/IRandomSource.cs`
- Create: `src/ScreenBugs.Core/Simulation/SystemRandomSource.cs`
- Create: `src/ScreenBugs.Core/Simulation/Bounds.cs`
- Test: `tests/ScreenBugs.Tests/BoundsTests.cs`

- [ ] **Step 1: Write the failing Bounds tests**

Create `tests/ScreenBugs.Tests/BoundsTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class BoundsTests
{
    private static readonly Bounds Screen = new(1920, 1080);

    [Fact]
    public void Contains_is_true_inside_and_on_the_edge_and_false_outside()
    {
        Assert.True(Screen.Contains(new Vector2(960, 540)));
        Assert.True(Screen.Contains(new Vector2(0, 0)));
        Assert.True(Screen.Contains(new Vector2(1920, 1080)));
        Assert.False(Screen.Contains(new Vector2(-1, 540)));
        Assert.False(Screen.Contains(new Vector2(960, 1081)));
    }

    [Fact]
    public void Clamp_pulls_points_inside_by_the_inset()
    {
        Assert.Equal(new Vector2(2, 2), Screen.Clamp(new Vector2(-50, -50), 2));
        Assert.Equal(new Vector2(1918, 1078), Screen.Clamp(new Vector2(5000, 5000), 2));
        Assert.Equal(new Vector2(960, 540), Screen.Clamp(new Vector2(960, 540), 2));
    }

    [Fact]
    public void Center_is_the_middle_of_the_screen()
    {
        Assert.Equal(new Vector2(960, 540), Screen.Center);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BoundsTests" -nologo -v q`
Expected: build FAILS with `CS0246: The type or namespace name 'Bounds' could not be found`.

- [ ] **Step 3: Create the random source abstraction and adapter**

Create `src/ScreenBugs.Core/Simulation/IRandomSource.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

/// <summary>Source of randomness for the simulation. Seeded implementations make runs reproducible.</summary>
public interface IRandomSource
{
    /// <summary>Uniform value in [0, 1).</summary>
    float NextFloat();

    /// <summary>Uniform value in [min, max).</summary>
    float NextFloat(float min, float max);

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    int NextInt(int maxExclusive);
}
```

Create `src/ScreenBugs.Core/Simulation/SystemRandomSource.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

public sealed class SystemRandomSource(int? seed = null) : IRandomSource
{
    private readonly Random random = seed is null ? new Random() : new Random(seed.Value);

    public float NextFloat() => random.NextSingle();

    public float NextFloat(float min, float max) => min + (max - min) * random.NextSingle();

    public int NextInt(int maxExclusive) => random.Next(maxExclusive);
}
```

- [ ] **Step 4: Create Bounds and the Simulation global using**

Now that the `ScreenBugs.Core.Simulation` namespace exists, add its global using to `tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj`, in the same `<ItemGroup>` as the other two:

```xml
    <Using Include="ScreenBugs.Core.Simulation" />
```

Create `src/ScreenBugs.Core/Simulation/Bounds.cs`:

```csharp
using System.Numerics;

namespace ScreenBugs.Core.Simulation;

/// <summary>The screen rectangle in DIPs, origin top-left, Y down.</summary>
public readonly record struct Bounds(float Width, float Height)
{
    public Vector2 Center => new(Width / 2f, Height / 2f);

    public bool Contains(Vector2 point) =>
        point.X >= 0f && point.Y >= 0f && point.X <= Width && point.Y <= Height;

    public Vector2 Clamp(Vector2 point, float inset) =>
        new(Math.Clamp(point.X, inset, Width - inset), Math.Clamp(point.Y, inset, Height - inset));
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BoundsTests" -nologo -v q`
Expected: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation tests/ScreenBugs.Tests/BoundsTests.cs tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj
git commit -m "feat(core): add IRandomSource, SystemRandomSource and Bounds

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 3: Species catalog

**Files:**
- Create: `src/ScreenBugs.Core/Simulation/SpeciesId.cs`
- Create: `src/ScreenBugs.Core/Simulation/BugSpecies.cs`
- Create: `src/ScreenBugs.Core/Simulation/SpeciesCatalog.cs`
- Test: `tests/ScreenBugs.Tests/SpeciesCatalogTests.cs`

- [ ] **Step 1: Write the failing catalog tests**

Create `tests/ScreenBugs.Tests/SpeciesCatalogTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class SpeciesCatalogTests
{
    [Fact]
    public void Catalog_has_nine_distinct_species()
    {
        Assert.Equal(9, SpeciesCatalog.All.Count);
        Assert.Equal(9, SpeciesCatalog.All.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Get_returns_the_species_with_that_id()
    {
        foreach (var id in Enum.GetValues<SpeciesId>())
        {
            Assert.Equal(id, SpeciesCatalog.Get(id).Id);
        }
    }

    [Fact]
    public void Every_species_has_sane_positive_tuning()
    {
        foreach (var s in SpeciesCatalog.All)
        {
            Assert.True(s.BodyLength > 0, s.Id.ToString());
            Assert.True(s.HitRadius > 0, s.Id.ToString());
            Assert.True(s.WalkSpeed > 0, s.Id.ToString());
            Assert.True(s.FleeSpeed > s.WalkSpeed, s.Id.ToString());
            Assert.True(s.TurnRate > 0, s.Id.ToString());
            Assert.True(s.FleeRadius > 0, s.Id.ToString());
            Assert.True(s.ReactionDelayMin > 0, s.Id.ToString());
            Assert.True(s.ReactionDelayMax >= s.ReactionDelayMin, s.Id.ToString());
            Assert.True(s.PauseChancePerSecond > 0, s.Id.ToString());
            Assert.True(s.PauseMin > 0, s.Id.ToString());
            Assert.True(s.PauseMax >= s.PauseMin, s.Id.ToString());
            Assert.Equal(0.6f * s.BodyLength, s.StrideLength);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~SpeciesCatalogTests" -nologo -v q`
Expected: build FAILS with `CS0103: The name 'SpeciesCatalog' does not exist`.

- [ ] **Step 3: Create SpeciesId and BugSpecies**

Create `src/ScreenBugs.Core/Simulation/SpeciesId.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

public enum SpeciesId
{
    HissingCockroach,
    BlackGardenAnt,
    RedFireAnt,
    PrayingMantis,
    SevenSpotLadybug,
    StagBeetle,
    HouseSpider,
    Centipede,
    StinkBug,
}
```

Create `src/ScreenBugs.Core/Simulation/BugSpecies.cs` (all lengths in DIPs, speeds in DIPs per second, turn rate in radians per second, times in seconds; see spec 5.1):

```csharp
namespace ScreenBugs.Core.Simulation;

public sealed record BugSpecies(
    SpeciesId Id,
    float BodyLength,
    float HitRadius,
    float WalkSpeed,
    float FleeSpeed,
    float TurnRate,
    float FleeRadius,
    float ReactionDelayMin,
    float ReactionDelayMax,
    float PauseChancePerSecond,
    float PauseMin,
    float PauseMax)
{
    /// <summary>DIPs traveled per full leg cycle while walking (spec 5.1).</summary>
    public float StrideLength => 0.6f * BodyLength;
}
```

- [ ] **Step 4: Create SpeciesCatalog with the spec's tuning table**

Create `src/ScreenBugs.Core/Simulation/SpeciesCatalog.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

/// <summary>The nine species and their tuning (spec 5.1 table).</summary>
public static class SpeciesCatalog
{
    public static IReadOnlyList<BugSpecies> All { get; } =
    [
        //             Id,                          Body, Hit, Walk, Flee, Turn, FleeR, ReactMin, ReactMax, Pause/s, PauseMin, PauseMax
        new(SpeciesId.HissingCockroach,  44f, 26f, 110f, 330f, 5.0f, 180f, 0.10f, 0.25f, 0.20f, 0.5f, 2.0f),
        new(SpeciesId.BlackGardenAnt,    16f, 14f,  70f, 175f, 6.0f, 120f, 0.10f, 0.25f, 0.50f, 0.3f, 1.2f),
        new(SpeciesId.RedFireAnt,        15f, 14f,  80f, 200f, 6.0f, 120f, 0.10f, 0.25f, 0.50f, 0.3f, 1.2f),
        new(SpeciesId.PrayingMantis,     56f, 24f,  25f,  50f, 2.0f,  90f, 0.20f, 0.40f, 0.80f, 1.0f, 4.0f),
        new(SpeciesId.SevenSpotLadybug,  22f, 16f,  40f,  80f, 3.0f, 100f, 0.10f, 0.25f, 0.30f, 0.5f, 2.0f),
        new(SpeciesId.StagBeetle,        40f, 22f,  30f,  55f, 2.0f,  90f, 0.10f, 0.25f, 0.30f, 0.5f, 2.0f),
        new(SpeciesId.HouseSpider,       34f, 24f,  90f, 270f, 8.0f, 150f, 0.05f, 0.15f, 1.00f, 0.8f, 3.0f),
        new(SpeciesId.Centipede,         50f, 22f,  60f, 150f, 3.0f, 130f, 0.10f, 0.25f, 0.15f, 0.5f, 2.0f),
        new(SpeciesId.StinkBug,          28f, 18f,  35f,  70f, 2.5f, 100f, 0.10f, 0.25f, 0.40f, 0.5f, 2.0f),
    ];

    public static BugSpecies Get(SpeciesId id) => All.First(s => s.Id == id);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~SpeciesCatalogTests" -nologo -v q`
Expected: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation tests/ScreenBugs.Tests/SpeciesCatalogTests.cs
git commit -m "feat(core): add SpeciesId, BugSpecies and the nine-species catalog

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 4: BugState and Bug

**Files:**
- Create: `src/ScreenBugs.Core/Simulation/BugState.cs`
- Create: `src/ScreenBugs.Core/Simulation/Bug.cs`
- Test: `tests/ScreenBugs.Tests/BugTests.cs`

- [ ] **Step 1: Write the failing Bug tests**

Create `tests/ScreenBugs.Tests/BugTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class BugTests
{
    private static readonly BugSpecies Ant = SpeciesCatalog.Get(SpeciesId.BlackGardenAnt);

    [Fact]
    public void New_bug_is_wandering_and_alive()
    {
        var bug = new Bug(1, Ant, seed: 42);

        Assert.Equal(BugState.Wandering, bug.State);
        Assert.True(bug.IsAlive);
        Assert.Equal(1, bug.Id);
        Assert.Same(Ant, bug.Species);
    }

    [Fact]
    public void HitTest_uses_the_species_hit_radius()
    {
        var bug = new Bug(1, Ant, seed: 42) { Position = new Vector2(100, 100) };

        Assert.True(bug.HitTest(new Vector2(100, 100)));
        Assert.True(bug.HitTest(new Vector2(100 + Ant.HitRadius, 100)));
        Assert.False(bug.HitTest(new Vector2(100 + Ant.HitRadius + 0.5f, 100)));
    }

    [Fact]
    public void Squashed_bug_is_not_alive_and_never_hit()
    {
        var bug = new Bug(1, Ant, seed: 42) { Position = new Vector2(100, 100), State = BugState.Squashed };

        Assert.False(bug.IsAlive);
        Assert.False(bug.HitTest(new Vector2(100, 100)));
    }

    [Fact]
    public void SpeedFactor_is_in_range_and_determined_by_seed()
    {
        var a = new Bug(1, Ant, seed: 7);
        var b = new Bug(2, Ant, seed: 7);
        var c = new Bug(3, Ant, seed: 8);

        Assert.InRange(a.SpeedFactor, 0.85f, 1.15f);
        Assert.Equal(a.SpeedFactor, b.SpeedFactor);
        Assert.NotEqual(a.SpeedFactor, c.SpeedFactor);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugTests" -nologo -v q`
Expected: build FAILS with `CS0246: The type or namespace name 'Bug' could not be found`.

- [ ] **Step 3: Create BugState and Bug**

Create `src/ScreenBugs.Core/Simulation/BugState.cs`:

```csharp
namespace ScreenBugs.Core.Simulation;

public enum BugState
{
    Wandering,
    Pausing,
    Fleeing,
    Squashed,
}
```

Create `src/ScreenBugs.Core/Simulation/Bug.cs`. Setters are `internal` so the simulation and the test project (via `InternalsVisibleTo`) can set state directly (spec 5.1). `ReactionTimer` is `float?`: `null` means the cursor is not close, a value is the seconds left before the bug reacts.

```csharp
using System.Numerics;

namespace ScreenBugs.Core.Simulation;

/// <summary>One bug's mutable state. Owned and stepped by <see cref="BugSimulation"/>.</summary>
public sealed class Bug(int id, BugSpecies species, int seed)
{
    public int Id => id;

    public BugSpecies Species => species;

    /// <summary>Stable per-bug seed for visual variation and the splat shape. An initialized property (not `=> seed`) because `seed` is also used in the <see cref="SpeedFactor"/> initializer; capturing it as well would trigger CS9124.</summary>
    public int Seed { get; } = seed;

    /// <summary>Multiplies walk and flee speed; in [0.85, 1.15] and fixed by <see cref="Seed"/>.</summary>
    public float SpeedFactor { get; } = 0.85f + 0.30f * new Random(seed).NextSingle();

    public Vector2 Position { get; internal set; }

    /// <summary>Radians; 0 points right (+X), positive turns clockwise on screen (Y is down).</summary>
    public float Heading { get; internal set; }

    public float TargetHeading { get; internal set; }

    /// <summary>Current speed in DIPs per second.</summary>
    public float Speed { get; internal set; }

    public BugState State { get; internal set; } = BugState.Wandering;

    /// <summary>Seconds spent in the current state.</summary>
    public float StateTime { get; internal set; }

    /// <summary>Leg cycle position in [0, 1); advances with distance traveled.</summary>
    public float LegPhase { get; internal set; }

    /// <summary>Seconds until the bug reacts to a nearby cursor; null when the cursor is not close.</summary>
    public float? ReactionTimer { get; internal set; }

    /// <summary>Seconds the cursor has been far away while fleeing.</summary>
    public float FleeSafeTime { get; internal set; }

    /// <summary>0 to 1 while squashed; the bug is removed at 1.</summary>
    public float SquashProgress { get; internal set; }

    /// <summary>Seconds until the next wander retarget.</summary>
    public float RetargetTimer { get; internal set; }

    /// <summary>Length of the current pause in seconds.</summary>
    public float PauseDuration { get; internal set; }

    /// <summary>Radians added to the flee direction; redrawn every 0.3 s.</summary>
    public float FleeJitter { get; internal set; }

    public float FleeJitterTimer { get; internal set; }

    /// <summary>Seconds since spawn.</summary>
    public float Age { get; internal set; }

    /// <summary>True once the bug has been inside the screen; enables the edge clamp.</summary>
    public bool HasEnteredScreen { get; internal set; }

    public bool IsAlive => State != BugState.Squashed;

    public bool HitTest(Vector2 point) =>
        IsAlive && Vector2.Distance(point, Position) <= species.HitRadius;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugTests" -nologo -v q`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs.Core/Simulation tests/ScreenBugs.Tests/BugTests.cs
git commit -m "feat(core): add BugState and Bug

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 5: BugSimulation skeleton: spawning, hit testing, squashing

This task creates `BugSimulation` with spawning from the edges, `AddBug`, `HitTest`, `TrySquashAt`, and a `Step` that only advances timers and fades squashed bugs. Movement, pausing, fleeing, and respawn are added in Chunk 2 (Tasks 6 to 9).

**Files:**
- Create: `src/ScreenBugs.Core/Simulation/BugSimulation.cs`
- Create: `tests/ScreenBugs.Tests/SimulationSteps.cs`
- Test: `tests/ScreenBugs.Tests/BugSpawnTests.cs`
- Test: `tests/ScreenBugs.Tests/BugSquashTests.cs`

- [ ] **Step 1: Write the test helper**

Create `tests/ScreenBugs.Tests/SimulationSteps.cs`. `Walker` is an ant that never pauses on its own, so movement tests are deterministic.

```csharp
namespace ScreenBugs.Tests;

internal static class SimulationSteps
{
    public const float Dt = 1f / 60f;

    public static readonly Bounds Screen = new(1920, 1080);

    /// <summary>A black garden ant that never pauses by chance, for deterministic movement tests.</summary>
    public static readonly BugSpecies Walker =
        SpeciesCatalog.Get(SpeciesId.BlackGardenAnt) with { PauseChancePerSecond = 0f };

    public static BugSimulation Create(int count, int seed = 1234) =>
        new(Screen, new SystemRandomSource(seed)) { TargetCount = count };

    /// <summary>Steps the simulation at 60 Hz for at least <paramref name="seconds"/>.</summary>
    public static void StepFor(BugSimulation sim, float seconds, Vector2? cursor = null)
    {
        int steps = (int)MathF.Ceiling(seconds / Dt);
        for (int i = 0; i < steps; i++)
        {
            sim.Step(Dt, cursor);
        }
    }

    public static Vector2 Direction(float heading) => new(MathF.Cos(heading), MathF.Sin(heading));

    public static int AliveCount(BugSimulation sim) => sim.Bugs.Count(b => b.IsAlive);
}
```

- [ ] **Step 2: Write the failing spawn and squash tests**

Create `tests/ScreenBugs.Tests/BugSpawnTests.cs` (more tests are added to this file in Task 9):

```csharp
namespace ScreenBugs.Tests;

public sealed class BugSpawnTests
{
    [Fact]
    public void Setting_TargetCount_spawns_the_requested_number_of_bugs()
    {
        var sim = SimulationSteps.Create(5);

        Assert.Equal(5, sim.TargetCount);
        Assert.Equal(5, sim.Bugs.Count);
        Assert.Equal(5, SimulationSteps.AliveCount(sim));
        Assert.Equal(5, sim.Bugs.Select(b => b.Id).Distinct().Count());
    }

    [Fact]
    public void Spawned_bugs_start_outside_the_screen_heading_inward()
    {
        var sim = SimulationSteps.Create(20);

        foreach (var bug in sim.Bugs)
        {
            Assert.False(SimulationSteps.Screen.Contains(bug.Position));
            Assert.False(bug.HasEnteredScreen);
            var toCenter = SimulationSteps.Screen.Center - bug.Position;
            Assert.True(Vector2.Dot(SimulationSteps.Direction(bug.Heading), toCenter) > 0f);
        }
    }

    [Fact]
    public void Spawned_bugs_are_placed_one_body_length_outside_an_edge()
    {
        var sim = SimulationSteps.Create(20);

        foreach (var bug in sim.Bugs)
        {
            float off = bug.Species.BodyLength;
            bool onLeft = bug.Position.X == -off;
            bool onRight = bug.Position.X == SimulationSteps.Screen.Width + off;
            bool onTop = bug.Position.Y == -off;
            bool onBottom = bug.Position.Y == SimulationSteps.Screen.Height + off;
            Assert.True(onLeft || onRight || onTop || onBottom, $"bug {bug.Id} at {bug.Position}");
        }
    }

    [Fact]
    public void AddBug_places_a_wandering_bug_exactly_where_asked()
    {
        var sim = SimulationSteps.Create(0);

        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(300, 400), 1.5f);

        Assert.Single(sim.Bugs);
        Assert.Equal(new Vector2(300, 400), bug.Position);
        Assert.Equal(1.5f, bug.Heading);
        Assert.Equal(BugState.Wandering, bug.State);
        Assert.True(bug.HasEnteredScreen);
    }

    [Fact]
    public void HitTest_returns_the_nearest_overlapping_bug_or_null()
    {
        var sim = SimulationSteps.Create(0);
        var a = sim.AddBug(SimulationSteps.Walker, new Vector2(500, 500), 0f);
        var b = sim.AddBug(SimulationSteps.Walker, new Vector2(510, 500), 0f);

        Assert.Same(a, sim.HitTest(new Vector2(503, 500)));
        Assert.Same(b, sim.HitTest(new Vector2(507, 500)));
        Assert.Null(sim.HitTest(new Vector2(600, 600)));
    }
}
```

Create `tests/ScreenBugs.Tests/BugSquashTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class BugSquashTests
{
    [Fact]
    public void TrySquashAt_on_a_bug_squashes_it_and_returns_true()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        Assert.True(sim.TrySquashAt(new Vector2(965, 540)));
        Assert.Equal(BugState.Squashed, bug.State);
        Assert.False(bug.IsAlive);
        Assert.Equal(0f, bug.SquashProgress);
    }

    [Fact]
    public void TrySquashAt_on_empty_space_returns_false()
    {
        var sim = SimulationSteps.Create(0);
        sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        Assert.False(sim.TrySquashAt(new Vector2(100, 100)));
        Assert.Equal(1, SimulationSteps.AliveCount(sim));
    }

    [Fact]
    public void Squashed_bug_fades_and_is_removed_within_two_seconds()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);
        sim.TrySquashAt(bug.Position);

        SimulationSteps.StepFor(sim, 1f);
        Assert.Contains(bug, sim.Bugs);
        Assert.InRange(bug.SquashProgress, 0.6f, 0.7f);

        SimulationSteps.StepFor(sim, 1f);
        Assert.DoesNotContain(bug, sim.Bugs);
    }

    [Fact]
    public void Squashed_bug_cannot_be_squashed_again()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);
        sim.TrySquashAt(bug.Position);

        Assert.False(sim.TrySquashAt(bug.Position));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugSpawnTests|FullyQualifiedName~BugSquashTests" -nologo -v q`
Expected: build FAILS with `CS0246: The type or namespace name 'BugSimulation' could not be found`.

- [ ] **Step 4: Create BugSimulation**

Create `src/ScreenBugs.Core/Simulation/BugSimulation.cs`. The class has a primary constructor and no constructor body: the caller sets `TargetCount` (normally in an object initializer) and the setter spawns the initial population, which is the same code path the tray menu uses later (spec 5.6). Keeping `bounds` and `rng` out of field initializers avoids compiler warning CS9124 (parameter both captured and used to initialize a field). `UpdateState` and `Move` are stubs that later tasks fill in.

```csharp
using System.Numerics;

namespace ScreenBugs.Core.Simulation;

/// <summary>Owns the bugs and steps their behavior (spec section 5). Pure C#; no UI dependencies.</summary>
public sealed class BugSimulation(Bounds bounds, IRandomSource rng)
{
    private const float MaxDt = 0.1f;
    private const float SquashDuration = 1.5f;

    private readonly List<Bug> bugs = [];
    private int nextId;
    private int targetCount;

    public IReadOnlyList<Bug> Bugs => bugs;

    /// <summary>How many alive bugs the simulation maintains (spec 5.6). Setting it spawns or removes bugs immediately.</summary>
    public int TargetCount
    {
        get => targetCount;
        set
        {
            targetCount = value;
            while (AliveCount < targetCount)
            {
                SpawnFromEdge();
            }

            for (int i = bugs.Count - 1; i >= 0 && AliveCount > targetCount; i--)
            {
                if (bugs[i].IsAlive)
                {
                    bugs.RemoveAt(i);
                }
            }
        }
    }

    private int AliveCount => bugs.Count(b => b.IsAlive);

    /// <summary>Places a wandering bug exactly where asked. Exists for tests; the app never calls it (spec 5.1).</summary>
    public Bug AddBug(BugSpecies species, Vector2 position, float heading)
    {
        var bug = new Bug(nextId++, species, rng.NextInt(int.MaxValue))
        {
            Position = position,
            Heading = heading,
            TargetHeading = heading,
            HasEnteredScreen = bounds.Contains(position),
            RetargetTimer = rng.NextFloat(1f, 4f),
        };
        bug.Speed = species.WalkSpeed * bug.SpeedFactor;
        bugs.Add(bug);
        return bug;
    }

    /// <summary>The nearest alive bug whose hit disc contains <paramref name="point"/>, or null.</summary>
    public Bug? HitTest(Vector2 point)
    {
        Bug? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var bug in bugs)
        {
            if (!bug.HitTest(point))
            {
                continue;
            }

            float distance = Vector2.Distance(bug.Position, point);
            if (distance < nearestDistance)
            {
                nearest = bug;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public bool TrySquashAt(Vector2 point)
    {
        var bug = HitTest(point);
        if (bug is null)
        {
            return false;
        }

        EnterState(bug, BugState.Squashed);
        return true;
    }

    /// <summary>Advances the world by <paramref name="dt"/> seconds (clamped to 0.1). <paramref name="cursor"/> is null when unknown.</summary>
    public void Step(float dt, Vector2? cursor)
    {
        dt = MathF.Min(dt, MaxDt);
        foreach (var bug in bugs)
        {
            AdvanceTimers(bug, dt);
            UpdateState(bug, dt, cursor);
            Move(bug, dt, cursor);
        }

        bugs.RemoveAll(b => b.SquashProgress >= 1f);
    }

    private static void AdvanceTimers(Bug bug, float dt)
    {
        bug.Age += dt;
        bug.StateTime += dt;
        bug.RetargetTimer -= dt;
        bug.FleeJitterTimer -= dt;
        if (bug.ReactionTimer is { } remaining)
        {
            bug.ReactionTimer = remaining - dt;
        }
    }

    private void UpdateState(Bug bug, float dt, Vector2? cursor)
    {
        if (bug.State == BugState.Squashed)
        {
            bug.Speed = 0f;
            bug.SquashProgress += dt / SquashDuration;
        }
    }

    private void Move(Bug bug, float dt, Vector2? cursor)
    {
    }

    private void EnterState(Bug bug, BugState state)
    {
        bug.State = state;
        bug.StateTime = 0f;
        if (state == BugState.Squashed)
        {
            bug.Speed = 0f;
            bug.SquashProgress = 0f;
        }
    }

    /// <summary>Adds a random species one body length outside a random edge, heading inward ±30° (spec 5.5).</summary>
    private void SpawnFromEdge()
    {
        var species = SpeciesCatalog.All[rng.NextInt(SpeciesCatalog.All.Count)];
        var bug = new Bug(nextId++, species, rng.NextInt(int.MaxValue));

        float off = species.BodyLength;
        float along = rng.NextFloat();
        int edge = rng.NextInt(4);
        Vector2 position;
        float inwardHeading;
        switch (edge)
        {
            case 0:
                position = new Vector2(-off, along * bounds.Height);
                inwardHeading = 0f;
                break;
            case 1:
                position = new Vector2(along * bounds.Width, -off);
                inwardHeading = MathF.PI / 2f;
                break;
            case 2:
                position = new Vector2(bounds.Width + off, along * bounds.Height);
                inwardHeading = MathF.PI;
                break;
            default:
                position = new Vector2(along * bounds.Width, bounds.Height + off);
                inwardHeading = -MathF.PI / 2f;
                break;
        }

        bug.Position = position;
        bug.Heading = inwardHeading + rng.NextFloat(-MathF.PI / 6f, MathF.PI / 6f);
        bug.TargetHeading = bug.Heading;
        bug.RetargetTimer = rng.NextFloat(1f, 4f);
        bug.Speed = species.WalkSpeed * bug.SpeedFactor;
        bugs.Add(bug);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugSpawnTests|FullyQualifiedName~BugSquashTests" -nologo -v q`
Expected: `Passed! - Failed: 0, Passed: 9` with no compiler warnings.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/BugSimulation.cs tests/ScreenBugs.Tests
git commit -m "feat(core): add BugSimulation with edge spawning, hit testing and squashing

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

Chunk 1 is complete when `dotnet test tests/ScreenBugs.Tests` passes (19 tests) and `git log --oneline` shows five new commits on top of the spec commits.

<!-- end of chunk 1 -->

## Chunk 2: Core behavior: movement, pausing, fleeing, respawn

All tasks in this chunk modify `src/ScreenBugs.Core/Simulation/BugSimulation.cs` incrementally. Each "replace X with" refers to the version of X left by the previous task.

### Task 6: Movement, wandering and edge steering

**Files:**
- Modify: `src/ScreenBugs.Core/Simulation/BugSimulation.cs`
- Test: `tests/ScreenBugs.Tests/BugSimulationTests.cs`

- [ ] **Step 1: Write the failing movement tests**

Create `tests/ScreenBugs.Tests/BugSimulationTests.cs` (Task 7 adds pause tests to this file):

```csharp
namespace ScreenBugs.Tests;

public sealed class BugSimulationTests
{
    [Fact]
    public void Walking_bug_moves_along_its_heading_and_advances_its_legs()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        SimulationSteps.StepFor(sim, 0.5f);

        Assert.True(bug.Position.X > 960 + 20, $"moved to {bug.Position}");
        Assert.InRange(bug.Position.Y, 530f, 550f);
        Assert.NotEqual(0f, bug.LegPhase);
    }

    [Fact]
    public void Step_clamps_dt_to_a_tenth_of_a_second()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(960, 540), 0f);

        sim.Step(5f, null);

        float maxTravel = SimulationSteps.Walker.WalkSpeed * 1.15f * 0.1f;
        Assert.True(Vector2.Distance(bug.Position, new Vector2(960, 540)) <= maxTravel + 0.01f);
    }

    [Fact]
    public void Bug_entering_from_outside_is_flagged_and_then_kept_inside()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(-10, 540), 0f);
        Assert.False(bug.HasEnteredScreen);

        SimulationSteps.StepFor(sim, 1f);

        Assert.True(bug.HasEnteredScreen);
        Assert.True(bug.Position.X >= 2f);
    }

    [Fact]
    public void Bugs_that_entered_the_screen_stay_inside_the_inset_bounds()
    {
        var sim = SimulationSteps.Create(10);

        for (int i = 0; i < 20_000; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            foreach (var bug in sim.Bugs)
            {
                if (!bug.IsAlive || !bug.HasEnteredScreen)
                {
                    continue;
                }

                Assert.InRange(bug.Position.X, 2f, SimulationSteps.Screen.Width - 2f);
                Assert.InRange(bug.Position.Y, 2f, SimulationSteps.Screen.Height - 2f);
            }
        }
    }

    [Fact]
    public void Bug_heading_at_an_edge_is_steered_back_toward_the_screen()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(30, 540), MathF.PI);

        for (int i = 0; i < 120; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            Assert.True(bug.Position.X >= 2f, $"left the screen at step {i}: {bug.Position}");
        }

        Assert.True(bug.Position.X > 30f, $"did not move back in from the edge: {bug.Position}");
        Assert.True(MathF.Cos(bug.TargetHeading) > 0f, $"wander target {bug.TargetHeading} still points off screen");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugSimulationTests" -nologo -v q`
Expected: FAIL. `Walking_bug_moves...` fails because the bug did not move (`Move` is empty).

- [ ] **Step 3: Add the movement constants**

In `BugSimulation.cs`, replace the two existing constants with this block:

```csharp
    private const float MaxDt = 0.1f;
    private const float SquashDuration = 1.5f;
    private const float EdgeMargin = 60f;
    private const float EdgeInset = 2f;
    private const float EdgeSteerWeight = 2f;
    private const float HeadingNoise = 0.3f;
    private const float FleeTurnMultiplier = 2f;
    private const float FleeStrideMultiplier = 2f;
```

- [ ] **Step 4: Implement wandering and movement**

In `BugSimulation.cs`, replace the `UpdateState` and `Move` stubs with:

```csharp
    private void UpdateState(Bug bug, float dt, Vector2? cursor)
    {
        switch (bug.State)
        {
            case BugState.Squashed:
                bug.Speed = 0f;
                bug.SquashProgress += dt / SquashDuration;
                break;
            case BugState.Wandering:
                UpdateWandering(bug, dt);
                break;
        }
    }

    private void UpdateWandering(Bug bug, float dt)
    {
        if (bug.RetargetTimer <= 0f)
        {
            PickNewTarget(bug);
        }

        bug.Heading += rng.NextFloat(-HeadingNoise, HeadingNoise) * dt;
        bug.Speed = bug.Species.WalkSpeed * bug.SpeedFactor;
    }

    /// <summary>Wander retarget (spec 5.3): new target within ±90° of the current heading, 1 to 4 s until the next one.</summary>
    private void PickNewTarget(Bug bug)
    {
        bug.TargetHeading = bug.Heading + rng.NextFloat(-MathF.PI / 2f, MathF.PI / 2f);
        bug.RetargetTimer = rng.NextFloat(1f, 4f);
    }

    /// <summary>Turning, translation, edge clamp and leg phase (spec 5.4). Pausing and squashed bugs do not turn.</summary>
    private void Move(Bug bug, float dt, Vector2? cursor)
    {
        if (bug.State is BugState.Wandering or BugState.Fleeing)
        {
            Vector2 repulsion = EdgeRepulsion(bug.Position);
            Vector2 steer = DesiredDirection(bug, cursor) + EdgeSteerWeight * repulsion;
            if (steer.LengthSquared() > 1e-6f)
            {
                float target = MathF.Atan2(steer.Y, steer.X);
                if (bug.State == BugState.Wandering && Vector2.Dot(Direction(bug.TargetHeading), repulsion) < 0f)
                {
                    // The wander target points into an edge that is pushing back: adopt the steered
                    // direction so the bug commits to turning away instead of oscillating at the edge.
                    bug.TargetHeading = target;
                }

                float turnRate = bug.State == BugState.Fleeing
                    ? FleeTurnMultiplier * bug.Species.TurnRate
                    : bug.Species.TurnRate;
                bug.Heading = TurnToward(bug.Heading, target, turnRate * dt);
            }
        }

        Vector2 before = bug.Position;
        bug.Position += Direction(bug.Heading) * bug.Speed * dt;

        if (!bug.HasEnteredScreen && bounds.Contains(bug.Position))
        {
            bug.HasEnteredScreen = true;
        }

        if (bug.HasEnteredScreen)
        {
            Vector2 clamped = bounds.Clamp(bug.Position, EdgeInset);
            if (clamped != bug.Position)
            {
                bug.Position = clamped;
                Vector2 toCenter = bounds.Center - bug.Position;
                bug.TargetHeading = MathF.Atan2(toCenter.Y, toCenter.X);
            }
        }

        float stride = bug.State == BugState.Fleeing
            ? FleeStrideMultiplier * bug.Species.StrideLength
            : bug.Species.StrideLength;
        bug.LegPhase = (bug.LegPhase + Vector2.Distance(before, bug.Position) / stride) % 1f;
    }

    /// <summary>Where the bug wants to go before edge steering. Fleeing is added in Task 8.</summary>
    private Vector2 DesiredDirection(Bug bug, Vector2? cursor) => Direction(bug.TargetHeading);

    /// <summary>Edge repulsion (spec 5.4): signed distance to each edge; anything closer than the margin, including negative (outside), pushes inward.</summary>
    private Vector2 EdgeRepulsion(Vector2 position)
    {
        Vector2 repulsion = Vector2.Zero;
        float left = position.X;
        float right = bounds.Width - position.X;
        float top = position.Y;
        float bottom = bounds.Height - position.Y;

        if (left < EdgeMargin)
        {
            repulsion += new Vector2(1f - left / EdgeMargin, 0f);
        }

        if (right < EdgeMargin)
        {
            repulsion += new Vector2(-(1f - right / EdgeMargin), 0f);
        }

        if (top < EdgeMargin)
        {
            repulsion += new Vector2(0f, 1f - top / EdgeMargin);
        }

        if (bottom < EdgeMargin)
        {
            repulsion += new Vector2(0f, -(1f - bottom / EdgeMargin));
        }

        return repulsion;
    }

    private static Vector2 Direction(float heading) => new(MathF.Cos(heading), MathF.Sin(heading));

    /// <summary>Rotates <paramref name="heading"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>, the short way around.</summary>
    private static float TurnToward(float heading, float target, float maxDelta)
    {
        float diff = WrapAngle(target - heading);
        return heading + Math.Clamp(diff, -maxDelta, maxDelta);
    }

    /// <summary>Wraps an angle into (-π, π].</summary>
    private static float WrapAngle(float angle)
    {
        angle %= MathF.Tau;
        if (angle > MathF.PI)
        {
            angle -= MathF.Tau;
        }
        else if (angle <= -MathF.PI)
        {
            angle += MathF.Tau;
        }

        return angle;
    }
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test tests/ScreenBugs.Tests -nologo -v q`
Expected: `Passed!` with 0 failures (the in-bounds test takes a second or two).

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/BugSimulation.cs tests/ScreenBugs.Tests/BugSimulationTests.cs
git commit -m "feat(core): wandering, edge steering, clamping and leg phase

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 7: Pausing

**Files:**
- Modify: `src/ScreenBugs.Core/Simulation/BugSimulation.cs`
- Modify: `tests/ScreenBugs.Tests/BugSimulationTests.cs`

- [ ] **Step 1: Add the failing pause tests**

Append inside the `BugSimulationTests` class:

```csharp
    [Fact]
    public void Pausing_bug_is_completely_still_and_its_legs_do_not_move()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(500, 500), 0.4f);
        SimulationSteps.StepFor(sim, 0.2f);
        bug.State = BugState.Pausing;
        bug.StateTime = 0f;
        bug.PauseDuration = 5f;
        var position = bug.Position;
        float heading = bug.Heading;
        float legPhase = bug.LegPhase;

        SimulationSteps.StepFor(sim, 1f);

        Assert.Equal(BugState.Pausing, bug.State);
        Assert.Equal(position, bug.Position);
        Assert.Equal(heading, bug.Heading);
        Assert.Equal(legPhase, bug.LegPhase);
        Assert.Equal(0f, bug.Speed);
    }

    [Fact]
    public void Pausing_bug_returns_to_wandering_when_its_pause_ends()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, new Vector2(500, 500), 0f);
        bug.State = BugState.Pausing;
        bug.StateTime = 0f;
        bug.PauseDuration = 0.5f;

        SimulationSteps.StepFor(sim, 0.4f);
        Assert.Equal(BugState.Pausing, bug.State);

        SimulationSteps.StepFor(sim, 0.2f);
        Assert.Equal(BugState.Wandering, bug.State);
    }

    [Fact]
    public void Wandering_bug_with_pause_chance_eventually_pauses()
    {
        var sim = SimulationSteps.Create(0);
        var ant = SpeciesCatalog.Get(SpeciesId.BlackGardenAnt);
        var bug = sim.AddBug(ant, new Vector2(960, 540), 0f);

        bool paused = false;
        for (int i = 0; i < 60 * 30 && !paused; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            paused = bug.State == BugState.Pausing;
        }

        Assert.True(paused, "ant never paused in 30 s at 0.5 pauses per second");
        Assert.InRange(bug.PauseDuration, ant.PauseMin, ant.PauseMax);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugSimulationTests" -nologo -v q`
Expected: FAIL. `Pausing_bug_is_completely_still...` fails on the position assertion because the bug keeps walking (there is no `Pausing` handling yet), and `..._eventually_pauses` fails because the state never changes.

- [ ] **Step 3: Implement pausing**

In `BugSimulation.cs`, replace `UpdateState` and `UpdateWandering` with the versions below and add `UpdatePausing`. Also extend `EnterState` so returning to `Wandering` picks a new target (spec 5.3).

```csharp
    private void UpdateState(Bug bug, float dt, Vector2? cursor)
    {
        switch (bug.State)
        {
            case BugState.Squashed:
                bug.Speed = 0f;
                bug.SquashProgress += dt / SquashDuration;
                break;
            case BugState.Wandering:
                UpdateWandering(bug, dt);
                break;
            case BugState.Pausing:
                UpdatePausing(bug);
                break;
        }
    }

    private void UpdateWandering(Bug bug, float dt)
    {
        if (bug.RetargetTimer <= 0f)
        {
            PickNewTarget(bug);
        }

        bug.Heading += rng.NextFloat(-HeadingNoise, HeadingNoise) * dt;
        bug.Speed = bug.Species.WalkSpeed * bug.SpeedFactor;

        if (rng.NextFloat() < bug.Species.PauseChancePerSecond * dt)
        {
            bug.PauseDuration = rng.NextFloat(bug.Species.PauseMin, bug.Species.PauseMax);
            EnterState(bug, BugState.Pausing);
        }
    }

    private void UpdatePausing(Bug bug)
    {
        bug.Speed = 0f;
        if (bug.StateTime >= bug.PauseDuration)
        {
            EnterState(bug, BugState.Wandering);
        }
    }
```

Replace `EnterState` with:

```csharp
    private void EnterState(Bug bug, BugState state)
    {
        bug.State = state;
        bug.StateTime = 0f;
        switch (state)
        {
            case BugState.Wandering:
                PickNewTarget(bug);
                break;
            case BugState.Pausing:
                bug.Speed = 0f;
                break;
            case BugState.Squashed:
                bug.Speed = 0f;
                bug.SquashProgress = 0f;
                break;
        }
    }
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test tests/ScreenBugs.Tests -nologo -v q`
Expected: `Passed!` with 0 failures.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/BugSimulation.cs tests/ScreenBugs.Tests/BugSimulationTests.cs
git commit -m "feat(core): pausing state with chance-based entry and timed exit

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 8: Fleeing from the cursor

**Files:**
- Modify: `src/ScreenBugs.Core/Simulation/BugSimulation.cs`
- Test: `tests/ScreenBugs.Tests/BugFleeTests.cs`

- [ ] **Step 1: Write the failing flee tests**

Create `tests/ScreenBugs.Tests/BugFleeTests.cs`. The bug heads right (+X) and the cursor sits 40 DIPs behind it (spec 10).

```csharp
namespace ScreenBugs.Tests;

public sealed class BugFleeTests
{
    private static readonly Vector2 Start = new(960, 540);
    private static readonly Vector2 CursorBehind = new(920, 540);

    [Fact]
    public void Bug_does_not_flee_before_the_minimum_reaction_delay()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);

        SimulationSteps.StepFor(sim, 0.08f, CursorBehind);

        Assert.NotEqual(BugState.Fleeing, bug.State);
        Assert.NotNull(bug.ReactionTimer);
    }

    [Fact]
    public void Bug_flees_within_half_a_second_and_gets_farther_from_the_cursor()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);
        float startDistance = Vector2.Distance(CursorBehind, bug.Position);

        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);

        Assert.Equal(BugState.Fleeing, bug.State);
        Assert.True(Vector2.Distance(CursorBehind, bug.Position) > startDistance);
        Assert.Equal(SimulationSteps.Walker.FleeSpeed * bug.SpeedFactor, bug.Speed);
    }

    [Fact]
    public void Cursor_leaving_before_the_reaction_fires_cancels_the_reaction()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);

        SimulationSteps.StepFor(sim, 0.05f, CursorBehind);
        Assert.NotNull(bug.ReactionTimer);

        sim.Step(SimulationSteps.Dt, null);

        Assert.Null(bug.ReactionTimer);
        Assert.Equal(BugState.Wandering, bug.State);
    }

    [Fact]
    public void Fleeing_ends_with_a_pause_then_wandering_once_the_cursor_is_gone()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);
        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);
        Assert.Equal(BugState.Fleeing, bug.State);

        var states = new List<BugState>();
        for (int i = 0; i < 60 * 3; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            states.Add(bug.State);
        }

        int firstPausing = states.IndexOf(BugState.Pausing);
        int firstWandering = states.IndexOf(BugState.Wandering);
        Assert.True(firstPausing >= 0, "never paused after fleeing");
        Assert.True(firstWandering > firstPausing, "did not wander after the pause");
        Assert.Equal(BugState.Fleeing, states[0]);
    }

    [Fact]
    public void Fleeing_bug_ignores_pause_chance_and_keeps_running_while_the_cursor_follows()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SpeciesCatalog.Get(SpeciesId.BlackGardenAnt), Start, 0f);
        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);
        Assert.Equal(BugState.Fleeing, bug.State);

        for (int i = 0; i < 60; i++)
        {
            var chasingCursor = bug.Position - SimulationSteps.Direction(bug.Heading) * 30f;
            sim.Step(SimulationSteps.Dt, chasingCursor);
            Assert.Equal(BugState.Fleeing, bug.State);
        }
    }

    [Fact]
    public void Squashed_bug_does_not_react_to_the_cursor()
    {
        var sim = SimulationSteps.Create(0);
        var bug = sim.AddBug(SimulationSteps.Walker, Start, 0f);
        sim.TrySquashAt(Start);
        var position = bug.Position;

        SimulationSteps.StepFor(sim, 0.5f, CursorBehind);

        Assert.Equal(BugState.Squashed, bug.State);
        Assert.Equal(position, bug.Position);
        Assert.Null(bug.ReactionTimer);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugFleeTests" -nologo -v q`
Expected: FAIL. `Bug_flees_within_half_a_second...` fails because the state is still `Wandering`.

- [ ] **Step 3: Add the flee constants**

Add to the constants block in `BugSimulation.cs`:

```csharp
    private const float FleeSafeDistanceFactor = 1.5f;
    private const float FleeSafeDuration = 0.8f;
    private const float FleeJitterInterval = 0.3f;
    private const float FleeJitterMax = 20f * MathF.PI / 180f;
    private const float MinFleeDistance = 0.01f;
```

- [ ] **Step 4: Implement the reaction and fleeing logic**

Replace `UpdateState` with:

```csharp
    private void UpdateState(Bug bug, float dt, Vector2? cursor)
    {
        if (bug.State == BugState.Squashed)
        {
            bug.Speed = 0f;
            bug.SquashProgress += dt / SquashDuration;
            return;
        }

        UpdateReaction(bug, cursor);

        switch (bug.State)
        {
            case BugState.Wandering:
                UpdateWandering(bug, dt);
                break;
            case BugState.Pausing:
                UpdatePausing(bug);
                break;
            case BugState.Fleeing:
                UpdateFleeing(bug, dt, cursor);
                break;
        }
    }

    /// <summary>Common cursor reaction (spec 5.3): arm a delay when the cursor comes close, cancel if it leaves, flee when it expires.</summary>
    private void UpdateReaction(Bug bug, Vector2? cursor)
    {
        bool cursorNear = cursor is { } c && Vector2.Distance(c, bug.Position) <= bug.Species.FleeRadius;
        if (!cursorNear)
        {
            bug.ReactionTimer = null;
            return;
        }

        if (bug.State == BugState.Fleeing)
        {
            return;
        }

        bug.ReactionTimer ??= rng.NextFloat(bug.Species.ReactionDelayMin, bug.Species.ReactionDelayMax);
        if (bug.ReactionTimer <= 0f)
        {
            EnterState(bug, BugState.Fleeing);
        }
    }

    private void UpdateFleeing(Bug bug, float dt, Vector2? cursor)
    {
        bug.Speed = bug.Species.FleeSpeed * bug.SpeedFactor;

        if (bug.FleeJitterTimer <= 0f)
        {
            bug.FleeJitter = rng.NextFloat(-FleeJitterMax, FleeJitterMax);
            bug.FleeJitterTimer = FleeJitterInterval;
        }

        bool cursorFar = cursor is not { } c
            || Vector2.Distance(c, bug.Position) > FleeSafeDistanceFactor * bug.Species.FleeRadius;
        bug.FleeSafeTime = cursorFar ? bug.FleeSafeTime + dt : 0f;

        if (bug.FleeSafeTime >= FleeSafeDuration)
        {
            bug.PauseDuration = rng.NextFloat(0.3f, 1.0f);
            EnterState(bug, BugState.Pausing);
        }
    }
```

Replace `DesiredDirection` with:

```csharp
    /// <summary>Where the bug wants to go before edge steering (spec 5.3).</summary>
    private static Vector2 DesiredDirection(Bug bug, Vector2? cursor)
    {
        if (bug.State != BugState.Fleeing)
        {
            return Direction(bug.TargetHeading);
        }

        if (cursor is not { } c)
        {
            return Direction(bug.Heading);
        }

        Vector2 away = bug.Position - c;
        if (away.Length() < MinFleeDistance)
        {
            return Direction(bug.Heading);
        }

        return Direction(MathF.Atan2(away.Y, away.X) + bug.FleeJitter);
    }
```

Add a `Fleeing` case to `EnterState`:

```csharp
            case BugState.Fleeing:
                bug.ReactionTimer = null;
                bug.FleeSafeTime = 0f;
                bug.FleeJitterTimer = 0f;
                break;
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test tests/ScreenBugs.Tests -nologo -v q`
Expected: `Passed!` with 0 failures.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/BugSimulation.cs tests/ScreenBugs.Tests/BugFleeTests.cs
git commit -m "feat(core): flee from the cursor with reaction delay, jitter and cool-down

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 9: Respawn, stragglers and target count

**Files:**
- Modify: `src/ScreenBugs.Core/Simulation/BugSimulation.cs`
- Modify: `tests/ScreenBugs.Tests/BugSpawnTests.cs`

- [ ] **Step 1: Add the failing respawn and count tests**

Append inside the `BugSpawnTests` class:

```csharp
    [Fact]
    public void After_a_squash_the_population_is_restored_within_eight_and_a_half_seconds()
    {
        var sim = SimulationSteps.Create(3);
        int maxIdBefore = sim.Bugs.Max(b => b.Id);
        Assert.True(sim.TrySquashAt(sim.Bugs[0].Position));
        Assert.Equal(2, SimulationSteps.AliveCount(sim));

        SimulationSteps.StepFor(sim, 8.5f);

        Assert.Equal(3, SimulationSteps.AliveCount(sim));
        Assert.True(sim.Bugs.Max(b => b.Id) > maxIdBefore);
    }

    [Fact]
    public void Respawn_timer_starts_after_a_death_and_is_cancelled_by_a_count_change()
    {
        var sim = SimulationSteps.Create(3);
        sim.TrySquashAt(sim.Bugs[0].Position);
        Assert.Null(sim.RespawnTimer);

        sim.Step(SimulationSteps.Dt, null);
        Assert.NotNull(sim.RespawnTimer);
        Assert.InRange(sim.RespawnTimer!.Value, 3f, 8f);

        sim.TargetCount = 5;

        Assert.Null(sim.RespawnTimer);
        Assert.Equal(5, SimulationSteps.AliveCount(sim));
        for (int i = 0; i < 600; i++)
        {
            sim.Step(SimulationSteps.Dt, null);
            Assert.True(SimulationSteps.AliveCount(sim) <= 5, $"exceeded target at step {i}");
        }
    }

    [Fact]
    public void Raising_the_target_spawns_immediately_and_lowering_removes_alive_bugs_only()
    {
        var sim = SimulationSteps.Create(3);

        sim.TargetCount = 10;
        Assert.Equal(10, SimulationSteps.AliveCount(sim));

        sim.TrySquashAt(sim.Bugs[0].Position);
        sim.TargetCount = 1;

        Assert.Equal(1, SimulationSteps.AliveCount(sim));
        Assert.Contains(sim.Bugs, b => b.State == BugState.Squashed);
        Assert.Equal(1, sim.TargetCount);
    }

    [Fact]
    public void Bug_that_never_enters_the_screen_is_removed_after_ten_seconds_and_replaced()
    {
        var sim = SimulationSteps.Create(0);
        var straggler = sim.AddBug(SimulationSteps.Walker, new Vector2(-30, 540), MathF.PI);
        straggler.State = BugState.Pausing;
        straggler.StateTime = 0f;
        straggler.PauseDuration = 100f;
        sim.TargetCount = 1;
        Assert.Single(sim.Bugs);

        SimulationSteps.StepFor(sim, 10.5f);
        Assert.DoesNotContain(straggler, sim.Bugs);

        SimulationSteps.StepFor(sim, 8.5f);
        Assert.Single(sim.Bugs);
        Assert.NotSame(straggler, sim.Bugs[0]);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ScreenBugs.Tests --filter "FullyQualifiedName~BugSpawnTests" -nologo -v q`
Expected: build FAILS with `CS1061: 'BugSimulation' does not contain a definition for 'RespawnTimer'`.

- [ ] **Step 3: Add the respawn constant and state**

Add to the constants block:

```csharp
    private const float StragglerTimeout = 10f;
```

Add a field after `targetCount`:

```csharp
    private float? respawnTimer;
```

- [ ] **Step 4: Implement RespawnTimer, timer cancellation, straggler removal and respawn**

Add after the `Bugs` property:

```csharp
    /// <summary>Seconds until the next respawn, or null when no respawn is pending. Exposed for tests.</summary>
    internal float? RespawnTimer => respawnTimer;
```

In the `TargetCount` setter, insert `respawnTimer = null;` immediately after `targetCount = value;` so a count change cancels any pending respawn (spec 5.6).

Replace `Step` with:

```csharp
    public void Step(float dt, Vector2? cursor)
    {
        dt = MathF.Min(dt, MaxDt);
        foreach (var bug in bugs)
        {
            AdvanceTimers(bug, dt);
            UpdateState(bug, dt, cursor);
            Move(bug, dt, cursor);
        }

        bugs.RemoveAll(b => b.SquashProgress >= 1f || (!b.HasEnteredScreen && b.Age >= StragglerTimeout));
        Respawn(dt);
    }

    /// <summary>Respawn (spec 5.5): one pending timer of 3 to 8 s whenever the population is short; spawn only if still short when it expires.</summary>
    private void Respawn(float dt)
    {
        if (respawnTimer is { } remaining)
        {
            remaining -= dt;
            if (remaining > 0f)
            {
                respawnTimer = remaining;
                return;
            }

            respawnTimer = null;
            if (AliveCount < targetCount)
            {
                SpawnFromEdge();
            }

            return;
        }

        if (AliveCount < targetCount)
        {
            respawnTimer = rng.NextFloat(3f, 8f);
        }
    }
```

- [ ] **Step 5: Run all tests**

Run: `dotnet test tests/ScreenBugs.Tests -nologo -v q`
Expected: `Passed!` with 0 failures and no build warnings about unused members.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs.Core/Simulation/BugSimulation.cs tests/ScreenBugs.Tests/BugSpawnTests.cs
git commit -m "feat(core): respawn timer, straggler removal and adjustable target count

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

Chunk 2 is complete when `dotnet test tests/ScreenBugs.Tests` passes and `git log --oneline` shows the nine Chunk 1 and Chunk 2 commits on top of the spec commits.

<!-- end of chunk 2 -->

## Chunk 3: WPF overlay, Win32 plumbing, rendering primitives, first painter

The WPF layer has no unit tests (spec 10 assigns it a manual checklist), so each task here is: write the files, build, commit. Task 16 ends with the manual checks that prove click-through, squashing, and CPU cost before the other painters are ported.

### Task 10: WPF application project

**Files:**
- Create: `src/ScreenBugs/ScreenBugs.csproj`, `src/ScreenBugs/app.manifest`, `src/ScreenBugs/App.xaml`, `src/ScreenBugs/App.xaml.cs`
- Delete: `src/ScreenBugs/MainWindow.xaml`, `src/ScreenBugs/MainWindow.xaml.cs`

- [ ] **Step 1: Scaffold the project and wire it into the solution**

```bash
dotnet new wpf -n ScreenBugs -o src/ScreenBugs
dotnet sln ScreenBugs.slnx add src/ScreenBugs
dotnet add src/ScreenBugs reference src/ScreenBugs.Core
rm src/ScreenBugs/MainWindow.xaml src/ScreenBugs/MainWindow.xaml.cs
```

Keep the generated `AssemblyInfo.cs` (it holds the WPF `ThemeInfo` attribute).

- [ ] **Step 2: Replace the csproj**

Overwrite `src/ScreenBugs/ScreenBugs.csproj`. The two `Using Remove` lines stop the WinForms SDK from importing `System.Drawing` and `System.Windows.Forms` everywhere, which would make `Color` and `Point` ambiguous with WPF's types. `PlatformTarget` is x64 because the code calls the 64-bit-only `GetWindowLongPtrW`.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <PlatformTarget>x64</PlatformTarget>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RootNamespace>ScreenBugs</RootNamespace>
    <AssemblyName>ScreenBugs</AssemblyName>
    <!-- WFAC010: the WinForms analyzer objects to DPI settings in app.manifest; the manifest is the right place for a WPF host. -->
    <NoWarn>$(NoWarn);WFAC010</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Using Remove="System.Drawing" />
    <Using Remove="System.Windows.Forms" />
    <Using Include="System.Numerics" />
    <Using Include="ScreenBugs.Core.Simulation" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ScreenBugs.Core\ScreenBugs.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Add the DPI manifest**

Create `src/ScreenBugs/app.manifest` (spec 7.1: PerMonitorV2 so DIP math matches the primary monitor):

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="ScreenBugs.app" />
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 4: Replace App.xaml and App.xaml.cs**

Overwrite `src/ScreenBugs/App.xaml` (no `StartupUri`; the overlay is created in code, and the app only exits when told to):

```xml
<Application x:Class="ScreenBugs.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
    </Application.Resources>
</Application>
```

Overwrite `src/ScreenBugs/App.xaml.cs` with a placeholder that Task 16 fills in:

```csharp
using System.Windows;

namespace ScreenBugs;

public partial class App : Application
{
}
```

- [ ] **Step 5: Build**

Run: `dotnet build ScreenBugs.slnx -nologo -v q`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add ScreenBugs.slnx src/ScreenBugs
git commit -m "chore: add ScreenBugs WPF application project

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 11: Win32 interop

**Files:**
- Create: `src/ScreenBugs/Overlay/NativeMethods.cs`

- [ ] **Step 1: Write NativeMethods**

Create `src/ScreenBugs/Overlay/NativeMethods.cs` (spec 7.6). `SetWindowLongPtr` returns 0 both on failure and when the previous value was 0, so the wrapper clears the last error first and only throws when an error code is actually set.

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScreenBugs.Overlay;

/// <summary>The Win32 calls the overlay needs (spec 7.6).</summary>
internal static class NativeMethods
{
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    private const int GWL_EXSTYLE = -20;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    /// <summary>Reads the window's extended style bits.</summary>
    public static int GetExtendedStyle(IntPtr hwnd)
    {
        Marshal.SetLastSystemError(0);
        IntPtr result = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ThrowIfZeroWithError(result);
        return (int)result.ToInt64();
    }

    /// <summary>Replaces the window's extended style bits.</summary>
    public static void SetExtendedStyle(IntPtr hwnd, int style)
    {
        Marshal.SetLastSystemError(0);
        IntPtr result = SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style));
        ThrowIfZeroWithError(result);
    }

    /// <summary>Moves the window to the top of the topmost band without activating, moving or resizing it.</summary>
    public static void BringToTopmost(IntPtr hwnd)
    {
        if (!SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>Cursor position in physical screen pixels; false when Windows will not report it.</summary>
    public static bool TryGetCursorPosition(out int x, out int y)
    {
        if (GetCursorPos(out POINT point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private static void ThrowIfZeroWithError(IntPtr result)
    {
        int error = Marshal.GetLastPInvokeError();
        if (result == IntPtr.Zero && error != 0)
        {
            throw new Win32Exception(error);
        }
    }
}
```

- [ ] **Step 2: Build and commit**

Run: `dotnet build src/ScreenBugs -nologo -v q`
Expected: `Build succeeded.`

```bash
git add src/ScreenBugs/Overlay/NativeMethods.cs
git commit -m "feat(overlay): Win32 wrappers for extended styles, topmost and cursor position

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 12: Frame loop, cursor tracker, click-through controller, topmost keeper

**Files:**
- Create: `src/ScreenBugs/Overlay/FrameLoop.cs`
- Create: `src/ScreenBugs/Overlay/CursorTracker.cs`
- Create: `src/ScreenBugs/Overlay/ClickThroughController.cs`
- Create: `src/ScreenBugs/Overlay/TopmostKeeper.cs`

- [ ] **Step 1: Write FrameLoop**

Create `src/ScreenBugs/Overlay/FrameLoop.cs` (spec 7.4). Elapsed render time accumulates; a tick fires when a sixtieth of a second has built up, and the remainder (capped at one interval) carries over so 120 Hz and 144 Hz monitors both settle at 60 ticks per second. The real elapsed time since the last tick is passed on.

```csharp
using System.Windows.Media;

namespace ScreenBugs.Overlay;

/// <summary>Calls <paramref name="tick"/> with the elapsed seconds, at most 60 times per second, from WPF's rendering callback.</summary>
public sealed class FrameLoop(Action<float> tick)
{
    private const double Interval = 1.0 / 60.0;

    private TimeSpan? lastRenderingTime;
    private TimeSpan lastTickTime;
    private double accumulator;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        lastRenderingTime = null;
        accumulator = 0;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan now = ((RenderingEventArgs)e).RenderingTime;
        if (lastRenderingTime is not { } last)
        {
            lastRenderingTime = now;
            lastTickTime = now;
            return;
        }

        accumulator += (now - last).TotalSeconds;
        lastRenderingTime = now;
        if (accumulator < Interval)
        {
            return;
        }

        accumulator = Math.Min(accumulator - Interval, Interval);
        float dt = (float)(now - lastTickTime).TotalSeconds;
        lastTickTime = now;
        tick(dt);
    }
}
```

- [ ] **Step 2: Write CursorTracker**

Create `src/ScreenBugs/Overlay/CursorTracker.cs` (spec 7.3). The overlay's top-left is the screen origin, so converting screen pixels to DIPs with the window's device transform gives window coordinates directly.

```csharp
using System.Windows;

namespace ScreenBugs.Overlay;

/// <summary>Global cursor position in the overlay's DIP coordinates, or null when unavailable.</summary>
public static class CursorTracker
{
    public static Vector2? GetCursorDips(Window window)
    {
        if (!NativeMethods.TryGetCursorPosition(out int x, out int y))
        {
            return null;
        }

        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is null)
        {
            return null;
        }

        Point dips = source.CompositionTarget.TransformFromDevice.Transform(new Point(x, y));
        return new Vector2((float)dips.X, (float)dips.Y);
    }
}
```

- [ ] **Step 3: Write ClickThroughController**

Create `src/ScreenBugs/Overlay/ClickThroughController.cs` (spec 7.2). It only touches the window style when the desired state changes.

```csharp
namespace ScreenBugs.Overlay;

/// <summary>Sets WS_EX_TRANSPARENT (click-through) except while the cursor is over a bug.</summary>
public sealed class ClickThroughController(IntPtr hwnd)
{
    private bool? clickThrough;

    public void Update(bool cursorOverBug)
    {
        bool wanted = !cursorOverBug;
        if (clickThrough == wanted)
        {
            return;
        }

        int style = NativeMethods.GetExtendedStyle(hwnd);
        style = wanted
            ? style | NativeMethods.WS_EX_TRANSPARENT
            : style & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetExtendedStyle(hwnd, style);
        clickThrough = wanted;
    }
}
```

- [ ] **Step 4: Write TopmostKeeper**

Create `src/ScreenBugs/Overlay/TopmostKeeper.cs` (spec 7.5):

```csharp
using System.Windows.Threading;

namespace ScreenBugs.Overlay;

/// <summary>Re-asserts HWND_TOPMOST every two seconds so windows that become topmost later do not bury the overlay.</summary>
public sealed class TopmostKeeper(IntPtr hwnd)
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool subscribed;

    public void Start()
    {
        if (!subscribed)
        {
            timer.Tick += (_, _) => NativeMethods.BringToTopmost(hwnd);
            subscribed = true;
        }

        timer.Start();
    }

    public void Stop() => timer.Stop();
}
```

- [ ] **Step 5: Build and commit**

Run: `dotnet build src/ScreenBugs -nologo -v q`
Expected: `Build succeeded.`

```bash
git add src/ScreenBugs/Overlay
git commit -m "feat(overlay): frame loop, cursor tracking, click-through toggle and topmost keeper

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 13: Rendering primitives

**Files:**
- Create: `src/ScreenBugs/Rendering/IBugPainter.cs`
- Create: `src/ScreenBugs/Rendering/PainterPens.cs`
- Create: `src/ScreenBugs/Rendering/Shapes.cs`
- Create: `src/ScreenBugs/Rendering/LegPainter.cs`
- Create: `src/ScreenBugs/Rendering/BodyMotion.cs`

Painters draw in "specimen units", the coordinates in `bug-specimens.svg`, under a uniform `ScaleTransform` that maps the specimen body length to `Species.BodyLength` DIPs. These helpers make that convenient.

- [ ] **Step 1: Write IBugPainter**

Create `src/ScreenBugs/Rendering/IBugPainter.cs`:

```csharp
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Draws one species (spec 6).</summary>
public interface IBugPainter
{
    /// <summary>Main body color; the splat is drawn in a darkened version of it.</summary>
    Color BodyColor { get; }

    /// <summary>Draws the bug in bug-local space: origin at the body center, bug facing up (negative Y), DIP units.</summary>
    void Paint(DrawingContext dc, Bug bug);
}
```

- [ ] **Step 2: Write PainterPens**

Create `src/ScreenBugs/Rendering/PainterPens.cs`. Everything is frozen so WPF does not track changes on objects that never change.

```csharp
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Frozen brushes and pens for painters.</summary>
public static class PainterPens
{
    /// <summary>Alpha 1: invisible, yet non-transparent to Windows layered-window hit testing (spec 7.2).</summary>
    public static readonly SolidColorBrush HitDisc = Brush(Color.FromArgb(1, 0, 0, 0));

    /// <summary>Black at about 8 percent opacity for the shadow under each bug (spec 6).</summary>
    public static readonly SolidColorBrush Shadow = Brush(Color.FromArgb(20, 0, 0, 0));

    /// <summary>Parses an SVG-style "#rrggbb" color.</summary>
    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>Round-capped pen. <paramref name="specimenWidth"/> is in specimen units and is raised so the line is never thinner than one DIP after <paramref name="scale"/> is applied.</summary>
    public static Pen Pen(Color color, double specimenWidth, double scale)
    {
        var pen = new Pen(Brush(color), Math.Max(specimenWidth, 1.0 / scale))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        return pen;
    }

    /// <summary>Moves a color toward black by <paramref name="fraction"/> (0 to 1).</summary>
    public static Color Darken(Color color, double fraction) => Color.FromRgb(
        (byte)(color.R * (1 - fraction)),
        (byte)(color.G * (1 - fraction)),
        (byte)(color.B * (1 - fraction)));
}
```

- [ ] **Step 3: Write Shapes**

Create `src/ScreenBugs/Rendering/Shapes.cs`. The names mirror the SVG path commands (`L`, `Q`, `C`, `Z`) so porting from the specimen sheet is mechanical.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Frozen geometry builders mirroring the SVG path commands in the specimen sheet.</summary>
public static class Shapes
{
    /// <summary>SVG "M p0 L p1 L p2 ..." (open).</summary>
    public static PathGeometry Polyline(params Point[] points) => Build(points, closed: false);

    /// <summary>SVG "M p0 L p1 ... Z" (closed and filled).</summary>
    public static PathGeometry Polygon(params Point[] points) => Build(points, closed: true);

    /// <summary>SVG "M start Q control end".</summary>
    public static PathGeometry Quadratic(Point start, Point control, Point end) =>
        Figure(start, closed: false, new QuadraticBezierSegment(control, end, isStroked: true));

    /// <summary>SVG "M start C c1 c2 end".</summary>
    public static PathGeometry Cubic(Point start, Point c1, Point c2, Point end) =>
        Figure(start, closed: false, new BezierSegment(c1, c2, end, isStroked: true));

    /// <summary>One figure from explicit segments, for mixed L/Q/C paths.</summary>
    public static PathGeometry Figure(Point start, bool closed, params PathSegment[] segments)
    {
        var figure = new PathFigure { StartPoint = start, IsClosed = closed, IsFilled = closed };
        foreach (var segment in segments)
        {
            figure.Segments.Add(segment);
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    public static LineSegment Line(Point to) => new(to, isStroked: true);

    public static QuadraticBezierSegment Quad(Point control, Point to) => new(control, to, isStroked: true);

    public static BezierSegment Bezier(Point c1, Point c2, Point to) => new(c1, c2, to, isStroked: true);

    private static PathGeometry Build(Point[] points, bool closed) =>
        Figure(points[0], closed, points.Skip(1).Select(p => (PathSegment)Line(p)).ToArray());
}
```

- [ ] **Step 4: Write LegPainter**

Create `src/ScreenBugs/Rendering/LegPainter.cs` (spec 6 gait). A leg is hip to knee to foot, rotated about the hip by the swing. `DrawLegPair` draws the left leg and its mirror with the same signed swing, which puts the pair in antiphase because the mirrored geometry turns the same rotation into the opposite stride.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Two-segment legs with the gait swing from spec 6.</summary>
public static class LegPainter
{
    /// <summary>Signed swing in radians: amplitude times sin(2π(phase + groupOffset)).</summary>
    public static double Swing(float legPhase, double groupOffset, double amplitudeDegrees) =>
        amplitudeDegrees * Math.PI / 180.0 * Math.Sin(2.0 * Math.PI * (legPhase + groupOffset));

    /// <summary>Draws one leg rotated about the hip by <paramref name="swingRadians"/>.</summary>
    public static void DrawLeg(DrawingContext dc, Pen pen, Point hip, Point knee, Point foot, double swingRadians)
    {
        dc.PushTransform(new RotateTransform(swingRadians * 180.0 / Math.PI, hip.X, hip.Y));
        dc.DrawLine(pen, hip, knee);
        dc.DrawLine(pen, knee, foot);
        dc.Pop();
    }

    /// <summary>Draws a left leg (negative X) and its right-side mirror with the same signed swing.</summary>
    public static void DrawLegPair(DrawingContext dc, Pen pen, Point hip, Point knee, Point foot, double swingRadians)
    {
        DrawLeg(dc, pen, hip, knee, foot, swingRadians);
        DrawLeg(dc, pen, Mirror(hip), Mirror(knee), Mirror(foot), swingRadians);
    }

    public static Point Mirror(Point point) => new(-point.X, point.Y);
}
```

- [ ] **Step 5: Write BodyMotion**

Create `src/ScreenBugs/Rendering/BodyMotion.cs` (spec 6 body bob and antenna waggle):

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Body bob and antenna waggle shared by every painter.</summary>
public static class BodyMotion
{
    private const double BobDips = 1.0;
    private const double AntennaAmplitudeDegrees = 3.0;

    /// <summary>Sideways body offset in specimen units: one DIP times sin(4π phase).</summary>
    public static double Bob(float legPhase, double scale) =>
        BobDips / scale * Math.Sin(4.0 * Math.PI * legPhase);

    /// <summary>Antenna rotation in degrees: 3° times sin(2π phase + side); side is 0 for the left antenna and π for the right.</summary>
    public static double AntennaAngle(float legPhase, double side) =>
        AntennaAmplitudeDegrees * Math.Sin(2.0 * Math.PI * legPhase + side);

    /// <summary>Strokes an antenna rotated about its base by <see cref="AntennaAngle"/>.</summary>
    public static void DrawAntenna(DrawingContext dc, Pen pen, PathGeometry antenna, Point basePoint, float legPhase, double side)
    {
        dc.PushTransform(new RotateTransform(AntennaAngle(legPhase, side), basePoint.X, basePoint.Y));
        dc.DrawGeometry(null, pen, antenna);
        dc.Pop();
    }
}
```

- [ ] **Step 6: Build and commit**

Run: `dotnet build src/ScreenBugs -nologo -v q`
Expected: `Build succeeded.`

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): painter interface, frozen pens, SVG-style shapes, leg and body motion helpers

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 14: Ant geometry, first painter, painter registry

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/AntGeometry.cs`
- Create: `src/ScreenBugs/Rendering/Painters/BlackGardenAntPainter.cs`
- Create: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

The ant is the `<symbol id="ant">` in `bug-specimens.svg`. Its body runs from the top of the head (y = -46) to the tip of the abdomen (y = 35), 81 specimen units.

- [ ] **Step 1: Write AntGeometry**

Create `src/ScreenBugs/Rendering/Painters/AntGeometry.cs`:

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

/// <summary>Ant drawing shared by the black garden ant and the red fire ant (specimen "ant" symbol).</summary>
public sealed class AntGeometry(Color color, float bodyLength)
{
    private const double SpecimenBodyLength = 81.0;
    private const double LegAmplitudeDegrees = 9.0;

    private readonly double scale = bodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush body = PainterPens.Brush(color);
    private readonly Pen legPen = PainterPens.Pen(color, 1.6, bodyLength / SpecimenBodyLength);
    private readonly Pen antennaPen = PainterPens.Pen(color, 1.4, bodyLength / SpecimenBodyLength);
    private readonly PathGeometry leftMandible = Shapes.Quadratic(new(-6, -44), new(-8, -52), new(-2, -50));
    private readonly PathGeometry rightMandible = Shapes.Quadratic(new(6, -44), new(8, -52), new(2, -50));
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-5, -44), new(-14, -58), new(-26, -64));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(5, -44), new(14, -58), new(26, -64));

    /// <summary>Initialized property rather than `=> color` so the parameter is not both captured and used in initializers (CS9124).</summary>
    public Color Color { get; } = color;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 22), 14, 19);

        LegPainter.DrawLegPair(dc, legPen, new(-6, -22), new(-22, -36), new(-30, -24), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-7, -15), new(-26, -14), new(-34, -2), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-6, -8), new(-22, 4), new(-26, 20), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        dc.DrawGeometry(null, legPen, leftMandible);
        dc.DrawGeometry(null, legPen, rightMandible);
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-5, -44), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(5, -44), bug.LegPhase, Math.PI);
        dc.DrawEllipse(body, null, new Point(0, -36), 10, 10);
        dc.DrawEllipse(body, null, new Point(0, -15), 6, 11);
        dc.DrawEllipse(body, null, new Point(0, -1), 3, 3);
        dc.DrawEllipse(body, null, new Point(0, 18), 12, 17);
        dc.Pop();

        dc.Pop();
    }
}
```

- [ ] **Step 2: Write BlackGardenAntPainter**

Create `src/ScreenBugs/Rendering/Painters/BlackGardenAntPainter.cs`:

```csharp
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class BlackGardenAntPainter : IBugPainter
{
    private readonly AntGeometry ant = new(PainterPens.Hex("#1c1c1c"), SpeciesCatalog.Get(SpeciesId.BlackGardenAnt).BodyLength);

    public Color BodyColor => ant.Color;

    public void Paint(DrawingContext dc, Bug bug) => ant.Paint(dc, bug);
}
```

- [ ] **Step 3: Write BugPainterRegistry with a temporary placeholder mapping**

Create `src/ScreenBugs/Rendering/BugPainterRegistry.cs`. Until Chunk 4 lands, every species draws as a black garden ant so the overlay can be exercised now.

```csharp
using ScreenBugs.Rendering.Painters;

namespace ScreenBugs.Rendering;

/// <summary>Maps each <see cref="SpeciesId"/> to its painter.</summary>
public sealed class BugPainterRegistry
{
    private static readonly IBugPainter Placeholder = new BlackGardenAntPainter();

    private readonly Dictionary<SpeciesId, IBugPainter> painters =
        Enum.GetValues<SpeciesId>().ToDictionary(id => id, _ => Placeholder);

    public IBugPainter Get(SpeciesId id) => painters[id];
}
```

- [ ] **Step 4: Build and commit**

Run: `dotnet build src/ScreenBugs -nologo -v q`
Expected: `Build succeeded.`

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): shared ant geometry, black garden ant painter and painter registry

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 15: BugCanvas and OverlayWindow

**Files:**
- Create: `src/ScreenBugs/Overlay/BugCanvas.cs`
- Create: `src/ScreenBugs/Overlay/OverlayWindow.xaml`, `src/ScreenBugs/Overlay/OverlayWindow.xaml.cs`

- [ ] **Step 1: Write BugCanvas**

Create `src/ScreenBugs/Overlay/BugCanvas.cs` (spec 6, BugCanvas). Squashed bugs are skipped here; Chunk 4 adds the splat.

```csharp
using System.Windows;
using System.Windows.Media;
using ScreenBugs.Rendering;

namespace ScreenBugs.Overlay;

/// <summary>Draws every bug once per frame: hit disc, then the species painter in bug-local space.</summary>
public sealed class BugCanvas : FrameworkElement
{
    private readonly BugPainterRegistry painters = new();

    public BugSimulation? Simulation { get; set; }

    public void Redraw() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        if (Simulation is null)
        {
            return;
        }

        foreach (var bug in Simulation.Bugs)
        {
            if (!bug.IsAlive)
            {
                continue;
            }

            var center = new Point(bug.Position.X, bug.Position.Y);
            dc.DrawEllipse(PainterPens.HitDisc, null, center, bug.Species.HitRadius, bug.Species.HitRadius);
            dc.PushTransform(new TranslateTransform(center.X, center.Y));
            dc.PushTransform(new RotateTransform(bug.Heading * 180.0 / Math.PI + 90.0));
            painters.Get(bug.Species.Id).Paint(dc, bug);
            dc.Pop();
            dc.Pop();
        }
    }
}
```

- [ ] **Step 2: Write OverlayWindow.xaml**

Create `src/ScreenBugs/Overlay/OverlayWindow.xaml` (spec 7.1):

```xml
<Window x:Class="ScreenBugs.Overlay.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:overlay="clr-namespace:ScreenBugs.Overlay"
        Title="Screen Bugs"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ResizeMode="NoResize"
        Topmost="True"
        ShowInTaskbar="False"
        ShowActivated="False"
        Focusable="False"
        WindowStartupLocation="Manual"
        Left="0"
        Top="0">
    <overlay:BugCanvas x:Name="Surface" />
</Window>
```

- [ ] **Step 3: Write OverlayWindow.xaml.cs**

Create `src/ScreenBugs/Overlay/OverlayWindow.xaml.cs`. The extended styles are applied once the native window exists. The `Surface` field is generated from `x:Name` and is visible inside the assembly.

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenBugs.Overlay;

/// <summary>Transparent, topmost, click-through window covering the primary monitor (spec 7.1).</summary>
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

    public IntPtr Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        int style = NativeMethods.GetExtendedStyle(Handle);
        NativeMethods.SetExtendedStyle(
            Handle,
            style | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Point position = e.GetPosition(this);
        Surface.Simulation?.TrySquashAt(new Vector2((float)position.X, (float)position.Y));
        e.Handled = true;
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `dotnet build src/ScreenBugs -nologo -v q`
Expected: `Build succeeded.`

```bash
git add src/ScreenBugs/Overlay
git commit -m "feat(overlay): BugCanvas renderer and transparent topmost OverlayWindow

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 16: Minimal composition and the first manual run

**Files:**
- Modify: `src/ScreenBugs/App.xaml.cs`

- [ ] **Step 1: Wire the simulation, window and loop together**

Overwrite `src/ScreenBugs/App.xaml.cs` (spec 7.7 tick; tray, single instance and crash logging arrive in Chunk 5):

```csharp
using System.Windows;
using ScreenBugs.Overlay;

namespace ScreenBugs;

public partial class App : Application
{
    private const int InitialBugCount = 3;

    private OverlayWindow? overlay;
    private FrameLoop? frameLoop;
    private TopmostKeeper? topmostKeeper;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var bounds = new Bounds((float)SystemParameters.PrimaryScreenWidth, (float)SystemParameters.PrimaryScreenHeight);
        var simulation = new BugSimulation(bounds, new SystemRandomSource()) { TargetCount = InitialBugCount };

        var window = new OverlayWindow();
        window.Surface.Simulation = simulation;
        window.Show();
        overlay = window;

        var clickThrough = new ClickThroughController(window.Handle);
        topmostKeeper = new TopmostKeeper(window.Handle);
        frameLoop = new FrameLoop(dt =>
        {
            Vector2? cursor = CursorTracker.GetCursorDips(window);
            simulation.Step(dt, cursor);
            clickThrough.Update(cursor is { } c && simulation.HitTest(c) is not null);
            window.Surface.Redraw();
        });

        frameLoop.Start();
        topmostKeeper.Start();
    }
}
```

- [ ] **Step 2: Build and run**

Run: `dotnet run --project src/ScreenBugs -c Release`
Expected: no console errors; within a few seconds three ants walk in from the screen edges over your desktop. The app has no tray icon yet; stop it with Ctrl+C in that terminal, or from another PowerShell with `Stop-Process -Name ScreenBugs`.

- [ ] **Step 3: Manual checks (spec 10 items 1, 2, 3, 5)**

While the app runs, confirm each:

1. Ants wander, pause, and turn; legs animate while walking and stop while paused.
2. Clicking and typing in a browser or editor works normally everywhere the ants are not.
3. Moving the cursor toward an ant makes it run; a quick decisive click on an ant makes it vanish (the splat comes in Chunk 4), and a replacement ant walks in from an edge 3 to 8 s later.
4. Alt-Tab shows no "Screen Bugs" entry, and after squashing an ant the app you were using still has keyboard focus.

The ants are about 16 DIPs long, so on a high-resolution display run these checks with `InitialBugCount` temporarily at `10` (Step 4 needs that anyway).

If a click on an ant passes through to the app underneath instead of squashing it, check `ClickThroughController.Update` is being called with `true` when the cursor is over a bug (set a breakpoint or a `Debug.WriteLine`) before changing anything else.

- [ ] **Step 4: CPU check (spec 10 item 8)**

Temporarily change `InitialBugCount` to `10`, rebuild, run in Release, and watch `ScreenBugs.exe` in Task Manager's Details tab for 30 seconds. Write the CPU percentage and screen resolution in the commit message body of Step 5. If total CPU stays below roughly 10 percent, proceed. If it is clearly higher, change `FrameLoop.Interval` to `1.0 / 30.0`, re-measure, and note both numbers; leave the constant at whichever value the user prefers after reading the numbers. Restore `InitialBugCount` to `3` before committing.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs
git commit -m "feat(app): compose simulation, overlay window and frame loop

Measured CPU with 10 bugs at <resolution>: <n> percent.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

Chunk 3 is complete when the manual checks in Task 16 pass and the CPU number is recorded.

<!-- end of chunk 3 -->

## Chunk 4: Remaining painters and the splat

Every painter ports one `<g id="...">` group from `docs/superpowers/specs/assets/bug-specimens.svg` into specimen units under a `ScaleTransform`, following the pattern of `AntGeometry` (Task 14): shadow first, then legs (mirrored pairs with per-pair gait offsets from spec 6), then the body group offset by the bob, with antennae rotated by the waggle. `SpecimenBodyLength` is the head-top-to-tail extent in specimen units, excluding antennae, legs and mandibles. SVG coordinates are copied verbatim; only colors and pen widths go through `PainterPens`.

Each task ends with a build, a short visual check (run the app, temporarily set `App.InitialBugCount` to `10` so the species shows up quickly, restore it before committing), and a commit.

### Task 17: Red fire ant, hissing cockroach, explicit registry

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/RedFireAntPainter.cs`
- Create: `src/ScreenBugs/Rendering/Painters/HissingCockroachPainter.cs`
- Modify: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

- [ ] **Step 1: Write RedFireAntPainter**

```csharp
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class RedFireAntPainter : IBugPainter
{
    private readonly AntGeometry ant = new(PainterPens.Hex("#a8462a"), SpeciesCatalog.Get(SpeciesId.RedFireAnt).BodyLength);

    public Color BodyColor => ant.Color;

    public void Paint(DrawingContext dc, Bug bug) => ant.Paint(dc, bug);
}
```

- [ ] **Step 2: Write HissingCockroachPainter**

SVG group `hissing-cockroach`. Body: head top (-58 - 6 = -64) to abdomen tip (18 + 54 = 72), 136 units.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class HissingCockroachPainter : IBugPainter
{
    private const double SpecimenBodyLength = 136.0;
    private const double LegAmplitudeDegrees = 8.0;

    private static readonly Color Shell = PainterPens.Hex("#3b2314");
    private static readonly Color Dark = PainterPens.Hex("#2b1a0f");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.HissingCockroach).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush shell = PainterPens.Brush(Shell);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly SolidColorBrush horn = PainterPens.Brush(PainterPens.Hex("#4a2e18"));
    private readonly Pen shellOutline = PainterPens.Pen(PainterPens.Hex("#24140a"), 1.0, 1.0);
    private readonly Pen darkOutline = PainterPens.Pen(PainterPens.Hex("#1a0f08"), 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly Pen bandPen;
    private readonly PathGeometry leftAntenna = Shapes.Quadratic(new(-6, -62), new(-30, -95), new(-62, -98));
    private readonly PathGeometry rightAntenna = Shapes.Quadratic(new(6, -62), new(30, -95), new(62, -98));
    private readonly PathGeometry[] bands =
    [
        Shapes.Quadratic(new(-21, -20), new(0, -17), new(21, -20)),
        Shapes.Quadratic(new(-27, -4), new(0, -1), new(27, -4)),
        Shapes.Quadratic(new(-30, 12), new(0, 15), new(30, 12)),
        Shapes.Quadratic(new(-29, 28), new(0, 31), new(29, 28)),
        Shapes.Quadratic(new(-26, 44), new(0, 47), new(26, 44)),
        Shapes.Quadratic(new(-20, 58), new(0, 61), new(20, 58)),
    ];

    public HissingCockroachPainter()
    {
        legPen = PainterPens.Pen(Shell, 3.5, scale);
        antennaPen = PainterPens.Pen(Shell, 2.0, scale);
        bandPen = PainterPens.Pen(PainterPens.Hex("#9a6230"), 2.5, scale);
    }

    public Color BodyColor => Shell;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(4, 22), 34, 58);

        LegPainter.DrawLegPair(dc, legPen, new(-18, -30), new(-48, -52), new(-62, -30), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-24, -2), new(-58, -8), new(-70, 14), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-20, 26), new(-50, 44), new(-58, 72), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-6, -62), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(6, -62), bug.LegPhase, Math.PI);
        dc.DrawEllipse(dark, null, new Point(0, -58), 9, 6);
        dc.DrawEllipse(shell, shellOutline, new Point(0, 18), 30, 54);
        foreach (var band in bands)
        {
            dc.DrawGeometry(null, bandPen, band);
        }

        dc.DrawEllipse(dark, darkOutline, new Point(0, -42), 27, 17);
        dc.DrawEllipse(horn, null, new Point(-10, -52), 6, 4);
        dc.DrawEllipse(horn, null, new Point(10, -52), 6, 4);
        dc.Pop();

        dc.Pop();
    }
}
```

The pens that depend on `scale` are created in the constructor because a field initializer cannot read another instance field; this is the one constructor-body pattern used by the painters. Outline pens pass `scale: 1.0` because a one-unit outline is meant to scale down with the body.

- [ ] **Step 3: Make the registry explicit**

Overwrite `src/ScreenBugs/Rendering/BugPainterRegistry.cs`. `Placeholder` entries are replaced one per task through Task 23.

```csharp
using ScreenBugs.Rendering.Painters;

namespace ScreenBugs.Rendering;

/// <summary>Maps each <see cref="SpeciesId"/> to its painter.</summary>
public sealed class BugPainterRegistry
{
    private static readonly IBugPainter Placeholder = new BlackGardenAntPainter();

    private readonly Dictionary<SpeciesId, IBugPainter> painters = new()
    {
        [SpeciesId.HissingCockroach] = new HissingCockroachPainter(),
        [SpeciesId.BlackGardenAnt] = new BlackGardenAntPainter(),
        [SpeciesId.RedFireAnt] = new RedFireAntPainter(),
        [SpeciesId.PrayingMantis] = Placeholder,
        [SpeciesId.SevenSpotLadybug] = Placeholder,
        [SpeciesId.StagBeetle] = Placeholder,
        [SpeciesId.HouseSpider] = Placeholder,
        [SpeciesId.Centipede] = Placeholder,
        [SpeciesId.StinkBug] = Placeholder,
    };

    public IBugPainter Get(SpeciesId id) => painters[id];
}
```

- [ ] **Step 4: Build, look, commit**

Run: `dotnet build src/ScreenBugs -nologo -v q` then `dotnet run --project src/ScreenBugs -c Release`.
Expected: reddish ants and large dark banded cockroaches appear among the black ants; cockroach antennae waggle and legs cycle. Stop the app.

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): red fire ant and hissing cockroach painters, explicit registry

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 18: Praying mantis

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/PrayingMantisPainter.cs`
- Modify: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

- [ ] **Step 1: Write PrayingMantisPainter**

SVG group `praying-mantis`. Body: eye top (-84 - 4 = -88) to abdomen tip (80), 168 units. Four walking legs; the raptorial forelegs are static.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class PrayingMantisPainter : IBugPainter
{
    private const double SpecimenBodyLength = 168.0;
    private const double LegAmplitudeDegrees = 6.0;

    private static readonly Color Green = PainterPens.Hex("#5fae46");
    private static readonly Color Limb = PainterPens.Hex("#4f9a3c");
    private static readonly Color Dark = PainterPens.Hex("#2f6b23");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.PrayingMantis).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush green = PainterPens.Brush(Green);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly Pen outline = PainterPens.Pen(Dark, 1.0, 1.0);
    private readonly Pen vein = PainterPens.Pen(PainterPens.Hex("#3f8a30"), 0.8, 1.0);
    private readonly Pen legPen;
    private readonly Pen forelegPen;
    private readonly Pen antennaPen;
    private readonly PathGeometry abdomen = Shapes.Figure(
        new(0, -22), closed: true,
        Shapes.Bezier(new(14, -10), new(14, 40), new(6, 72)),
        Shapes.Quad(new(0, 80), new(-6, 72)),
        Shapes.Bezier(new(-14, 40), new(-14, -10), new(0, -22)));
    private readonly PathGeometry leftVein = Shapes.Quadratic(new(-6, -10), new(-8, 30), new(-3, 62));
    private readonly PathGeometry rightVein = Shapes.Quadratic(new(6, -10), new(8, 30), new(3, 62));
    private readonly PathGeometry leftForeleg = Shapes.Polyline(new(-3, -60), new(-22, -44), new(-12, -26));
    private readonly PathGeometry rightForeleg = Shapes.Polyline(new(3, -60), new(22, -44), new(12, -26));
    private readonly PathGeometry head = Shapes.Polygon(new(-12, -84), new(12, -84), new(0, -66));
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-4, -86), new(-10, -100));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(4, -86), new(10, -100));

    public PrayingMantisPainter()
    {
        legPen = PainterPens.Pen(Limb, 2.5, scale);
        forelegPen = PainterPens.Pen(Limb, 4.0, scale);
        antennaPen = PainterPens.Pen(Limb, 1.0, scale);
    }

    public Color BodyColor => Green;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 26), 16, 54);

        LegPainter.DrawLegPair(dc, legPen, new(-6, -12), new(-40, -30), new(-56, -4), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-6, 6), new(-36, 30), new(-42, 62), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        dc.DrawGeometry(green, outline, abdomen);
        dc.DrawLine(outline, new Point(0, -18), new Point(0, 66));
        dc.DrawGeometry(null, vein, leftVein);
        dc.DrawGeometry(null, vein, rightVein);
        dc.DrawRoundedRectangle(green, outline, new Rect(-4, -66, 8, 46), 3, 3);
        dc.DrawGeometry(null, forelegPen, leftForeleg);
        dc.DrawGeometry(null, forelegPen, rightForeleg);
        dc.DrawLine(outline, new Point(-22, -44), new Point(-12, -26));
        dc.DrawLine(outline, new Point(22, -44), new Point(12, -26));
        dc.DrawGeometry(green, outline, head);
        dc.DrawEllipse(dark, null, new Point(-11, -84), 4, 4);
        dc.DrawEllipse(dark, null, new Point(11, -84), 4, 4);
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-4, -86), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(4, -86), bug.LegPhase, Math.PI);
        dc.Pop();

        dc.Pop();
    }
}
```

- [ ] **Step 2: Register it**

In `BugPainterRegistry.cs` replace `[SpeciesId.PrayingMantis] = Placeholder,` with `[SpeciesId.PrayingMantis] = new PrayingMantisPainter(),`.

- [ ] **Step 3: Build, look, commit**

Run the build and the app. Expected: a long green mantis with folded forelegs moves slowly and pauses often.

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): praying mantis painter

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 19: Seven-spot ladybug

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/SevenSpotLadybugPainter.cs`
- Modify: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

- [ ] **Step 1: Write SevenSpotLadybugPainter**

SVG group `seven-spot-ladybug`. Body: head top (-42 - 6 = -48) to elytra bottom (6 + 36 = 42), 90 units.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class SevenSpotLadybugPainter : IBugPainter
{
    private const double SpecimenBodyLength = 90.0;
    private const double LegAmplitudeDegrees = 8.0;

    private static readonly Color Red = PainterPens.Hex("#d8321f");
    private static readonly Color Black = PainterPens.Hex("#1a1a1a");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.SevenSpotLadybug).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush red = PainterPens.Brush(Red);
    private readonly SolidColorBrush black = PainterPens.Brush(Black);
    private readonly SolidColorBrush white = PainterPens.Brush(PainterPens.Hex("#f2f2f2"));
    private readonly Pen outline = PainterPens.Pen(PainterPens.Hex("#8e1b0f"), 1.0, 1.0);
    private readonly Pen seam = PainterPens.Pen(PainterPens.Hex("#8e1b0f"), 1.2, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-4, -46), new(-10, -56));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(4, -46), new(10, -56));
    private readonly (Point Center, double Radius)[] spots =
    [
        (new(0, -24), 4.5), (new(-14, -12), 5), (new(14, -12), 5),
        (new(-24, 10), 5), (new(24, 10), 5), (new(-10, 28), 5), (new(10, 28), 5),
    ];

    public SevenSpotLadybugPainter()
    {
        legPen = PainterPens.Pen(Black, 2.0, scale);
        antennaPen = PainterPens.Pen(Black, 1.2, scale);
    }

    public Color BodyColor => Red;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 10), 38, 38);

        LegPainter.DrawLegPair(dc, legPen, new(-14, -18), new(-30, -30), new(-36, -20), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-24, -4), new(-44, -8), new(-50, 4), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-20, 14), new(-38, 26), new(-40, 42), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-4, -46), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(4, -46), bug.LegPhase, Math.PI);
        dc.DrawEllipse(red, outline, new Point(0, 6), 36, 36);
        dc.DrawLine(seam, new Point(0, -28), new Point(0, 42));
        foreach (var (center, radius) in spots)
        {
            dc.DrawEllipse(black, null, center, radius, radius);
        }

        dc.DrawEllipse(black, null, new Point(0, -30), 22, 10);
        dc.DrawEllipse(white, null, new Point(-12, -31), 3, 3);
        dc.DrawEllipse(white, null, new Point(12, -31), 3, 3);
        dc.DrawEllipse(black, null, new Point(0, -42), 9, 6);
        dc.Pop();

        dc.Pop();
    }
}
```

- [ ] **Step 2: Register it**

Replace `[SpeciesId.SevenSpotLadybug] = Placeholder,` with `[SpeciesId.SevenSpotLadybug] = new SevenSpotLadybugPainter(),`.

- [ ] **Step 3: Build, look, commit**

Expected: a red domed beetle with seven black spots and a black head shield.

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): seven-spot ladybug painter

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 20: Stag beetle

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/StagBeetlePainter.cs`
- Modify: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

- [ ] **Step 1: Write StagBeetlePainter**

SVG group `stag-beetle`. Body: head top (-52) to elytra bottom (18 + 40 = 58), 110 units; the antlers extend beyond and are excluded from the body length.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class StagBeetlePainter : IBugPainter
{
    private const double SpecimenBodyLength = 110.0;
    private const double LegAmplitudeDegrees = 7.0;

    private static readonly Color Brown = PainterPens.Hex("#2b1b12");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.StagBeetle).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush brown = PainterPens.Brush(Brown);
    private readonly SolidColorBrush black = PainterPens.Brush(Colors.Black);
    private readonly Pen outline = PainterPens.Pen(PainterPens.Hex("#150c07"), 1.0, 1.0);
    private readonly Pen seam = PainterPens.Pen(PainterPens.Hex("#5a3a26"), 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly Pen antlerPen;
    private readonly Pen tinePen;
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-16, -50), new(-26, -58));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(16, -50), new(26, -58));
    private readonly PathGeometry leftAntler = Shapes.Cubic(new(-8, -52), new(-18, -62), new(-24, -78), new(-16, -94));
    private readonly PathGeometry rightAntler = Shapes.Cubic(new(8, -52), new(18, -62), new(24, -78), new(16, -94));
    private readonly PathGeometry leftTip = Shapes.Quadratic(new(-16, -94), new(-12, -100), new(-4, -98));
    private readonly PathGeometry rightTip = Shapes.Quadratic(new(16, -94), new(12, -100), new(4, -98));

    public StagBeetlePainter()
    {
        legPen = PainterPens.Pen(Brown, 3.0, scale);
        antennaPen = PainterPens.Pen(Brown, 1.5, scale);
        antlerPen = PainterPens.Pen(Brown, 5.0, scale);
        tinePen = PainterPens.Pen(Brown, 3.0, scale);
    }

    public Color BodyColor => Brown;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 20), 28, 46);

        LegPainter.DrawLegPair(dc, legPen, new(-14, -24), new(-40, -44), new(-48, -28), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-18, -4), new(-52, -10), new(-58, 14), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-16, 20), new(-44, 40), new(-46, 66), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-16, -50), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(16, -50), bug.LegPhase, Math.PI);
        dc.DrawGeometry(null, antlerPen, leftAntler);
        dc.DrawGeometry(null, antlerPen, rightAntler);
        dc.DrawLine(tinePen, new Point(-19, -76), new Point(-9, -80));
        dc.DrawLine(tinePen, new Point(19, -76), new Point(9, -80));
        dc.DrawGeometry(null, tinePen, leftTip);
        dc.DrawGeometry(null, tinePen, rightTip);
        dc.DrawEllipse(brown, outline, new Point(0, 18), 24, 40);
        dc.DrawLine(seam, new Point(0, -22), new Point(0, 58));
        dc.DrawRoundedRectangle(brown, outline, new Rect(-19, -40, 38, 22), 7, 7);
        dc.DrawRoundedRectangle(brown, null, new Rect(-14, -52, 28, 14), 3, 3);
        dc.DrawEllipse(black, null, new Point(-13, -46), 2.5, 2.5);
        dc.DrawEllipse(black, null, new Point(13, -46), 2.5, 2.5);
        dc.Pop();

        dc.Pop();
    }
}
```

- [ ] **Step 2: Register it**

Replace `[SpeciesId.StagBeetle] = Placeholder,` with `[SpeciesId.StagBeetle] = new StagBeetlePainter(),`.

- [ ] **Step 3: Build, look, commit**

Expected: a slow dark beetle with two forward-curving antlers.

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): stag beetle painter

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 21: House spider

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/HouseSpiderPainter.cs`
- Modify: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

- [ ] **Step 1: Write HouseSpiderPainter**

SVG group `house-spider`. Body: cephalothorax top (-16 - 12 = -28) to abdomen bottom (14 + 24 = 38), 66 units. Eight legs, pairs 1 and 3 offset 0, pairs 2 and 4 offset 0.5. No antennae.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class HouseSpiderPainter : IBugPainter
{
    private const double SpecimenBodyLength = 66.0;
    private const double LegAmplitudeDegrees = 7.0;

    private static readonly Color Brown = PainterPens.Hex("#4a3b2f");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.HouseSpider).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush brown = PainterPens.Brush(Brown);
    private readonly SolidColorBrush abdomen = PainterPens.Brush(PainterPens.Hex("#5a4838"));
    private readonly SolidColorBrush black = PainterPens.Brush(Colors.Black);
    private readonly Pen outline = PainterPens.Pen(PainterPens.Hex("#33261b"), 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen palpPen;
    private readonly Pen chevronPen;
    private readonly (Point Center, double Radius)[] eyes =
    [
        (new(-4, -24), 1.5), (new(0, -25), 1.8), (new(4, -24), 1.5), (new(-7, -21), 1.3), (new(7, -21), 1.3),
    ];

    public HouseSpiderPainter()
    {
        legPen = PainterPens.Pen(Brown, 2.5, scale);
        palpPen = PainterPens.Pen(Brown, 2.0, scale);
        chevronPen = PainterPens.Pen(PainterPens.Hex("#a08a6a"), 2.0, scale);
    }

    public Color BodyColor => Brown;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 14), 22, 28);

        LegPainter.DrawLegPair(dc, legPen, new(-8, -26), new(-34, -66), new(-52, -84), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-11, -20), new(-50, -46), new(-78, -50), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-12, -12), new(-54, -6), new(-80, 8), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-10, -6), new(-40, 26), new(-52, 60), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        dc.DrawLine(palpPen, new Point(-4, -27), new Point(-7, -35));
        dc.DrawLine(palpPen, new Point(4, -27), new Point(7, -35));
        dc.DrawEllipse(abdomen, outline, new Point(0, 14), 18, 24);
        dc.DrawLine(chevronPen, new Point(0, -4), new Point(-8, 6));
        dc.DrawLine(chevronPen, new Point(0, -4), new Point(8, 6));
        dc.DrawLine(chevronPen, new Point(0, 8), new Point(-7, 16));
        dc.DrawLine(chevronPen, new Point(0, 8), new Point(7, 16));
        dc.DrawLine(chevronPen, new Point(0, 20), new Point(-5, 27));
        dc.DrawLine(chevronPen, new Point(0, 20), new Point(5, 27));
        dc.DrawEllipse(brown, outline, new Point(0, -16), 12, 12);
        foreach (var (center, radius) in eyes)
        {
            dc.DrawEllipse(black, null, center, radius, radius);
        }

        dc.Pop();

        dc.Pop();
    }
}
```

- [ ] **Step 2: Register it**

Replace `[SpeciesId.HouseSpider] = Placeholder,` with `[SpeciesId.HouseSpider] = new HouseSpiderPainter(),`.

- [ ] **Step 3: Build, look, commit**

Expected: an eight-legged spider that darts and stops; legs alternate in two groups of four.

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): house spider painter

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 22: Centipede

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/CentipedePainter.cs`
- Modify: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

- [ ] **Step 1: Write CentipedePainter**

SVG group `centipede`. Body: head top (-72 - 8 = -80) to last segment bottom (59 + 6.5 = 65.5), 145.5 units. Nine animated leg pairs at offsets `0.125 * i`, one static longer hind pair.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class CentipedePainter : IBugPainter
{
    private const double SpecimenBodyLength = 145.5;
    private const double LegAmplitudeDegrees = 10.0;
    private const int AnimatedPairs = 9;
    private const double SegmentSpacing = 13.0;
    private const double FirstSegmentY = -58.0;

    private static readonly Color Body = PainterPens.Hex("#b5702c");
    private static readonly Color Dark = PainterPens.Hex("#7a4519");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.Centipede).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush body = PainterPens.Brush(Body);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly SolidColorBrush black = PainterPens.Brush(Colors.Black);
    private readonly Pen outline = PainterPens.Pen(Dark, 1.0, 1.0);
    private readonly Pen legPen;
    private readonly Pen hindLegPen;
    private readonly Pen antennaPen;
    private readonly PathGeometry leftHindLeg = Shapes.Polyline(new(-8, 59), new(-16, 72), new(-20, 80));
    private readonly PathGeometry rightHindLeg = Shapes.Polyline(new(8, 59), new(16, 72), new(20, 80));
    private readonly PathGeometry leftAntenna = Shapes.Quadratic(new(-5, -78), new(-20, -86), new(-30, -92));
    private readonly PathGeometry rightAntenna = Shapes.Quadratic(new(5, -78), new(20, -86), new(30, -92));

    public CentipedePainter()
    {
        legPen = PainterPens.Pen(PainterPens.Hex("#d9a441"), 2.0, scale);
        hindLegPen = PainterPens.Pen(Body, 2.2, scale);
        antennaPen = PainterPens.Pen(Dark, 1.5, scale);
    }

    public Color BodyColor => Body;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 4), 14, 70);

        for (int i = 0; i < AnimatedPairs; i++)
        {
            double y = FirstSegmentY + SegmentSpacing * i;
            LegPainter.DrawLegPair(
                dc, legPen, new(-8, y), new(-18, y + 5), new(-24, y + 14),
                LegPainter.Swing(bug.LegPhase, 0.125 * i, LegAmplitudeDegrees));
        }

        dc.DrawGeometry(null, hindLegPen, leftHindLeg);
        dc.DrawGeometry(null, hindLegPen, rightHindLeg);

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-5, -78), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(5, -78), bug.LegPhase, Math.PI);
        for (int i = 0; i < AnimatedPairs; i++)
        {
            dc.DrawEllipse(body, outline, new Point(0, FirstSegmentY + SegmentSpacing * i), 9, 7);
        }

        dc.DrawEllipse(body, outline, new Point(0, 59), 8, 6.5);
        dc.DrawEllipse(dark, null, new Point(0, -72), 9, 8);
        dc.DrawEllipse(black, null, new Point(-4, -75), 1.5, 1.5);
        dc.DrawEllipse(black, null, new Point(4, -75), 1.5, 1.5);
        dc.Pop();

        dc.Pop();
    }
}
```

- [ ] **Step 2: Register it**

Replace `[SpeciesId.Centipede] = Placeholder,` with `[SpeciesId.Centipede] = new CentipedePainter(),`.

- [ ] **Step 3: Build, look, commit**

Expected: a segmented orange centipede whose yellow legs ripple in a wave along the body.

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): centipede painter with metachronal leg wave

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 23: Stink bug

**Files:**
- Create: `src/ScreenBugs/Rendering/Painters/StinkBugPainter.cs`
- Modify: `src/ScreenBugs/Rendering/BugPainterRegistry.cs`

- [ ] **Step 1: Write StinkBugPainter**

SVG group `stink-bug`. Body: head tip (-48) to shield bottom (54), 102 units. The antennae have light bands, so each antenna is drawn under one rotation using `BodyMotion.AntennaAngle` directly.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class StinkBugPainter : IBugPainter
{
    private const double SpecimenBodyLength = 102.0;
    private const double LegAmplitudeDegrees = 7.0;

    private static readonly Color Olive = PainterPens.Hex("#6b8a3a");
    private static readonly Color Dark = PainterPens.Hex("#3f5a20");

    private readonly double scale = SpeciesCatalog.Get(SpeciesId.StinkBug).BodyLength / SpecimenBodyLength;
    private readonly SolidColorBrush olive = PainterPens.Brush(Olive);
    private readonly SolidColorBrush dark = PainterPens.Brush(Dark);
    private readonly SolidColorBrush scutellum = PainterPens.Brush(PainterPens.Hex("#7d9a48"));
    private readonly SolidColorBrush eye = PainterPens.Brush(PainterPens.Hex("#1a1a1a"));
    private readonly Pen outline = PainterPens.Pen(Dark, 1.0, 1.0);
    private readonly Pen scutellumOutline = PainterPens.Pen(Dark, 0.8, 1.0);
    private readonly Pen legPen;
    private readonly Pen antennaPen;
    private readonly Pen bandPen;
    private readonly PathGeometry shield = Shapes.Figure(
        new(0, -48), closed: true,
        Shapes.Line(new(12, -34)), Shapes.Line(new(34, -20)), Shapes.Line(new(32, 8)),
        Shapes.Quad(new(22, 44), new(0, 54)), Shapes.Quad(new(-22, 44), new(-32, 8)),
        Shapes.Line(new(-34, -20)), Shapes.Line(new(-12, -34)));
    private readonly PathGeometry scutellumShape = Shapes.Polygon(new(-18, -18), new(18, -18), new(0, 22));
    private readonly PathGeometry leftAntenna = Shapes.Polyline(new(-4, -46), new(-16, -64), new(-22, -82));
    private readonly PathGeometry rightAntenna = Shapes.Polyline(new(4, -46), new(16, -64), new(22, -82));
    private readonly Point[] mottle =
    [
        new(-22, -8), new(24, -4), new(-20, 14), new(22, 18), new(-8, 36), new(10, 38), new(0, -30),
    ];

    public StinkBugPainter()
    {
        legPen = PainterPens.Pen(PainterPens.Hex("#4f6a2c"), 2.5, scale);
        antennaPen = PainterPens.Pen(Dark, 2.0, scale);
        bandPen = PainterPens.Pen(PainterPens.Hex("#c9b16a"), 2.4, scale);
    }

    public Color BodyColor => Olive;

    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 8), 36, 50);

        LegPainter.DrawLegPair(dc, legPen, new(-16, -22), new(-38, -40), new(-50, -26), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-28, -4), new(-56, -6), new(-62, 16), LegPainter.Swing(bug.LegPhase, 0.5, LegAmplitudeDegrees));
        LegPainter.DrawLegPair(dc, legPen, new(-24, 18), new(-50, 34), new(-54, 58), LegPainter.Swing(bug.LegPhase, 0.0, LegAmplitudeDegrees));

        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        DrawBandedAntenna(dc, leftAntenna, new Point(-4, -46), 1, bug.LegPhase, 0.0);
        DrawBandedAntenna(dc, rightAntenna, new Point(4, -46), -1, bug.LegPhase, Math.PI);
        dc.DrawGeometry(olive, outline, shield);
        dc.DrawLine(outline, new Point(-34, -20), new Point(34, -20));
        dc.DrawGeometry(scutellum, scutellumOutline, scutellumShape);
        foreach (var dot in mottle)
        {
            dc.DrawEllipse(dark, null, dot, 1.5, 1.5);
        }

        dc.DrawEllipse(eye, null, new Point(-7, -38), 2, 2);
        dc.DrawEllipse(eye, null, new Point(7, -38), 2, 2);
        dc.Pop();

        dc.Pop();
    }

    /// <summary>Antenna plus its two light bands, rotated together about the base. <paramref name="mirror"/> is 1 for the left antenna and -1 for the right.</summary>
    private void DrawBandedAntenna(DrawingContext dc, PathGeometry antenna, Point basePoint, int mirror, float legPhase, double side)
    {
        dc.PushTransform(new RotateTransform(BodyMotion.AntennaAngle(legPhase, side), basePoint.X, basePoint.Y));
        dc.DrawGeometry(null, antennaPen, antenna);
        dc.DrawLine(bandPen, new Point(-10 * mirror, -55), new Point(-14 * mirror, -61));
        dc.DrawLine(bandPen, new Point(-19 * mirror, -72), new Point(-21 * mirror, -78));
        dc.Pop();
    }
}
```

- [ ] **Step 2: Register it and drop the placeholder**

In `BugPainterRegistry.cs` replace `[SpeciesId.StinkBug] = Placeholder,` with `[SpeciesId.StinkBug] = new StinkBugPainter(),` and delete the `Placeholder` field. The registry now reads:

```csharp
using ScreenBugs.Rendering.Painters;

namespace ScreenBugs.Rendering;

/// <summary>Maps each <see cref="SpeciesId"/> to its painter.</summary>
public sealed class BugPainterRegistry
{
    private readonly Dictionary<SpeciesId, IBugPainter> painters = new()
    {
        [SpeciesId.HissingCockroach] = new HissingCockroachPainter(),
        [SpeciesId.BlackGardenAnt] = new BlackGardenAntPainter(),
        [SpeciesId.RedFireAnt] = new RedFireAntPainter(),
        [SpeciesId.PrayingMantis] = new PrayingMantisPainter(),
        [SpeciesId.SevenSpotLadybug] = new SevenSpotLadybugPainter(),
        [SpeciesId.StagBeetle] = new StagBeetlePainter(),
        [SpeciesId.HouseSpider] = new HouseSpiderPainter(),
        [SpeciesId.Centipede] = new CentipedePainter(),
        [SpeciesId.StinkBug] = new StinkBugPainter(),
    };

    public IBugPainter Get(SpeciesId id) => painters[id];
}
```

- [ ] **Step 3: Build, look, commit**

Expected: with the count temporarily at 10, all nine species appear over a couple of minutes and none is drawn as an ant placeholder.

```bash
git add src/ScreenBugs/Rendering
git commit -m "feat(rendering): stink bug painter; all nine species registered

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 24: Splat

**Files:**
- Create: `src/ScreenBugs/Rendering/SplatPainter.cs`
- Modify: `src/ScreenBugs/Overlay/BugCanvas.cs`

- [ ] **Step 1: Write SplatPainter**

Create `src/ScreenBugs/Rendering/SplatPainter.cs` (spec 6, SplatPainter). All random values come from `Random(bug.Seed)` so the shape is stable across frames; the caller has already translated to the bug's position.

```csharp
using System.Windows;
using System.Windows.Media;

namespace ScreenBugs.Rendering;

/// <summary>Draws a squashed bug as a fading blob of darkened body color with a few droplets.</summary>
public static class SplatPainter
{
    private const double DarkenFraction = 0.30;
    private const double BlobRadiusMin = 0.15;
    private const double BlobRadiusMax = 0.30;
    private const double BlobSpread = 0.35;
    private const double DropletRadiusMin = 0.04;
    private const double DropletRadiusMax = 0.08;
    private const double DropletDistanceMin = 0.5;
    private const double DropletDistanceMax = 0.9;

    public static void Paint(DrawingContext dc, Bug bug, Color bodyColor)
    {
        var random = new Random(bug.Seed);
        double size = bug.Species.BodyLength;
        var brush = PainterPens.Brush(PainterPens.Darken(bodyColor, DarkenFraction));

        dc.PushOpacity(Math.Clamp(1.0 - bug.SquashProgress, 0.0, 1.0));

        int blobs = random.Next(6, 10);
        for (int i = 0; i < blobs; i++)
        {
            double radius = size * Range(random, BlobRadiusMin, BlobRadiusMax);
            double distance = size * BlobSpread * random.NextDouble();
            dc.DrawEllipse(brush, null, Polar(random, distance), radius, radius);
        }

        int droplets = random.Next(3, 6);
        for (int i = 0; i < droplets; i++)
        {
            double radius = size * Range(random, DropletRadiusMin, DropletRadiusMax);
            double distance = size * Range(random, DropletDistanceMin, DropletDistanceMax);
            dc.DrawEllipse(brush, null, Polar(random, distance), radius, radius);
        }

        dc.Pop();
    }

    private static double Range(Random random, double min, double max) => min + (max - min) * random.NextDouble();

    private static Point Polar(Random random, double distance)
    {
        double angle = random.NextDouble() * Math.Tau;
        return new Point(Math.Cos(angle) * distance, Math.Sin(angle) * distance);
    }
}
```

- [ ] **Step 2: Draw splats from BugCanvas**

In `src/ScreenBugs/Overlay/BugCanvas.cs` replace the whole `OnRender` method with:

```csharp
    protected override void OnRender(DrawingContext dc)
    {
        if (Simulation is null)
        {
            return;
        }

        foreach (var bug in Simulation.Bugs)
        {
            var center = new Point(bug.Position.X, bug.Position.Y);
            var painter = painters.Get(bug.Species.Id);

            if (!bug.IsAlive)
            {
                dc.PushTransform(new TranslateTransform(center.X, center.Y));
                SplatPainter.Paint(dc, bug, painter.BodyColor);
                dc.Pop();
                continue;
            }

            dc.DrawEllipse(PainterPens.HitDisc, null, center, bug.Species.HitRadius, bug.Species.HitRadius);
            dc.PushTransform(new TranslateTransform(center.X, center.Y));
            dc.PushTransform(new RotateTransform(bug.Heading * 180.0 / Math.PI + 90.0));
            painter.Paint(dc, bug);
            dc.Pop();
            dc.Pop();
        }
    }
```

- [ ] **Step 3: Build, look, commit**

Run the app and squash a few bugs. Expected: each leaves a dark blob with droplets in its own body color that fades out over about a second and a half; a replacement walks in a few seconds later.

```bash
git add src/ScreenBugs/Rendering/SplatPainter.cs src/ScreenBugs/Overlay/BugCanvas.cs
git commit -m "feat(rendering): fading splat for squashed bugs

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

Chunk 4 is complete when all nine species render and squashing leaves a fading splat.

<!-- end of chunk 4 -->

## Chunk 5: Diagnostics, tray icon, application composition

### Task 25: Crash log and single-instance guard

**Files:**
- Create: `src/ScreenBugs/Diagnostics/CrashLog.cs`
- Create: `src/ScreenBugs/Diagnostics/SingleInstanceGuard.cs`

- [ ] **Step 1: Write CrashLog**

Create `src/ScreenBugs/Diagnostics/CrashLog.cs` (spec 9). It must never throw, because it runs inside the unhandled-exception handler.

```csharp
namespace ScreenBugs.Diagnostics;

/// <summary>Appends unhandled exceptions to %LocalAppData%\ScreenBugs\error.log.</summary>
public static class CrashLog
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenBugs",
        "error.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging must not throw from inside the exception handler.
        }
    }
}
```

- [ ] **Step 2: Write SingleInstanceGuard**

Create `src/ScreenBugs/Diagnostics/SingleInstanceGuard.cs` (spec 9). `AbandonedMutexException` means the previous owner died without releasing; treat that as acquired.

```csharp
namespace ScreenBugs.Diagnostics;

/// <summary>Named mutex so only one overlay runs per user session.</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\ScreenBugs.SingleInstance";

    private readonly Mutex mutex = new(initiallyOwned: false, MutexName);
    private bool acquired;

    /// <summary>True if this process now owns the instance slot; false if another instance holds it.</summary>
    public bool TryAcquire()
    {
        try
        {
            acquired = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        return acquired;
    }

    public void Dispose()
    {
        if (acquired)
        {
            mutex.ReleaseMutex();
            acquired = false;
        }

        mutex.Dispose();
    }
}
```

- [ ] **Step 3: Build and commit**

Run: `dotnet build src/ScreenBugs -nologo -v q`
Expected: `Build succeeded.`

```bash
git add src/ScreenBugs/Diagnostics
git commit -m "feat(app): crash log and single-instance mutex guard

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 26: Tray icon

**Files:**
- Create: `src/ScreenBugs/Tray/TrayIconFactory.cs`
- Create: `src/ScreenBugs/Tray/TrayIcon.cs`

These two files are the only ones that use `System.Drawing` and `System.Windows.Forms`, and they import those namespaces explicitly. Neither imports any WPF namespace, so `Color`, `Pen`, and `Point` are unambiguous inside them.

- [ ] **Step 1: Write TrayIconFactory**

Create `src/ScreenBugs/Tray/TrayIconFactory.cs` (spec 8: a 32x32 black ant drawn in code). The icon handle from `GetHicon` lives for the process lifetime; that single handle is intentionally not destroyed.

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScreenBugs.Tray;

/// <summary>Draws the tray glyph (a black ant seen from above) so no icon asset is needed.</summary>
public static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var pen = new Pen(Color.Black, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            graphics.DrawLine(pen, 12, 11, 4, 6);
            graphics.DrawLine(pen, 20, 11, 28, 6);
            graphics.DrawLine(pen, 12, 15, 3, 16);
            graphics.DrawLine(pen, 20, 15, 29, 16);
            graphics.DrawLine(pen, 12, 19, 5, 26);
            graphics.DrawLine(pen, 20, 19, 27, 26);
            graphics.DrawLine(pen, 14, 5, 10, 1);
            graphics.DrawLine(pen, 18, 5, 22, 1);

            graphics.FillEllipse(Brushes.Black, 11, 3, 10, 9);
            graphics.FillEllipse(Brushes.Black, 12, 11, 8, 9);
            graphics.FillEllipse(Brushes.Black, 10, 19, 12, 12);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
```

- [ ] **Step 2: Write TrayIcon**

Create `src/ScreenBugs/Tray/TrayIcon.cs` (spec 8). The explicit constructor is the allowed exception to the primary-constructor rule: it wires WinForms components and event handlers that need `this`.

```csharp
using System.Windows.Forms;

namespace ScreenBugs.Tray;

/// <summary>System tray icon with Pause/Resume, a Bugs count submenu, and Exit.</summary>
public sealed class TrayIcon : IDisposable
{
    private static readonly int[] CountChoices = [1, 3, 5, 10];

    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem pauseItem;
    private readonly ToolStripMenuItem[] countItems;

    public event Action? PauseToggled;

    public event Action<int>? BugCountChanged;

    public event Action? ExitRequested;

    public TrayIcon(int initialCount)
    {
        pauseItem = new ToolStripMenuItem("Pause");
        pauseItem.Click += (_, _) => PauseToggled?.Invoke();

        countItems = CountChoices
            .Select(count => new ToolStripMenuItem(count.ToString()) { Checked = count == initialCount, Tag = count })
            .ToArray();
        var bugsMenu = new ToolStripMenuItem("Bugs");
        foreach (var item in countItems)
        {
            item.Click += (_, _) => SelectCount(item);
            bugsMenu.DropDownItems.Add(item);
        }

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenuStrip();
        menu.Items.Add(pauseItem);
        menu.Items.Add(bugsMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        notifyIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "Screen Bugs",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    /// <summary>Swaps the first menu item between "Pause" and "Resume".</summary>
    public void SetPaused(bool paused) => pauseItem.Text = paused ? "Resume" : "Pause";

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

    private void SelectCount(ToolStripMenuItem selected)
    {
        foreach (var item in countItems)
        {
            item.Checked = item == selected;
        }

        BugCountChanged?.Invoke((int)selected.Tag!);
    }
}
```

- [ ] **Step 3: Build and commit**

Run: `dotnet build src/ScreenBugs -nologo -v q`
Expected: `Build succeeded.`

```bash
git add src/ScreenBugs/Tray
git commit -m "feat(tray): NotifyIcon with pause, bug count and exit menu, code-drawn glyph

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

### Task 27: Final application composition

**Files:**
- Modify: `src/ScreenBugs/App.xaml.cs`

- [ ] **Step 1: Replace App.xaml.cs**

Overwrite `src/ScreenBugs/App.xaml.cs` (spec 9). Pause hides the overlay and stops both timers; Resume reverses it. Hiding and re-showing a WPF window keeps its native handle, so the extended styles set in `OnSourceInitialized` survive.

```csharp
using System.Windows;
using System.Windows.Threading;
using ScreenBugs.Diagnostics;
using ScreenBugs.Overlay;
using ScreenBugs.Tray;

namespace ScreenBugs;

public partial class App : Application
{
    private const int InitialBugCount = 3;

    private SingleInstanceGuard? instanceGuard;
    private TrayIcon? trayIcon;
    private OverlayWindow? overlay;
    private FrameLoop? frameLoop;
    private TopmostKeeper? topmostKeeper;
    private bool paused;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        instanceGuard = new SingleInstanceGuard();
        if (!instanceGuard.TryAcquire())
        {
            Shutdown();
            return;
        }

        var bounds = new Bounds((float)SystemParameters.PrimaryScreenWidth, (float)SystemParameters.PrimaryScreenHeight);
        var simulation = new BugSimulation(bounds, new SystemRandomSource()) { TargetCount = InitialBugCount };

        var window = new OverlayWindow();
        window.Surface.Simulation = simulation;
        window.Show();
        overlay = window;

        var clickThrough = new ClickThroughController(window.Handle);
        topmostKeeper = new TopmostKeeper(window.Handle);
        frameLoop = new FrameLoop(dt =>
        {
            Vector2? cursor = CursorTracker.GetCursorDips(window);
            simulation.Step(dt, cursor);
            clickThrough.Update(cursor is { } c && simulation.HitTest(c) is not null);
            window.Surface.Redraw();
        });

        trayIcon = new TrayIcon(InitialBugCount);
        trayIcon.PauseToggled += TogglePause;
        trayIcon.BugCountChanged += count => simulation.TargetCount = count;
        trayIcon.ExitRequested += () => Shutdown();

        frameLoop.Start();
        topmostKeeper.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIcon?.Dispose();
        instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private void TogglePause()
    {
        paused = !paused;
        trayIcon?.SetPaused(paused);
        if (paused)
        {
            frameLoop?.Stop();
            topmostKeeper?.Stop();
            overlay?.Hide();
        }
        else
        {
            overlay?.Show();
            frameLoop?.Start();
            topmostKeeper?.Start();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLog.Write(e.Exception);
        e.Handled = true;
        trayIcon?.Dispose();
        trayIcon = null;
        Shutdown();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build ScreenBugs.slnx -nologo -v q`
Expected: `Build succeeded.` with 0 warnings from `App.xaml.cs`.

- [ ] **Step 3: Run the full manual checklist (spec 10)**

Run: `dotnet run --project src/ScreenBugs -c Release`, then confirm each item. Record any that fail and fix before committing.

1. Launch: a tray icon with a black ant appears; within a few seconds three bugs walk in from the edges over the desktop.
2. Click and type in other apps: works everywhere except on a bug.
3. Move the cursor toward a bug: it runs away after a brief hesitation; a quick decisive click still squashes it.
4. Squash: a splat in the bug's color fades over about 1.5 s; a replacement walks in 3 to 8 s later.
5. Alt-Tab shows no Screen Bugs entry; after a squash, the app you were using still has focus (type a character to confirm).
6. Tray menu: Pause hides all bugs and the item reads Resume; Resume brings them back moving; Bugs 10 adds bugs from the edges; Bugs 1 leaves a single bug; Exit removes the tray icon and the process ends (check Task Manager).
7. Launch a second copy while the first runs (`dotnet run --project src/ScreenBugs -c Release` in another terminal): it exits immediately and no second tray icon appears.
8. With Bugs set to 10, CPU in Task Manager stays in the range measured in Task 16.

- [ ] **Step 4: Confirm the crash log path works**

Temporarily add `throw new InvalidOperationException("crash test");` as the first line of `TogglePause`, run, click Pause. Expected: the app exits without a Windows error dialog and `%LocalAppData%\ScreenBugs\error.log` contains the exception with a timestamp. Remove the line and rebuild.

- [ ] **Step 5: Run the unit tests one last time**

Run: `dotnet test ScreenBugs.slnx -nologo -v q`
Expected: `Passed!` with 0 failures.

- [ ] **Step 6: Commit**

```bash
git add src/ScreenBugs/App.xaml.cs
git commit -m "feat(app): tray-driven composition with pause, count, exit, single instance and crash log

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

Chunk 5 is complete, and so is the plan, when the checklist in Task 27 passes, `git status` is clean, and `dotnet run --project src/ScreenBugs -c Release` starts the finished app.

<!-- end of chunk 5 -->
