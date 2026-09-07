# Body Undulation Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the centipede snake and the two ants sway by giving each body segment (and its leg pair) its own sideways offset from a head-to-tail travelling wave driven by `LegPhase`; try the same on the mantis abdomen and keep it only if it looks right.

**Architecture:** One pure function, `BodyMotion.Undulate`, gives the offset for a point at a fraction along the body. A small `BodyWave` record per species maps a specimen Y to that fraction and grows the amplitude toward the tail. Painters look up one offset per specimen Y and apply it to both a segment and the legs drawn at that Y, which is what keeps legs attached. A `--filmstrip` mode in `tools/BugRenderer` renders eight phases side by side so every painter change is judged by eye before it is committed.

**Tech Stack:** .NET 10, C# 14, WPF `DrawingContext`, xUnit 2.9. Windows only.

**Spec:** `docs/superpowers/specs/2026-09-06-body-undulation-design.md`. Section numbers below ("spec 4.1") refer to it. Issue: GitHub #1.

**Conventions (from the user's global CLAUDE.md, mandatory):**
- Primary constructors; use the parameters directly, no `_field` copies, no null checks.
- One type per file, named for the type.

**Commits:** every message ends with the trailer `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. Subjects are lowercase conventional style, as in `git log`.

**Branch:** `feat/body-undulation`, already created from `main`; the spec is already committed on it. All paths are relative to `C:\Users\AddamBoord\source\repos\ScreenSavers`.

**Build and test commands.** `dotnet test` intermittently hangs on this machine when it also has to build, and piping its output makes it worse. Always build first, redirect to a file, and pass `-nodeReuse:false`. `artifacts/` is gitignored.

```bash
export MSBUILDDISABLENODEREUSE=1
mkdir -p artifacts
dotnet build tests/ScreenBugs.Tests -nologo -v q -nodeReuse:false > artifacts/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" artifacts/b.log
dotnet test tests/ScreenBugs.Tests -nologo -v q --no-build -nodeReuse:false
dotnet build src/ScreenBugs -nologo -v q -nodeReuse:false > artifacts/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" artifacts/b.log
```

To run one test class: add `--filter "FullyQualifiedName~BodyMotionTests"` to the `dotnet test` line.

**Starting point:** 116 tests pass on `main`. This plan adds 13.

**Looking at PNGs:** the filmstrips are the review. After each painter task, open the PNG with the Read tool (it renders images) and check the acceptance points listed in that task before committing.

---

## File structure

```
src/ScreenBugs/Rendering/
  BodyMotion.cs                      mod   add Undulate next to Bob
  BodyWave.cs                        new   per-species wave: Y -> alongBody, amplitude growth
  Painters/CentipedePainter.cs       mod   per-segment offsets, legs included (spec 4.1)
  Painters/AntGeometry.cs            mod   per-part sway, legs included (spec 4.2)
  Painters/PrayingMantisPainter.cs   mod   abdomen swing, trial (spec 4.3)
src/ScreenBugs.Core/
  ScreenBugs.Core.csproj             mod   InternalsVisibleTo BugRenderer (the only Core change)
tools/BugRenderer/
  Program.cs                         mod   --filmstrip [dir]
  SpecimenRenderer.cs                mod   share Measure, TargetSize, SavePng; fix the phase-0 comment
  FilmstripRenderer.cs               new   eight phases side by side, one PNG per species
tests/ScreenBugs.Tests/
  ScreenBugs.Tests.csproj            mod   net10.0-windows, UseWPF, x64, reference ScreenBugs
  BodyMotionTests.cs                 new
  BodyWaveTests.cs                   new
docs/
  superpowers/specs/2026-09-02-screen-bugs-design.md   mod   body-bob bullet
  images/bugs/Centipede.png, BlackGardenAnt.png, RedFireAnt.png (PrayingMantis.png if kept)   regenerated
README.md                            mod   BugRenderer line, test count
```

---

## Chunk 1: everything

### Task 1: Let the test project see the WPF assembly

The wave maths lives in `ScreenBugs` (WPF, `net10.0-windows`, x64). The test project targets plain `net10.0` and references only Core. This task retargets it and proves the existing suite still runs before any new code exists. It is the riskiest build change, so it goes alone.

**Files:**
- Modify: `tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj`

- [ ] **Step 1: Replace the csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Windows-only and x64 because the rendering tests reach into the WPF app assembly. -->
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <PlatformTarget>x64</PlatformTarget>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="System.Numerics" />
    <Using Include="ScreenBugs.Core.Simulation" />
    <Using Include="ScreenBugs.Core.Settings" />
    <Using Include="ScreenBugs.Rendering" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ScreenBugs.Core\ScreenBugs.Core.csproj" />
    <ProjectReference Include="..\..\src\ScreenBugs\ScreenBugs.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Build and run the existing suite**

Run the build-then-test commands from the header.
Expected: build exit code 0 with no `error` lines; `Passed! - Failed: 0, Passed: 116`.

If the build complains about `System.Drawing` or WinForms ambiguity, the app project's `<Using Remove>` lines are not inherited; add the same two `<Using Remove="System.Drawing" />` and `<Using Remove="System.Windows.Forms" />` lines to the test csproj. Do not add `UseWindowsForms`.

- [ ] **Step 3: Commit**

```bash
git add tests/ScreenBugs.Tests/ScreenBugs.Tests.csproj
git commit -m "test: target windows so the suite can reach the rendering assembly

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: `BodyMotion.Undulate`

Spec 3.1. A pure function next to `Bob`. Tests first.

**Files:**
- Create: `tests/ScreenBugs.Tests/BodyMotionTests.cs`
- Modify: `src/ScreenBugs/Rendering/BodyMotion.cs`

- [ ] **Step 1: Write the failing tests**

`tests/ScreenBugs.Tests/BodyMotionTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class BodyMotionTests
{
    // LegPhase is a float, so phase arithmetic is only good to about 1e-7.
    private const double Tolerance = 1e-6;

    [Fact]
    public void Bob_is_one_dip_at_twice_the_leg_frequency()
    {
        // Rigid species keep Bob exactly as it was: a full DIP at an eighth of a stride.
        Assert.Equal(1.0, BodyMotion.Bob(0.125f, 1.0), Tolerance);
        Assert.Equal(2.0, BodyMotion.Bob(0.125f, 0.5), Tolerance);
    }

    [Fact]
    public void Undulate_is_zero_at_the_head_at_phase_zero()
    {
        Assert.Equal(0.0, BodyMotion.Undulate(0f, 1.0, 0.0, 3.0, 1.0), Tolerance);
    }

    [Fact]
    public void Undulate_amplitude_is_in_dips_divided_by_the_painter_scale()
    {
        // A quarter stride is the crest at the head.
        Assert.Equal(3.0, BodyMotion.Undulate(0.25f, 1.0, 0.0, 3.0, 1.0), Tolerance);
        Assert.Equal(6.0, BodyMotion.Undulate(0.25f, 0.5, 0.0, 3.0, 1.0), Tolerance);
    }

    [Fact]
    public void Undulate_lags_toward_the_tail_so_the_crest_travels_head_to_tail()
    {
        // A point a quarter of the way down a one-wavelength body sees the wave a quarter
        // stride late: what the head did at phase 0.1, that point does at phase 0.35.
        double head = BodyMotion.Undulate(0.1f, 1.0, 0.0, 2.0, 1.0);
        double later = BodyMotion.Undulate(0.35f, 1.0, 0.25, 2.0, 1.0);
        Assert.Equal(head, later, Tolerance);
    }

    [Fact]
    public void Undulate_wavelengths_scale_the_lag()
    {
        // With two wavelengths along the body the same point lags half a stride.
        double head = BodyMotion.Undulate(0.1f, 1.0, 0.0, 2.0, 2.0);
        double later = BodyMotion.Undulate(0.6f, 1.0, 0.25, 2.0, 2.0);
        Assert.Equal(head, later, Tolerance);
    }

    [Fact]
    public void Undulate_peaks_at_the_amplitude_over_a_full_stride()
    {
        double peak = Enumerable.Range(0, 400)
            .Select(i => Math.Abs(BodyMotion.Undulate(i / 400f, 2.0, 0.6, 3.0, 1.0)))
            .Max();
        Assert.Equal(1.5, peak, 1e-3);
    }

    [Fact]
    public void Undulate_depends_on_phase_alone_so_a_paused_bug_holds_its_shape()
    {
        // No time input: while LegPhase stands still, so does the wave.
        Assert.Equal(
            BodyMotion.Undulate(0.3f, 1.0, 0.7, 3.0, 1.0),
            BodyMotion.Undulate(0.3f, 1.0, 0.7, 3.0, 1.0));
    }
}
```

- [ ] **Step 2: Build to verify it fails**

Run the test-project build command.
Expected: exit code 1 and `error CS0117: 'BodyMotion' does not contain a definition for 'Undulate'`.

- [ ] **Step 3: Add `Undulate`**

In `src/ScreenBugs/Rendering/BodyMotion.cs`, after `Bob`:

```csharp
    /// <summary>
    /// Sideways offset in specimen units for a point at <paramref name="alongBody"/> (0 at the
    /// head, 1 at the tail): amplitude times sin(2π(phase − wavelengths·alongBody)). Subtracting
    /// the position term makes the crest travel head to tail as the phase advances. One cycle
    /// per stride, so it locks to the leg wave rather than to <see cref="Bob"/>'s two.
    /// </summary>
    public static double Undulate(float legPhase, double scale, double alongBody, double amplitudeDips, double wavelengths) =>
        amplitudeDips / scale * Math.Sin(2.0 * Math.PI * (legPhase - wavelengths * alongBody));
```

- [ ] **Step 4: Build and run the tests**

Run the build-then-test commands with `--filter "FullyQualifiedName~BodyMotionTests"`.
Expected: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs/Rendering/BodyMotion.cs tests/ScreenBugs.Tests/BodyMotionTests.cs
git commit -m "feat(render): per-segment undulation next to the whole-body bob

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: `BodyWave`

Spec 3.2. Maps a specimen Y to a fraction along the body, grows amplitude head to tail, clamps beyond the ends.

**Files:**
- Create: `tests/ScreenBugs.Tests/BodyWaveTests.cs`
- Create: `src/ScreenBugs/Rendering/BodyWave.cs`

- [ ] **Step 1: Write the failing tests**

`tests/ScreenBugs.Tests/BodyWaveTests.cs`:

```csharp
namespace ScreenBugs.Tests;

public sealed class BodyWaveTests
{
    // Scale 1 throughout, so every offset is in DIPs.
    private static readonly BodyWave Wave = new(HeadY: -70, TailY: 60, HeadAmplitudeDips: 0.5, TailAmplitudeDips: 3.0, Wavelengths: 1.0);

    private static double Peak(double y) => Enumerable.Range(0, 400)
        .Select(i => Math.Abs(Wave.OffsetAt(y, i / 400f, 1.0)))
        .Max();

    [Fact]
    public void Peak_swing_at_the_head_is_the_head_amplitude()
    {
        Assert.Equal(0.5, Peak(-70), 1e-3);
    }

    [Fact]
    public void Peak_swing_at_the_tail_is_the_tail_amplitude()
    {
        Assert.Equal(3.0, Peak(60), 1e-3);
    }

    [Fact]
    public void Peak_swing_halfway_is_the_mean_of_the_two()
    {
        Assert.Equal(1.75, Peak(-5), 1e-3);
    }

    [Fact]
    public void Points_beyond_either_end_move_with_that_end()
    {
        // Antennae sit ahead of the head and hind legs behind the tail; both must stay attached.
        Assert.Equal(Wave.OffsetAt(-70, 0.3f, 1.0), Wave.OffsetAt(-90, 0.3f, 1.0));
        Assert.Equal(Wave.OffsetAt(60, 0.3f, 1.0), Wave.OffsetAt(80, 0.3f, 1.0));
    }

    [Fact]
    public void Equal_head_and_tail_amplitude_reduces_to_undulate()
    {
        var flat = new BodyWave(HeadY: 0, TailY: 100, HeadAmplitudeDips: 2.0, TailAmplitudeDips: 2.0, Wavelengths: 1.5);
        Assert.Equal(
            BodyMotion.Undulate(0.4f, 0.5, 0.25, 2.0, 1.5),
            flat.OffsetAt(25, 0.4f, 0.5),
            1e-9);
    }
}
```

- [ ] **Step 2: Build to verify it fails**

Expected: `error CS0246: The type or namespace name 'BodyWave' could not be found`.

- [ ] **Step 3: Create `BodyWave`**

`src/ScreenBugs/Rendering/BodyWave.cs`:

```csharp
namespace ScreenBugs.Rendering;

/// <summary>
/// The undulation of one species in its specimen space: which Y is the head, which is the tail,
/// how far each end swings, and how many wavelengths fit along the body. Amplitude grows
/// linearly from head to tail so the head tracks straight while the rear swings widest.
/// </summary>
public sealed record BodyWave(double HeadY, double TailY, double HeadAmplitudeDips, double TailAmplitudeDips, double Wavelengths)
{
    /// <summary>
    /// Sideways offset in specimen units for a body point at specimen <paramref name="y"/>.
    /// Y beyond either end is clamped, so antennae move with the head and hind legs with the tail.
    /// </summary>
    public double OffsetAt(double y, float legPhase, double scale)
    {
        double along = Math.Clamp((y - HeadY) / (TailY - HeadY), 0.0, 1.0);
        double amplitude = HeadAmplitudeDips + ((TailAmplitudeDips - HeadAmplitudeDips) * along);
        return BodyMotion.Undulate(legPhase, scale, along, amplitude, Wavelengths);
    }
}
```

- [ ] **Step 4: Build and run the tests**

Filter `BodyWaveTests`. Expected: `Passed: 5`. Then run the whole suite: `Passed: 129`.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs/Rendering/BodyWave.cs tests/ScreenBugs.Tests/BodyWaveTests.cs
git commit -m "feat(render): BodyWave maps a specimen y onto the undulation

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: Filmstrip mode in BugRenderer

Spec 5. Built before the painters change so every painter change is reviewed with it. `Bug.LegPhase` has an internal setter, so Core exposes internals to the tool.

**Files:**
- Modify: `src/ScreenBugs.Core/ScreenBugs.Core.csproj`
- Modify: `tools/BugRenderer/SpecimenRenderer.cs`
- Create: `tools/BugRenderer/FilmstripRenderer.cs`
- Modify: `tools/BugRenderer/Program.cs`

- [ ] **Step 1: Expose Core internals to the tool**

In `src/ScreenBugs.Core/ScreenBugs.Core.csproj`:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="ScreenBugs.Tests" />
    <!-- The filmstrip sets Bug.LegPhase directly; nothing else in the tool needs internals. -->
    <InternalsVisibleTo Include="BugRenderer" />
  </ItemGroup>
```

- [ ] **Step 2: Share the measure, size and save steps of `SpecimenRenderer`**

In `tools/BugRenderer/SpecimenRenderer.cs`:

Make `TargetSize` internal:

```csharp
    /// <summary>How many pixels the longest side of a specimen should occupy.</summary>
    internal const double TargetSize = 320.0;
```

Replace the comment above `var bug = new Bug(...)` in `Write`, which is no longer true for the undulating species:

```csharp
        // A bug at leg phase 0: every leg sits at its neutral swing. Species with a body wave
        // show the curve that phase gives them, which is exactly what the first frame of a
        // stride looks like on screen.
        var bug = new Bug(id: 0, SpeciesCatalog.Get(id), seed: 0);
```

Replace the measuring block in `Write` with a call to a new shared method, and the PNG save with another:

```csharp
        Rect bounds = Measure(painter, bug);
        double zoom = TargetSize / Math.Max(bounds.Width, bounds.Height);
        int width = (int)Math.Ceiling((bounds.Width * zoom) + (Padding * 2));
        int height = (int)Math.Ceiling((bounds.Height * zoom) + (Padding * 2));

        var bare = Render(painter, bug, bounds, zoom * Supersample, width * Supersample, height * Supersample);
        var outlined = OutlineCompositor.AddOutline(bare, OutlineRadius * Supersample);
        var final = Downscale(outlined, width, height);

        SavePng(final, path);
    }

    /// <summary>
    /// Paints once, unscaled, purely to learn how much room the drawing needs. The painter works
    /// in bug-local space around the body centre and every species covers a different extent
    /// once legs, antennae and shadow are counted, so the bounds cannot be predicted from body
    /// length alone.
    /// </summary>
    internal static Rect Measure(IBugPainter painter, Bug bug)
    {
        var measured = new DrawingVisual();
        using (var dc = measured.RenderOpen())
        {
            painter.Paint(dc, bug);
        }

        return measured.ContentBounds;
    }

    internal static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        encoder.Save(file);
    }
```

Delete the old inline measuring code and the old encoder block from `Write`; `Render` and `Downscale` stay private and unchanged.

- [ ] **Step 3: Create `FilmstripRenderer`**

`tools/BugRenderer/FilmstripRenderer.cs`:

```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenBugs.Core.Simulation;
using ScreenBugs.Rendering;

namespace BugRenderer;

/// <summary>
/// Draws one species across a full stride, eight frames side by side, so the gait and the body
/// wave can be judged without watching the live overlay. A review aid only: nothing here ships.
/// </summary>
public static class FilmstripRenderer
{
    private const int Frames = 8;
    private const double Gap = 8.0;
    private const double Padding = 4.0;

    public static void Write(string path, SpeciesId id, BugPainterRegistry registry)
    {
        var painter = registry.Get(id);
        var species = SpeciesCatalog.Get(id);

        var bugs = new Bug[Frames];
        for (int k = 0; k < Frames; k++)
        {
            bugs[k] = new Bug(id: 0, species, seed: 0) { LegPhase = (float)k / Frames };
        }

        // One set of bounds for every frame, so the wave shows as displacement rather than as
        // each frame re-centring on its own extent.
        Rect bounds = Rect.Empty;
        foreach (var bug in bugs)
        {
            bounds.Union(SpecimenRenderer.Measure(painter, bug));
        }

        double zoom = SpecimenRenderer.TargetSize / Math.Max(bounds.Width, bounds.Height);
        double frameWidth = Math.Ceiling(bounds.Width * zoom);
        double frameHeight = Math.Ceiling(bounds.Height * zoom);
        int width = (int)Math.Ceiling((Padding * 2) + (Frames * frameWidth) + ((Frames - 1) * Gap));
        int height = (int)Math.Ceiling((Padding * 2) + frameHeight);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Opaque white so the strip reads the same in any image viewer, light or dark.
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            for (int k = 0; k < Frames; k++)
            {
                dc.PushTransform(new TranslateTransform(Padding + (k * (frameWidth + Gap)), Padding));
                dc.PushTransform(new ScaleTransform(zoom, zoom));
                dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
                painter.Paint(dc, bugs[k]);
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        SpecimenRenderer.SavePng(target, path);
    }
}
```

- [ ] **Step 4: Add the `--filmstrip` switch**

Replace everything from the `/// <summary>` above `Program` down to the closing brace of `Main` in `tools/BugRenderer/Program.cs`; the `FindRepositoryRoot` helper and the class closing brace stay:

```csharp
/// <summary>
/// Regenerates the specimen images the README uses, or, with <c>--filmstrip [dir]</c>, renders
/// each species across a stride for review.
/// Run: dotnet run --project tools/BugRenderer
/// Run: dotnet run --project tools/BugRenderer -- --filmstrip
/// </summary>
public static class Program
{
    // WPF's rendering objects have thread affinity and require a single-threaded apartment,
    // which a console app does not get by default.
    [STAThread]
    public static void Main(string[] args)
    {
        string root = FindRepositoryRoot();
        var registry = new BugPainterRegistry();

        if (args.Length > 0 && args[0] == "--filmstrip")
        {
            // artifacts/ is gitignored: filmstrips are looked at, never committed.
            string filmstrips = args.Length > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(root, "artifacts", "filmstrips");
            WriteAll(filmstrips, registry, FilmstripRenderer.Write, "filmstrips");
            return;
        }

        WriteAll(Path.Combine(root, "docs", "images", "bugs"), registry, SpecimenRenderer.Write, "specimens");
    }

    private static void WriteAll(
        string directory, BugPainterRegistry registry, Action<string, SpeciesId, BugPainterRegistry> write, string what)
    {
        foreach (var species in SpeciesCatalog.All)
        {
            string path = Path.Combine(directory, $"{species.Id}.png");
            write(path, species.Id, registry);
            Console.WriteLine($"{species.Id,-20} {new FileInfo(path).Length,7:N0} bytes");
        }

        Console.WriteLine($"\nWrote {SpeciesCatalog.All.Count} {what} to {directory}");
    }
```

- [ ] **Step 5: Build and run both modes**

```bash
dotnet build tools/BugRenderer -nologo -v q -nodeReuse:false > artifacts/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" artifacts/b.log
dotnet run --project tools/BugRenderer -- --filmstrip
dotnet run --project tools/BugRenderer
git status --short docs/images
```

Expected: exit 0; nine filmstrip PNGs under `artifacts/filmstrips/`; the specimen run reports nine files and `git status` shows **no** change under `docs/images` (the painters have not changed yet, so this proves the refactor is behaviour-preserving; if any file differs, open it beside the committed one and confirm it is pixel-identical before continuing, and note the encoder instability in the commit).

- [ ] **Step 6: Look at one filmstrip**

Read `artifacts/filmstrips/Centipede.png`. Expected: eight centipedes in a row, legs at different swings, body dead straight in every frame (the "before" picture).

- [ ] **Step 7: Commit**

```bash
git add src/ScreenBugs.Core/ScreenBugs.Core.csproj tools/BugRenderer/
git commit -m "tools(bugrenderer): --filmstrip renders each species across a stride

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Centipede snakes

Spec 4.1. The flagship. Every segment and its leg pair get the offset for their own Y; `Bob` goes.

**Files:**
- Modify: `src/ScreenBugs/Rendering/Painters/CentipedePainter.cs`

- [ ] **Step 1: Add the wave and the head/tail constants**

Next to the other constants:

```csharp
    private const double HeadY = -72.0;
    private const double TailY = 59.0;

    /// <summary>Spec 4.1: one wavelength along the body, the tail swinging six times wider than the head.</summary>
    private static readonly BodyWave Wave = new(HeadY: HeadY, TailY: TailY, HeadAmplitudeDips: 0.5, TailAmplitudeDips: 3.0, Wavelengths: 1.0);
```

- [ ] **Step 2: Replace `Paint`**

```csharp
    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 4), 14, 70);

        // Each pair lags the one ahead by an eighth of a cycle, giving a wave along the body.
        // Each pair also rides the body wave at its own segment's Y, so it stays attached.
        for (int i = 0; i < AnimatedPairs; i++)
        {
            double y = FirstSegmentY + (SegmentSpacing * i);
            dc.PushTransform(new TranslateTransform(Offset(y, bug), 0));
            LegPainter.DrawLegPair(
                dc, legPen, new(-8, y), new(-18, y + 5), new(-24, y + 14),
                LegPainter.Swing(bug.LegPhase, 0.125 * i, LegAmplitudeDegrees));
            dc.Pop();
        }

        dc.PushTransform(new TranslateTransform(Offset(TailY, bug), 0));
        dc.DrawGeometry(null, hindLegPen, leftHindLeg);
        dc.DrawGeometry(null, hindLegPen, rightHindLeg);
        dc.Pop();

        dc.PushTransform(new TranslateTransform(Offset(HeadY, bug), 0));
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-5, -78), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(5, -78), bug.LegPhase, Math.PI);
        dc.Pop();

        for (int i = 0; i < AnimatedPairs; i++)
        {
            double y = FirstSegmentY + (SegmentSpacing * i);
            dc.DrawEllipse(body, outline, new Point(Offset(y, bug), y), 9, 7);
        }

        dc.DrawEllipse(body, outline, new Point(Offset(TailY, bug), TailY), 8, 6.5);

        dc.PushTransform(new TranslateTransform(Offset(HeadY, bug), 0));
        dc.DrawEllipse(dark, null, new Point(0, HeadY), 9, 8);
        dc.DrawEllipse(black, null, new Point(-4, -75), 1.5, 1.5);
        dc.DrawEllipse(black, null, new Point(4, -75), 1.5, 1.5);
        dc.Pop();

        dc.Pop();
    }

    private double Offset(double y, Bug bug) => Wave.OffsetAt(y, bug.LegPhase, scale);
```

The draw order (legs, hind legs, antennae, segments, tail, head, eyes) is the same as before, so nothing changes which part covers which.

- [ ] **Step 3: Build, render, look**

```bash
dotnet build tools/BugRenderer -nologo -v q -nodeReuse:false > artifacts/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" artifacts/b.log
dotnet run --project tools/BugRenderer -- --filmstrip
```

Read `artifacts/filmstrips/Centipede.png` and check:
- The body is an S that shifts along the body from frame to frame (one crest travelling toward the tail across the eight frames).
- The head barely moves; the tail swings widest.
- Every leg pair's hips sit on their segment in every frame. No leg root floats beside a segment.
- The swing is obvious but the animal still reads as one body; if the tail overshoots the leg tips or the S looks like a zigzag, lower `TailAmplitudeDips` (try 2.5); if the ripple is hard to see, raise it (try 3.5). Record the final value in the commit body.
- The leg wave travels tail to head (`+ 0.125 * i`) while the body wave travels head to tail. If the two visibly fight, spec 3.1 sanctions flipping the sign of `wavelengths * alongBody` in `Undulate`; doing so inverts the two lag tests in `BodyMotionTests` (swap the roles of head and later), so update them in the same commit and say why.

- [ ] **Step 4: Run the full suite**

Build then test. Expected: `Passed: 129`.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs/Rendering/Painters/CentipedePainter.cs
git commit -m "feat(render): the centipede snakes, legs riding their segments

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Ants sway

Spec 4.2. Four body parts and three leg pairs, each at its own offset; half a wavelength so it is a sway, not an S. Both ants share `AntGeometry`.

**Files:**
- Modify: `src/ScreenBugs/Rendering/Painters/AntGeometry.cs`

- [ ] **Step 1: Add the wave and the part Y constants**

Next to the other constants:

```csharp
    private const double HeadY = -36.0;
    private const double ThoraxY = -15.0;
    private const double PetioleY = -1.0;
    private const double GasterY = 18.0;

    /// <summary>Spec 4.2: half a wavelength, so the body sways rather than snakes; the gaster swings three times the head.</summary>
    private static readonly BodyWave Wave = new(HeadY: HeadY, TailY: GasterY, HeadAmplitudeDips: 0.25, TailAmplitudeDips: 0.75, Wavelengths: 0.5);
```

- [ ] **Step 2: Replace `Paint` and add the leg helper**

```csharp
    public void Paint(DrawingContext dc, Bug bug)
    {
        dc.PushTransform(new ScaleTransform(scale, scale));

        dc.DrawEllipse(PainterPens.Shadow, null, new Point(3, 22), 14, 19);

        DrawLegPair(dc, bug, new(-6, -22), new(-22, -36), new(-30, -24), 0.0);
        DrawLegPair(dc, bug, new(-7, -15), new(-26, -14), new(-34, -2), 0.5);
        DrawLegPair(dc, bug, new(-6, -8), new(-22, 4), new(-26, 20), 0.0);

        dc.PushTransform(new TranslateTransform(Offset(HeadY, bug), 0));
        dc.DrawGeometry(null, legPen, leftMandible);
        dc.DrawGeometry(null, legPen, rightMandible);
        BodyMotion.DrawAntenna(dc, antennaPen, leftAntenna, new Point(-5, -44), bug.LegPhase, 0.0);
        BodyMotion.DrawAntenna(dc, antennaPen, rightAntenna, new Point(5, -44), bug.LegPhase, Math.PI);
        dc.DrawEllipse(body, null, new Point(0, HeadY), 10, 10);
        dc.Pop();

        dc.DrawEllipse(body, null, new Point(Offset(ThoraxY, bug), ThoraxY), 6, 11);
        dc.DrawEllipse(body, null, new Point(Offset(PetioleY, bug), PetioleY), 3, 3);
        dc.DrawEllipse(body, null, new Point(Offset(GasterY, bug), GasterY), 12, 17);

        dc.Pop();
    }

    /// <summary>A leg pair rides the body wave at its hip's Y, so it stays on the thorax as the body sways.</summary>
    private void DrawLegPair(DrawingContext dc, Bug bug, Point hip, Point knee, Point foot, double groupOffset)
    {
        dc.PushTransform(new TranslateTransform(Offset(hip.Y, bug), 0));
        LegPainter.DrawLegPair(dc, legPen, hip, knee, foot, LegPainter.Swing(bug.LegPhase, groupOffset, LegAmplitudeDegrees));
        dc.Pop();
    }

    private double Offset(double y, Bug bug) => Wave.OffsetAt(y, bug.LegPhase, scale);
```

- [ ] **Step 3: Build, render, look**

Same build and filmstrip commands as Task 5. Read `artifacts/filmstrips/BlackGardenAnt.png` and `RedFireAnt.png`:
- The gaster swings gently side to side across the frames; the head much less.
- All six legs stay rooted on the thorax in every frame.
- It must be subtle: at the README's display size the sway should be noticeable side by side with `main`'s specimen and no more. If the gaster visibly detaches from the petiole line, lower `TailAmplitudeDips` (try 0.5).

- [ ] **Step 4: Run the full suite**

Expected: `Passed: 129`.

- [ ] **Step 5: Commit**

```bash
git add src/ScreenBugs/Rendering/Painters/AntGeometry.cs
git commit -m "feat(render): the ants sway, head to gaster

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: Mantis trial

Spec 4.3 and the rule in spec 2. Only the abdomen group swings, inside the existing `Bob` transform. This task ends in one of two commits: keep, or revert to `main` with the verdict in the message.

**Files:**
- Modify: `src/ScreenBugs/Rendering/Painters/PrayingMantisPainter.cs`

- [ ] **Step 1: Add the wave and translate the abdomen group**

Next to the other constants:

```csharp
    /// <summary>Spec 4.3, a trial: only the abdomen swings, from nothing at the head to under a DIP at the tip.</summary>
    private static readonly BodyWave Wave = new(HeadY: -84, TailY: 80, HeadAmplitudeDips: 0.0, TailAmplitudeDips: 0.8, Wavelengths: 0.5);

    /// <summary>Midpoint of the abdomen path (-22 to 80); the whole path is one rigid piece.</summary>
    private const double AbdomenY = 30.0;
```

In `Paint`, wrap the first four body draws (abdomen, centre line, two veins) that follow the `Bob` push:

```csharp
        dc.PushTransform(new TranslateTransform(BodyMotion.Bob(bug.LegPhase, scale), 0));
        dc.PushTransform(new TranslateTransform(Wave.OffsetAt(AbdomenY, bug.LegPhase, scale), 0));
        dc.DrawGeometry(green, outline, abdomen);
        dc.DrawLine(outline, new Point(0, -18), new Point(0, 66));
        dc.DrawGeometry(null, vein, leftVein);
        dc.DrawGeometry(null, vein, rightVein);
        dc.Pop();
        dc.DrawRoundedRectangle(green, outline, new Rect(-4, -66, 8, 46), 3, 3);
```

Everything after the rounded rectangle is unchanged.

- [ ] **Step 2: Build, render, look, decide**

Same commands. Read `artifacts/filmstrips/PrayingMantis.png`:
- Keep it if the abdomen reads as swinging behind a steady thorax and the join at the thorax (y about -20) does not visibly step sideways.
- Drop it if the abdomen looks like it is sliding off the thorax, or the motion reads as a wobble on an animal that is supposed to walk deliberately.

- [ ] **Step 3a: Keep**

```bash
git add src/ScreenBugs/Rendering/Painters/PrayingMantisPainter.cs
git commit -m "feat(render): the mantis abdomen swings behind a steady thorax

Tried per spec 4.3 and kept: <one line on why it reads well>.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

- [ ] **Step 3b: Drop**

```bash
git checkout main -- src/ScreenBugs/Rendering/Painters/PrayingMantisPainter.cs
git status --short   # should be clean
```

Then record the verdict in the spec so the trial is closed. In `docs/superpowers/specs/2026-09-06-body-undulation-design.md` section 2, change the mantis row's treatment to `Tried and dropped: <one line on why>` and commit:

```bash
git add docs/superpowers/specs/2026-09-06-body-undulation-design.md
git commit -m "docs(spec): mantis abdomen swing tried and dropped

<one line on why it read wrong>

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: Specimens and docs

Spec 7. Regenerate the README images, amend the v1 spec's body-bob rule, mention the flag in the README.

**Files:**
- Regenerate: `docs/images/bugs/*.png`
- Modify: `docs/superpowers/specs/2026-09-02-screen-bugs-design.md` (the "Body bob" bullet, about line 342)
- Modify: `README.md` (the developer tools block, about line 197, and the test count on line 187)

- [ ] **Step 1: Regenerate the specimens**

```bash
dotnet run --project tools/BugRenderer
git status --short docs/images
```

Expected: only `Centipede.png`, `BlackGardenAnt.png`, `RedFireAnt.png` (and `PrayingMantis.png` if Task 7 kept it) are modified. If a rigid species' PNG is also listed, open both versions and compare; a pixel-identical pair means the encoder is unstable on this machine and the file should be restored with `git checkout -- <file>`, not committed.

Read the new `docs/images/bugs/Centipede.png`: a gently curved centipede, legs on their segments.

- [ ] **Step 2: Amend the v1 spec**

Replace the "Body bob" bullet in `docs/superpowers/specs/2026-09-02-screen-bugs-design.md`:

```markdown
- Body bob: the body group is offset sideways by `1 DIP * sin(4 * PI * LegPhase)`
  so it sways with the steps. The centipede and the two ants instead offset each
  body part by a head-to-tail travelling wave; see
  `2026-09-06-body-undulation-design.md`.
```

If Task 7 kept the mantis, list it in that sentence too.

- [ ] **Step 3: README**

In the developer tools block:

```bash
dotnet run --project tools/IconGen      # app icon + installer wizard images
dotnet run --project tools/BugRenderer  # the specimen images in this README
dotnet run --project tools/BugRenderer -- --filmstrip   # each bug across a stride, for eyeballing gaits
```

And update the test count comment on the `dotnet test` line to (129).

- [ ] **Step 4: Commit**

```bash
git add docs/images/bugs docs/superpowers/specs/2026-09-02-screen-bugs-design.md README.md
git commit -m "docs: regenerate the curved specimens, note the wave in the v1 spec

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: Live check against the issue's acceptance list

Spec 8. The overlay is the only place three of the points can be judged.

- [ ] **Step 1: Build and run the app**

```bash
dotnet build src/ScreenBugs -nologo -v q -nodeReuse:false > artifacts/b.log 2>&1; echo $?; grep -E "error|Error\(s\)" artifacts/b.log
dotnet run --project src/ScreenBugs
```

If another instance is running, the single-instance guard exits immediately; exit the tray icon first.

- [ ] **Step 2: Check, in the Options dialog, with one row set to Centipede and one to Black garden ant**

- Centipede snakes across the screen, tail wider than head; legs stay on their segments.
- Ants sway subtly.
- Set the centipede row's speed slider to its maximum: the ripple speeds up with it.
- Wait for a bug to pause: the ripple stops while it is paused.
- Click a centipede mid-ripple: the splat appears where it was, nothing snaps.
- Set rows to Ladybug, Stink bug, Hissing cockroach, Stag beetle, House spider: each moves exactly as before (whole-body bob only).

- [ ] **Step 3: Exit the app from the tray**

- [ ] **Step 4: Report**

Full suite result, the final amplitude values, the mantis verdict, and anything on the acceptance list that did not hold. Then hand off with superpowers:finishing-a-development-branch. The PR body should note that the one Core change is an `InternalsVisibleTo` line, since the issue said "no changes to `ScreenBugs.Core`".
