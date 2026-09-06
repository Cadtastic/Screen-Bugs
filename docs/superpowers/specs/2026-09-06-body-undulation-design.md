# Body Undulation: design spec

Date: 2026-09-06
Status: approved for planning
Issue: #1 "Give long-bodied bugs a wiggle: the centipede should snake, not slide"
Builds on: `2026-09-02-screen-bugs-design.md` section 6 (rendering). This spec
amends the "Body bob" rule there for three species and leaves every other
rendering rule as it is.

## 1. Overview

Every painter offsets its whole body sideways by `BodyMotion.Bob`: one DIP at
twice the leg frequency, applied identically to every body part. The centipede
therefore reads as a rigid plank sliding across the screen while its nine leg
pairs already ripple in a metachronal wave.

This feature adds a per-segment sideways offset keyed on position along the
body, so different points of a long body sit at different points of a
travelling wave. The centipede snakes; the two ants sway subtly; the praying
mantis is tried and kept only if it looks right.

Purely a rendering change. No new simulation state, no settings, no logic in
`ScreenBugs.Core`. `LegPhase` already carries everything needed.

Non-goals: bending paths (geometry stays rigid per segment), time-driven
motion (everything follows `LegPhase`), changes to rigid-bodied species.

## 2. Which species

| Species | Treatment | Why |
| --- | --- | --- |
| Centipede | Full snake: nine segments plus tail, amplitude grows head to tail, one wavelength along the body | Body is already a chain of segments drawn in a loop |
| Black garden ant, red fire ant | Subtle sway: head, thorax, petiole, gaster each offset, half a wavelength | Four parts along the axis; shared `AntGeometry` |
| Praying mantis | Trial: only the abdomen swings relative to the thorax | The abdomen is one path and cannot bend; dropped if it reads wrong |
| Ladybug, stink bug, cockroach, stag beetle, spider | Unchanged, keep `Bob` | Rigid shell, or the legs do all the work |

The mantis rule: implement the abdomen swing, render the filmstrip (section 5),
and judge by eye. If it does not sell, revert the painter to the version on
`main` and say so in the commit message. Either outcome closes the trial.

## 3. The wave

### 3.1 `BodyMotion.Undulate`

```csharp
// ScreenBugs/Rendering/BodyMotion.cs, alongside Bob
public static double Undulate(float legPhase, double scale, double alongBody,
                              double amplitudeDips, double wavelengths) =>
    amplitudeDips / scale * Math.Sin(2.0 * Math.PI * (legPhase - wavelengths * alongBody));
```

- `alongBody` is 0 at the head and 1 at the tail.
- Subtracting the position term makes the crest travel head to tail as the
  phase advances, the way a snake moves. (The leg wave in `CentipedePainter`
  uses `+ 0.125 i`, so it travels tail to head. If the two fight visually, the
  sign here is the one knob to flip; the filmstrip decides.)
- One cycle per stride (`2 * PI * phase`), not `Bob`'s two. A two-per-stride
  ripple on a 50 DIP body looks frantic.
- `/ scale` follows the `Bob` convention: painters draw in specimen units under
  a `ScaleTransform`, so a DIP amplitude is divided by the painter's scale.
- Driven from `LegPhase` only. `LegPhase` advances with distance travelled, so a
  paused bug stops rippling and a bug on a 3x speed row ripples faster, with no
  extra work.

### 3.2 `BodyWave`

```csharp
// ScreenBugs/Rendering/BodyWave.cs
public sealed record BodyWave(
    double HeadY, double TailY,
    double HeadAmplitudeDips, double TailAmplitudeDips,
    double Wavelengths)
{
    public double OffsetAt(double y, float legPhase, double scale)
    {
        double along = Math.Clamp((y - HeadY) / (TailY - HeadY), 0.0, 1.0);
        double amplitude = HeadAmplitudeDips + (TailAmplitudeDips - HeadAmplitudeDips) * along;
        return BodyMotion.Undulate(legPhase, scale, along, amplitude, Wavelengths);
    }
}
```

One per undulating species, declared as a static readonly field of its painter
in specimen coordinates. Amplitude grows linearly from head to tail so the head
tracks straight and the rear swings widest; a constant amplitude would read as
the whole animal sliding sideways. Clamping means parts drawn ahead of `HeadY`
(antennae, mandibles) move with the head, and parts behind `TailY` (hind legs)
move with the tail.

## 4. Painters

The rule that keeps legs attached: whatever offset a segment gets, its leg
pair gets the same one, looked up from the same specimen Y. Legs are drawn
before the body in every painter, so the offset is applied per leg pair with a
`TranslateTransform` rather than by one body-wide transform.

Starting values; the filmstrip tunes them.

### 4.1 Centipede

`new BodyWave(HeadY: -72, TailY: 59, HeadAmplitudeDips: 0.5, TailAmplitudeDips: 3.0, Wavelengths: 1.0)`

- The `Bob` transform goes.
- Leg loop: `dx = Wave.OffsetAt(y, phase, scale)` for the pair at `y`; push a
  translate by `dx`, draw the pair, pop.
- Hind legs translated by `OffsetAt(59)`.
- Segment ellipses centred at `(OffsetAt(y_i), y_i)`, the same `y_i` the legs used.
- Tail ellipse at `OffsetAt(59)`. Head ellipse, eyes and both antennae under one
  translate by `OffsetAt(-72)`.
- Shadow unchanged.

### 4.2 Ants (`AntGeometry`)

`new BodyWave(HeadY: -36, TailY: 18, HeadAmplitudeDips: 0.25, TailAmplitudeDips: 0.75, Wavelengths: 0.5)`

- The `Bob` transform goes.
- The three leg pairs (hips at y = -22, -15, -8) each translated by
  `OffsetAt(hip.Y)`.
- Head (-36) with mandibles and antennae under one translate; thorax (-15),
  petiole (-1) and gaster (18) each centred at their own offset.

### 4.3 Praying mantis (trial)

`new BodyWave(HeadY: -84, TailY: 80, HeadAmplitudeDips: 0.0, TailAmplitudeDips: 0.8, Wavelengths: 0.5)`

- `Bob` stays for thorax, head, forelegs and walking legs.
- Only the abdomen group (abdomen path, centre line, two veins) is translated by
  `OffsetAt(30)`, inside the `Bob` transform, so the abdomen swings relative to
  the thorax.

### 4.4 Everything else

No change. `SplatPainter` replaces the bug entirely on squash and is untouched.

## 5. Filmstrip (review aid)

`dotnet run --project tools/BugRenderer -- --filmstrip [dir]` writes
`<dir>/<SpeciesId>.png` for all nine species: eight frames at
`LegPhase = k / 8` side by side, no outline, no supersample. Bounds are the
union of `ContentBounds` across the eight phases so every frame shares an
origin and the wave shows as displacement rather than re-centring. Default
`dir` is `artifacts/filmstrips` under the repository root, which is
gitignored; the images are never committed.

Rendering the rigid species too is deliberate: their frames must differ only
by legs, antennae and `Bob`.

Implementation split: `SpecimenRenderer` exposes its measuring step
(`Measure`, the paint-once-to-learn-the-bounds trick), `TargetSize` and the
PNG save as internal members. The new `FilmstripRenderer` measures all eight
phases, draws them into one visual with an 8 px gap, and saves through the
same encoder path. Frame zoom reuses the specimen rule (`TargetSize` on the
longest side of the union bounds), so a filmstrip is eight specimen-sized
frames wide. `Write` keeps the outline and supersample for the README images;
the filmstrip uses neither.

`Bug.LegPhase` has an internal setter, so `ScreenBugs.Core` gains
`<InternalsVisibleTo Include="BugRenderer" />` next to the existing test entry.
That is the only line in Core this feature touches.

## 6. Testing

The wave maths is pure and lives in the WPF assembly. The test project is
retargeted to `net10.0-windows` with `UseWPF` and an x64 platform target, and
gains a project reference to `ScreenBugs`. No logic moves to Core for the sake
of testing.

`BodyMotionTests`:
- `Undulate` is zero at phase 0, `alongBody` 0.
- Amplitude is divided by scale.
- Head-to-tail lag: the offset at `alongBody = a`, phase `p` equals the offset
  at `alongBody = 0`, phase `p - wavelengths * a`.
- The peak over a phase sweep equals `amplitude / scale`.
- Same phase gives the same offset (the "stops when paused" property: the
  function has no time input).

`BodyWaveTests` (all at `scale = 1`, so offsets are in DIPs):
- Peak swing at `HeadY` is the head amplitude; at `TailY` the tail amplitude;
  at the midpoint, their mean.
- Y beyond either end clamps to that end.
- Equal head and tail amplitude reproduces `Undulate` directly.

Painters, `BugCanvas` and `SplatPainter` stay untested, as today; the filmstrip
and the live overlay are the review for those.

## 7. Documentation

- Section 6 of `2026-09-02-screen-bugs-design.md`, "Body bob": add that the
  centipede and ants use the per-segment undulation from this spec instead.
- The README specimen images for the affected species are regenerated and
  committed. At `LegPhase` 0 the wave is `sin(-2 * PI * w * alongBody)`, so
  those specimens show a gentle mid-stride curve; the README promises the
  images are exactly what walks across the screen, and the curve is a better
  advert for the feature than a straight plank. Rigid species regenerate
  byte-identical, which doubles as the "visually unchanged" check (if a rigid
  species' PNG does differ, compare visually before treating it as a failure;
  the encoder is not guaranteed stable across machines). The comment in
  `SpecimenRenderer.Write` that says phase 0 gives a symmetric pose is
  corrected in the same commit.
- The README developer-tools line for `tools/BugRenderer` mentions
  `--filmstrip`.

## 8. Acceptance (from the issue)

- The centipede visibly snakes as it crosses the screen, tail wider than head.
- Legs stay attached to their segments throughout.
- The ants sway subtly; noticeable side by side, not distracting.
- Ladybug, stink bug, cockroach, stag beetle and spider are visually unchanged.
- The wiggle stops when a bug pauses and speeds up with the per-row speed slider.
- Squashing looks normal at the moment of the click.
