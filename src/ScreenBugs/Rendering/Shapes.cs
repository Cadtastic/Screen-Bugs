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
