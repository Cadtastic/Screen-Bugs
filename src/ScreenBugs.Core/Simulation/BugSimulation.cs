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
