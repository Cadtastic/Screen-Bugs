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
