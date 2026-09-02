# Screen Bugs: design spec

Date: 2026-09-02
Status: approved for planning

## 1. Overview

Screen Bugs is a Windows desktop toy. Animated, code-drawn bugs wander across the
primary monitor on top of everything else. The overlay is click-through, so the
user keeps working normally. Bugs run away when the cursor gets close, and a
decisive click on a bug squashes it. A system tray icon controls the app.

Goals for v1:

- Transparent, always-on-top, click-through overlay covering the primary monitor.
- Nine species drawn as vector geometry and animated procedurally (walking gait,
  body bob, antenna waggle).
- Random wandering with pauses, edge avoidance, flee-from-cursor, and squash.
- Tray icon with Pause/Resume, bug count (1/3/5/10), and Exit.
- Deterministic, unit-tested simulation with no UI dependency.

Non-goals for v1 (candidates for later):

- Multi-monitor support (v1 uses the primary monitor only).
- Persisted settings (count resets to 3 on each launch).
- Run at Windows startup.
- Sound effects.

## 2. Technology

- .NET 10, C# 14, WPF for the overlay, Windows Forms `NotifyIcon` for the tray.
- xUnit for tests.
- Coding conventions from the user's global CLAUDE.md: primary constructors,
  parameters used directly (no backing fields), one type per file.

## 3. Solution layout

```
ScreenSavers/                                  repo root
  ScreenBugs.sln
  src/ScreenBugs.Core/                         net10.0 class library, no UI refs
    ScreenBugs.Core.csproj
    Simulation/
      BugSimulation.cs
      Bug.cs
      BugState.cs
      BugSpecies.cs
      SpeciesId.cs
      SpeciesCatalog.cs
      IRandomSource.cs
      SystemRandomSource.cs
      Bounds.cs
  src/ScreenBugs/                              net10.0-windows WPF app
    ScreenBugs.csproj                          UseWPF + UseWindowsForms
    App.xaml, App.xaml.cs
    app.manifest                               PerMonitorV2 DPI awareness
    Overlay/
      OverlayWindow.xaml, OverlayWindow.xaml.cs
      BugCanvas.cs
      FrameLoop.cs
      CursorTracker.cs
      ClickThroughController.cs
      TopmostKeeper.cs
      NativeMethods.cs
    Rendering/
      IBugPainter.cs
      BugPainterRegistry.cs
      LegPainter.cs
      SplatPainter.cs
      Painters/
        HissingCockroachPainter.cs
        BlackGardenAntPainter.cs
        RedFireAntPainter.cs
        PrayingMantisPainter.cs
        SevenSpotLadybugPainter.cs
        StagBeetlePainter.cs
        HouseSpiderPainter.cs
        CentipedePainter.cs
        StinkBugPainter.cs
    Tray/
      TrayIcon.cs
      TrayIconFactory.cs
    Diagnostics/
      CrashLog.cs
      SingleInstanceGuard.cs
  tests/ScreenBugs.Tests/                      net10.0 xUnit
    ScreenBugs.Tests.csproj
    BugSimulationTests.cs
    BugFleeTests.cs
    BugSquashTests.cs
    BugSpawnTests.cs
    SpeciesCatalogTests.cs
  docs/superpowers/specs/
    2026-09-02-screen-bugs-design.md           this file
    assets/bug-specimens.svg                   approved look for all nine species
```

Dependencies flow one way: `ScreenBugs` depends on `ScreenBugs.Core`;
`ScreenBugs.Tests` depends on `ScreenBugs.Core` only.

## 4. Coordinate conventions

- All simulation coordinates are WPF device-independent pixels (DIPs), Y down,
  origin at the top-left of the primary monitor. `System.Numerics.Vector2`.
- Heading is radians. 0 points right (+X); positive rotates clockwise on screen
  (because Y is down). A bug at heading `h` moves along `(cos h, sin h)`.
- Painters draw in bug-local space: origin at the body center, the bug facing up
  (negative Y), units in DIPs. The canvas rotates by `heading + 90 degrees` so
  "up" in painter space becomes the direction of travel.

## 5. Core simulation (`ScreenBugs.Core`)

The Core library is pure C#. Given the same `IRandomSource` sequence and the
same inputs, it produces the same output, which is what makes it testable.

### 5.1 Types

`IRandomSource`
- `float NextFloat()` in [0, 1); `float NextFloat(float min, float max)`;
  `int NextInt(int maxExclusive)`.
- `SystemRandomSource(int? seed = null)` wraps `System.Random`.

`Bounds` (readonly record struct): `Width`, `Height`. Helper `Contains(Vector2)`
and `Clamp(Vector2, float inset)`.

`SpeciesId` (enum): `HissingCockroach`, `BlackGardenAnt`, `RedFireAnt`,
`PrayingMantis`, `SevenSpotLadybug`, `StagBeetle`, `HouseSpider`, `Centipede`,
`StinkBug`.

`BugSpecies` (record) holds every per-species tuning value:

| Field | Meaning |
|---|---|
| `Id` | `SpeciesId` |
| `BodyLength` | DIPs, head to tail, sets the painter's scale |
| `HitRadius` | DIPs, radius of the invisible click disc around the body center |
| `WalkSpeed` | DIPs per second while wandering |
| `FleeSpeed` | DIPs per second while fleeing |
| `TurnRate` | radians per second while wandering; fleeing uses 2x |
| `FleeRadius` | DIPs; cursor inside this distance triggers a flee |
| `ReactionDelayMin`, `ReactionDelayMax` | seconds between cursor arriving and the flee starting |
| `PauseChancePerSecond` | probability per second of dropping into a pause while wandering |
| `PauseMin`, `PauseMax` | seconds a pause lasts |
| `StrideLength` | DIPs traveled per full leg cycle |
| `LegCount` | legs the painter animates: 4 (mantis walking legs), 6, 8, or 20 |

`SpeciesCatalog` (static): `IReadOnlyList<BugSpecies> All` and `Get(SpeciesId)`.
Values for v1:

| Species | Body | Hit | Walk | Flee | Turn | FleeR | React | Pause/s | Pause len | Legs |
|---|---|---|---|---|---|---|---|---|---|---|
| Hissing cockroach | 44 | 26 | 110 | 330 | 5.0 | 180 | 0.10 to 0.25 | 0.20 | 0.5 to 2.0 | 6 |
| Black garden ant | 16 | 14 | 70 | 175 | 6.0 | 120 | 0.10 to 0.25 | 0.50 | 0.3 to 1.2 | 6 |
| Red fire ant | 15 | 14 | 80 | 200 | 6.0 | 120 | 0.10 to 0.25 | 0.50 | 0.3 to 1.2 | 6 |
| Praying mantis | 56 | 24 | 25 | 50 | 2.0 | 90 | 0.20 to 0.40 | 0.80 | 1.0 to 4.0 | 4 |
| Seven-spot ladybug | 22 | 16 | 40 | 80 | 3.0 | 100 | 0.10 to 0.25 | 0.30 | 0.5 to 2.0 | 6 |
| Stag beetle | 40 | 22 | 30 | 55 | 2.0 | 90 | 0.10 to 0.25 | 0.30 | 0.5 to 2.0 | 6 |
| House spider | 34 | 24 | 90 | 270 | 8.0 | 150 | 0.05 to 0.15 | 1.00 | 0.8 to 3.0 | 8 |
| Centipede | 50 | 22 | 60 | 150 | 3.0 | 130 | 0.10 to 0.25 | 0.15 | 0.5 to 2.0 | 20 |
| Stink bug | 28 | 18 | 35 | 70 | 2.5 | 100 | 0.10 to 0.25 | 0.40 | 0.5 to 2.0 | 6 |

`StrideLength` is `0.6 * BodyLength` for every species.

`BugState` (enum): `Wandering`, `Pausing`, `Fleeing`, `Squashed`.

`Bug` (class, mutable, owned by the simulation):
- `int Id`, `BugSpecies Species`, `int Seed` (for stable per-bug visual variation
  and splat shape).
- `Vector2 Position`, `float Heading`, `float TargetHeading`, `float Speed`
  (current, DIPs per second).
- `BugState State`, `float StateTime` (seconds in the current state).
- `float LegPhase` in [0, 1): advances by `distanceMoved / StrideLength`, so legs
  stop when the bug stops.
- `float SpeedFactor` in [0.85, 1.15], drawn from `Seed`, multiplies both speeds.
- `float ReactionTimer`, `float FleeSafeTime` (seconds the cursor has been far
  away while fleeing), `float SquashProgress` in [0, 1].
- `bool HasEnteredScreen`: set true the first time `Bounds.Contains(Position)`
  holds; the edge clamp (5.4) applies only after that.
- `bool IsAlive => State != BugState.Squashed`.
- `bool HitTest(Vector2 point)` returns true when alive and
  `Distance(point, Position) <= Species.HitRadius`.

`BugSimulation(Bounds bounds, int targetCount, IRandomSource rng)`:
- `IReadOnlyList<Bug> Bugs`.
- `int TargetCount { get; set; }` (see 5.6).
- `void Step(float dt, Vector2? cursor)`; `cursor` is null when unknown.
- `Bug? HitTest(Vector2 point)` returns the nearest alive bug whose hit disc
  contains the point, or null.
- `bool TrySquashAt(Vector2 point)`.
- Constructor spawns `targetCount` bugs immediately (see 5.5).

### 5.2 Step order

For each `Step(dt, cursor)`:

1. Clamp `dt` to at most 0.1 s.
2. For every bug run the state logic (5.3), then movement (5.4).
3. Remove bugs whose `SquashProgress >= 1`.
4. Run respawn logic (5.5).

### 5.3 State logic

Common to `Wandering`, `Pausing`, and `Fleeing`: if `cursor` is within
`FleeRadius`, `ReactionTimer` counts down from a value drawn once from
`[ReactionDelayMin, ReactionDelayMax]` when the cursor first came close. When it
reaches zero and the state is not already `Fleeing`, enter `Fleeing`. If the
cursor leaves the radius before the timer expires, the timer is cancelled.

`Wandering`
- Every 1 to 4 s (drawn per event), set `TargetHeading = Heading + U(-90, +90)
  degrees`.
- Each step, add heading noise `U(-0.3, 0.3) * dt` radians.
- Turn toward `TargetHeading` (plus edge steering, 5.4) at `TurnRate`.
- `Speed = WalkSpeed * SpeedFactor`.
- With probability `PauseChancePerSecond * dt`, enter `Pausing` with a duration
  drawn from `[PauseMin, PauseMax]`.

`Pausing`
- `Speed = 0`. When `StateTime` reaches the drawn duration, return to `Wandering`
  and immediately pick a new `TargetHeading`.

`Fleeing`
- Desired direction = normalize(Position - cursor), rotated by a jitter of
  `U(-20, +20)` degrees redrawn every 0.3 s so the path is not perfectly
  predictable, plus edge steering.
- Turn toward it at `2 * TurnRate`; `Speed = FleeSpeed * SpeedFactor`.
- If the cursor is null or farther than `1.5 * FleeRadius`, accumulate
  `FleeSafeTime`; otherwise reset it to 0. When `FleeSafeTime >= 0.8 s`, enter
  `Pausing` for `U(0.3, 1.0)` s (catching its breath), then `Wandering`.

`Squashed`
- `Speed = 0`. `SquashProgress += dt / 1.5`. No cursor reaction.

### 5.4 Movement and edges

- Edge steering: with margin `M = 60` DIPs, for each screen edge closer than `M`,
  add `(1 - d / M)` times that edge's inward normal to a repulsion vector `r`.
  If `r` is non-zero, the effective target direction is
  `normalize(desiredDir + 2 * r)`. This applies to both wandering and fleeing.
- Turning: rotate `Heading` toward the effective target by at most
  `turnRate * dt` per step, taking the shorter way around.
- Move: `Position += (cos Heading, sin Heading) * Speed * dt`.
- Hard clamp (only when `HasEnteredScreen`): `Position` is clamped inside the
  bounds inset by 2 DIPs. If the clamp moved the bug, set `TargetHeading`
  toward the screen center.
- `LegPhase = (LegPhase + distanceMoved / StrideLength) mod 1`.

### 5.5 Spawning and respawning

- `SpawnFromEdge()`: choose a random edge; place the bug just outside the screen
  by `BodyLength` along that edge's outward normal, at a random point along the
  edge; set `Heading` to the inward normal plus `U(-30, +30)` degrees; species is
  chosen uniformly from `SpeciesCatalog.All`; `Seed` is `rng.NextInt(int.MaxValue)`.
  `HasEnteredScreen` starts false, so the bug can walk in from outside.
- Initial population: the constructor calls `SpawnFromEdge()` `targetCount`
  times.
- Respawn: whenever `aliveCount < TargetCount` and no respawn timer is running,
  start one with `U(3, 8)` s. When it expires, spawn one bug and clear the timer.
  Because the check runs every step, several deaths queue up one respawn at a
  time, each 3 to 8 s apart.

### 5.6 Changing the target count

Setting `TargetCount`:
- Increase: call `SpawnFromEdge()` for each missing bug immediately.
- Decrease: remove surplus alive bugs, newest `Id` first, until
  `aliveCount == TargetCount`. Squashed bugs are left to finish fading.

### 5.7 Squash

`TrySquashAt(point)`: `HitTest(point)`; if a bug is found, set `State =
Squashed`, `SquashProgress = 0`, `Speed = 0`, return true; otherwise false.

## 6. Rendering (`ScreenBugs/Rendering`)

`IBugPainter { void Paint(DrawingContext dc, Bug bug); }` draws one bug in
bug-local space (section 4). One implementation per species, registered in
`BugPainterRegistry` which maps `SpeciesId` to a painter instance.

Shared helpers:

- `LegPainter.DrawLeg(dc, pen, hip, knee, foot, swingRadians)` draws a two-segment
  leg (hip to knee to foot) rotated about the hip by `swingRadians`.
- Gait: `swing = amplitude * sin(2 * PI * (bug.LegPhase + groupOffset))`.
  - Six legs: tripod gait. Legs are indexed front to back on each side; left 1,
    left 3, and right 2 use `groupOffset = 0`, the others use `0.5`. Because the
    right side is mirrored, applying the same signed swing to both sides
    naturally moves them in opposite directions.
  - Four legs (mantis): front pair offset 0, rear pair 0.5. The raptorial
    forelegs are static and drawn folded.
  - Eight legs (spider): legs 1 and 3 offset 0, legs 2 and 4 offset 0.5.
  - Centipede: 10 body segments with a leg pair each; pair `i` uses
    `groupOffset = 0.1 * i`, producing a metachronal wave down the body.
  - Amplitude is 6 to 10 degrees depending on species (ants 9, cockroach 8,
    others 7, centipede 10).
- Body bob: the body group is offset sideways by `1 DIP * sin(4 * PI * LegPhase)`
  so it sways with the steps.
- Antennae waggle: rotate each antenna about its base by
  `3 degrees * sin(2 * PI * LegPhase + side)`.
- Shadow: an ellipse under the body, black at 8 percent opacity, offset (2, 3).
- Hit disc: a filled circle of `HitRadius` in `Color.FromArgb(1, 0, 0, 0)` drawn
  first for every alive bug. It is visually invisible but non-transparent to
  Windows layered-window hit testing (see 7.2).

Geometry for each species is ported from `assets/bug-specimens.svg`. Each
specimen there is drawn at roughly 3x on-screen size; painters scale the
specimen coordinates so the body spans `Species.BodyLength`.

`SplatPainter.Paint(dc, bug)` renders a squashed bug: 6 to 9 overlapping circles
in the species' darkened body color, offset randomly within `0.7 * BodyLength`
of the center, plus 3 to 5 small droplet circles further out. All offsets come
from a `Random(bug.Seed)` so the splat is stable across frames. The whole splat
is drawn at opacity `1 - SquashProgress`.

`BugCanvas : FrameworkElement`:
- `BugSimulation? Simulation` property.
- `OnRender` iterates `Simulation.Bugs`; for alive bugs it pushes a translate to
  `Position` and a rotate of `Heading + 90 degrees` then calls the species
  painter; for squashed bugs it calls `SplatPainter` (translate only).
- `Redraw()` calls `InvalidateVisual()`; the frame loop calls it once per tick.

## 7. Overlay window (`ScreenBugs/Overlay`)

### 7.1 `OverlayWindow`

- `WindowStyle=None`, `AllowsTransparency=true`, `Background=Transparent`,
  `ResizeMode=NoResize`, `Topmost=true`, `ShowInTaskbar=false`,
  `ShowActivated=false`, `Focusable=false`.
- Positioned at (0, 0) with size `SystemParameters.PrimaryScreenWidth` by
  `PrimaryScreenHeight` (DIPs), which covers the primary monitor including the
  taskbar area.
- Content is a single `BugCanvas`.
- On `SourceInitialized`, adds the extended styles `WS_EX_TOOLWINDOW` (no
  Alt-Tab entry), `WS_EX_NOACTIVATE` (never takes focus), and
  `WS_EX_TRANSPARENT` (click-through; toggled at runtime by
  `ClickThroughController`).
- `MouseLeftButtonDown` converts the event position to a `Vector2` and calls
  `Simulation.TrySquashAt`. The event is marked handled either way.
- `app.manifest` declares PerMonitorV2 DPI awareness so DIP math matches the
  primary monitor's scale.

### 7.2 Click-through and hit testing

Windows routes mouse input on a layered window only where pixels are
non-transparent, and never when `WS_EX_TRANSPARENT` is set. The two mechanisms
combine:

- Default: `WS_EX_TRANSPARENT` is set, so every click passes through regardless
  of what is drawn.
- Each frame, `ClickThroughController.Update(bool cursorOverBug)` clears the
  style when the cursor is inside some alive bug's hit disc and sets it when it
  is not. It calls `SetWindowLongPtr` only on a state change.
- While the style is cleared, the hit disc (alpha 1) under the bug makes the
  click land on our window; `MouseLeftButtonDown` then squashes it. Clicks
  elsewhere still pass through because those pixels have alpha 0.

### 7.3 `CursorTracker`

`Vector2? GetCursorDips(Window window)`: calls `GetCursorPos`; on failure returns
null. Converts screen pixels to DIPs with the window's
`CompositionTarget.TransformFromDevice`. A null cursor means "far away" to the
simulation.

### 7.4 `FrameLoop`

- `FrameLoop(Action<float> tick)`; `Start()` subscribes to
  `CompositionTarget.Rendering`, `Stop()` unsubscribes.
- Uses `RenderingEventArgs.RenderingTime` to measure elapsed time; skips the
  callback if less than 1/60 s has elapsed since the last accepted frame; passes
  `dt` (seconds, capped at 0.1 by the simulation) to `tick`.
- `Start()` after a `Stop()` resets the timestamp so the paused duration is not
  passed as `dt`.

### 7.5 `TopmostKeeper`

A `DispatcherTimer` at 2 s calling `SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE)` so the overlay stays above windows
that become topmost later. Stopped while paused.

### 7.6 `NativeMethods`

`GetWindowLongPtr`, `SetWindowLongPtr`, `SetWindowPos`, `GetCursorPos`, and
the constants `GWL_EXSTYLE`, `WS_EX_TRANSPARENT`, `WS_EX_TOOLWINDOW`,
`WS_EX_NOACTIVATE`, `HWND_TOPMOST`, `SWP_*`. All wrapped as `internal static`
methods that throw `Win32Exception` on failure except `GetCursorPos`, which
returns false.

### 7.7 Per-frame tick

```
cursor = CursorTracker.GetCursorDips(window)
simulation.Step(dt, cursor)
clickThrough.Update(cursor is not null && simulation.HitTest(cursor.Value) is not null)
canvas.Redraw()
```

## 8. Tray (`ScreenBugs/Tray`)

`TrayIcon` wraps a `System.Windows.Forms.NotifyIcon`:
- Icon from `TrayIconFactory.Create()`, which draws a 32x32 black ant silhouette
  with `System.Drawing` and converts it to an `Icon`. No icon asset file.
- Context menu: `Pause` (text becomes `Resume` while paused), `Bugs` submenu with
  radio-checked items `1`, `3`, `5`, `10`, a separator, and `Exit`.
- Events: `PauseToggled`, `BugCountChanged(int)`, `ExitRequested`.
- `IDisposable`: sets `Visible = false` and disposes the `NotifyIcon` so no ghost
  icon remains.

## 9. Application composition (`App`)

- `ShutdownMode = OnExplicitShutdown`; no main window is shown by WPF.
- `OnStartup`:
  1. `SingleInstanceGuard.TryAcquire()` opens a named mutex
     `Local\ScreenBugs.SingleInstance`; if it already exists, shut down quietly.
  2. Create `BugSimulation(bounds from primary screen, targetCount: 3,
     new SystemRandomSource())`.
  3. Create `OverlayWindow`, assign the simulation to its canvas, show it.
  4. Create `TrayIcon`; wire `PauseToggled` to hide/show the window and
     stop/start the `FrameLoop` and `TopmostKeeper`; wire `BugCountChanged` to
     `simulation.TargetCount`; wire `ExitRequested` to `Shutdown()`.
  5. Start the frame loop and topmost keeper.
- `OnExit` disposes the tray icon and releases the mutex.
- `DispatcherUnhandledException`: `CrashLog.Write(exception)` appends to
  `%LocalAppData%\ScreenBugs\error.log`, the tray icon is disposed, and the app
  shuts down. The handler marks the exception handled so WPF does not show its
  own dialog.

## 10. Testing

Unit tests (xUnit, `ScreenBugs.Tests`, seeded `SystemRandomSource(1234)`):

- Stays in bounds: 10 bugs, 20,000 steps of 1/60 s with no cursor; every alive
  bug that has entered the screen is inside the bounds inset by 2 DIPs.
- Legs stop when paused: `LegPhase` does not change across a step where the bug
  is `Pausing`.
- Flees the cursor: place a cursor 40 DIPs from a wandering bug; after 1 s of
  steps the bug is `Fleeing` and its distance from the cursor has increased.
- Reaction delay: with the cursor close, the bug is not `Fleeing` before
  `ReactionDelayMin` has elapsed.
- Flee ends: after the cursor moves away, the bug returns to `Pausing` then
  `Wandering` within 3 s.
- Squash: `TrySquashAt` on a bug's position returns true, the bug is `Squashed`,
  after 1.5 s of steps it is removed from `Bugs`, and `TrySquashAt` on empty
  space returns false.
- Respawn: after a squash the alive count returns to `TargetCount` within 8 s
  of steps, and the new bug spawns outside the bounds heading inward.
- Target count up and down: raising to 10 spawns 7 immediately; lowering to 1
  leaves exactly one alive bug and keeps any fading squashed bug.
- Hit test: `HitTest` returns the nearest bug when two overlap and null when the
  point is outside every disc.
- Catalog: nine species, all with positive values, `FleeSpeed > WalkSpeed`,
  `ReactionDelayMax >= ReactionDelayMin`, `PauseMax >= PauseMin`.

Manual checklist for the WPF layer (documented in the plan, run before calling
the work done):

1. Launch: bugs walk in from the edges over a normal desktop.
2. Clicking and typing in other apps works everywhere except on a bug.
3. Moving the cursor toward a bug makes it run; a quick click still squashes it.
4. A squashed bug leaves a fading splat and a replacement appears later.
5. Alt-Tab shows no Screen Bugs entry; the foreground app keeps focus after a
   squash.
6. Tray: Pause hides the bugs and Resume brings them back; the count menu adds
   and removes bugs; Exit removes the tray icon.
7. Launching a second copy does nothing.
8. Task Manager shows modest CPU with 10 bugs at 60 fps.

## 11. Future work (out of scope)

Multi-monitor overlays, persisted settings, run at startup, squash sound,
additional species, bug-to-bug interactions.
