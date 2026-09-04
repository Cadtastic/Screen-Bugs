using System.Numerics;

namespace ScreenBugs.Core.Simulation;

/// <summary>Owns the bugs and steps their behavior (spec section 5). Pure C#; no UI dependencies.</summary>
public sealed class BugSimulation(Bounds bounds, IRandomSource rng, ISpeciesSource speciesSource)
{
    private const float MaxDt = 0.1f;
    private const float SquashDuration = 1.5f;
    private const float EdgeMargin = 60f;
    private const float EdgeInset = 2f;
    private const float EdgeSteerWeight = 2f;
    private const float HeadingNoise = 0.3f;
    private const float FleeTurnMultiplier = 2f;
    private const float FleeStrideMultiplier = 2f;
    private const float FleeSafeDistanceFactor = 1.5f;
    private const float FleeSafeDuration = 0.8f;
    private const float FleeJitterInterval = 0.3f;
    private const float FleeJitterMax = 20f * MathF.PI / 180f;
    private const float MinFleeDistance = 0.01f;
    private const float StragglerTimeout = 10f;

    private readonly List<Bug> bugs = [];
    private int nextId;
    private int targetCount;
    private float? respawnTimer;

    public IReadOnlyList<Bug> Bugs => bugs;

    /// <summary>Seconds until the next respawn, or null when no respawn is pending. Exposed for tests.</summary>
    internal float? RespawnTimer => respawnTimer;

    /// <summary>How many alive bugs the simulation maintains (spec 5.6). Setting it spawns or removes bugs immediately.</summary>
    public int TargetCount
    {
        get => targetCount;
        set
        {
            targetCount = value;
            respawnTimer = null;
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

    /// <summary>
    /// Places a wandering bug exactly where asked. Exists for tests; the app never calls it.
    /// <paramref name="slotIndex"/> defaults to none, so the bug runs at the default speed.
    /// </summary>
    public Bug AddBug(BugSpecies species, Vector2 position, float heading, int slotIndex = -1)
    {
        var bug = new Bug(nextId++, species, rng.NextInt(int.MaxValue))
        {
            Position = position,
            Heading = heading,
            TargetHeading = heading,
            HasEnteredScreen = bounds.Contains(position),
            RetargetTimer = rng.NextFloat(1f, 4f),
            SlotIndex = slotIndex,
        };
        bug.Speed = SpeedOf(bug, species.WalkSpeed);
        bugs.Add(bug);
        return bug;
    }

    /// <summary>
    /// A bug's current speed: the species figure, its own small variation, and its options row's
    /// multiplier, which is read live so a slider drag reaches bugs already on screen.
    /// </summary>
    private float SpeedOf(Bug bug, float baseSpeed) =>
        baseSpeed * bug.SpeedFactor * speciesSource.SpeedFor(bug.SlotIndex);

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

    /// <summary>
    /// Removes every alive bug and walks a fresh population in from the edges. Squashed bugs are
    /// left to finish fading. Used when the selected bug types change.
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
        bug.Speed = SpeedOf(bug, bug.Species.FleeSpeed);

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

    private void UpdateWandering(Bug bug, float dt)
    {
        if (bug.RetargetTimer <= 0f)
        {
            PickNewTarget(bug);
        }

        bug.Heading += rng.NextFloat(-HeadingNoise, HeadingNoise) * dt;
        bug.Speed = SpeedOf(bug, bug.Species.WalkSpeed);

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
            case BugState.Fleeing:
                bug.ReactionTimer = null;
                bug.FleeSafeTime = 0f;
                bug.FleeJitterTimer = 0f;
                break;
            case BugState.Squashed:
                bug.Speed = 0f;
                bug.SquashProgress = 0f;
                break;
        }
    }

    /// <summary>Adds a random species one body length outside a random edge, heading inward ±30° (spec 5.5).</summary>
    private void SpawnFromEdge()
    {
        var choice = speciesSource.Next();
        var species = choice.Species;
        var bug = new Bug(nextId++, species, rng.NextInt(int.MaxValue)) { SlotIndex = choice.SlotIndex };

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
        bug.Speed = SpeedOf(bug, species.WalkSpeed);
        bugs.Add(bug);
    }
}
