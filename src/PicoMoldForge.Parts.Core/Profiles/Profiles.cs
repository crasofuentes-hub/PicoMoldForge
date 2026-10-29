using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Profiles;

/// <summary>
/// Closed 2D region described by a signed distance function (negative inside, positive outside).
/// Distance values must never overestimate the true Euclidean distance by more than the Lipschitz factor.
/// </summary>
public interface IProfile2D
{
    double Distance(Vec2 q);

    Box2 Bounds { get; }

    /// <summary>Upper bound of the gradient magnitude (1 for exact distance fields).</summary>
    double Lipschitz { get; }
}

public sealed class CircleProfile : IProfile2D
{
    public CircleProfile(Vec2 center, double radius)
    {
        if (!(radius > 0) || double.IsInfinity(radius))
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Circle radius must be a positive finite value.");
        }

        Center = center;
        Radius = radius;
        Bounds = new Box2(new Vec2(center.X - radius, center.Y - radius), new Vec2(center.X + radius, center.Y + radius));
    }

    public Vec2 Center { get; }

    public double Radius { get; }

    public Box2 Bounds { get; }

    public double Lipschitz => 1.0;

    public double Distance(Vec2 q) => (q - Center).Length - Radius;
}

/// <summary>Axis-aligned rectangle with optional rounded corners (exact distance).</summary>
public sealed class RectangleProfile : IProfile2D
{
    private readonly Vec2 _center;
    private readonly Vec2 _halfSize;

    public RectangleProfile(Vec2 min, Vec2 max, double cornerRadius = 0)
    {
        if (!(max.X > min.X) || !(max.Y > min.Y))
        {
            throw new ArgumentException("Rectangle max must be greater than min on both axes.");
        }

        var halfX = (max.X - min.X) / 2;
        var halfY = (max.Y - min.Y) / 2;

        if (cornerRadius < 0 || cornerRadius > Math.Min(halfX, halfY))
        {
            throw new ArgumentOutOfRangeException(nameof(cornerRadius), "Corner radius must be between 0 and half of the smallest side.");
        }

        Min = min;
        Max = max;
        CornerRadius = cornerRadius;
        _center = (min + max) * 0.5;
        _halfSize = new Vec2(halfX - cornerRadius, halfY - cornerRadius);
        Bounds = new Box2(min, max);
    }

    public Vec2 Min { get; }

    public Vec2 Max { get; }

    public double CornerRadius { get; }

    public Box2 Bounds { get; }

    public double Lipschitz => 1.0;

    public double Distance(Vec2 q)
    {
        var dx = Math.Abs(q.X - _center.X) - _halfSize.X;
        var dy = Math.Abs(q.Y - _center.Y) - _halfSize.Y;
        var ox = Math.Max(dx, 0);
        var oy = Math.Max(dy, 0);
        var outside = Math.Sqrt((ox * ox) + (oy * oy));
        var inside = Math.Min(Math.Max(dx, dy), 0);
        return outside + inside - CornerRadius;
    }
}

/// <summary>
/// Polygon region with one or more closed loops, filled with the even-odd rule (loops inside loops are holes).
/// Exact Euclidean distance to the boundary.
/// </summary>
public sealed class PolygonProfile : IProfile2D
{
    private readonly Vec2[][] _loops;

    public PolygonProfile(IReadOnlyList<IReadOnlyList<Vec2>> loops)
    {
        ArgumentNullException.ThrowIfNull(loops);

        if (loops.Count == 0)
        {
            throw new ArgumentException("A polygon profile needs at least one loop.", nameof(loops));
        }

        _loops = loops.Select(NormalizeLoop).ToArray();
        Bounds = Box2.FromPoints(_loops.SelectMany(loop => loop));
    }

    public PolygonProfile(IReadOnlyList<Vec2> loop)
        : this(new[] { loop })
    {
    }

    public IReadOnlyList<IReadOnlyList<Vec2>> Loops => _loops;

    public Box2 Bounds { get; }

    public double Lipschitz => 1.0;

    public double Distance(Vec2 p)
    {
        var best = double.PositiveInfinity;
        var inside = false;

        foreach (var loop in _loops)
        {
            var count = loop.Length;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var a = loop[j];
                var b = loop[i];
                var ex = b.X - a.X;
                var ey = b.Y - a.Y;
                var wx = p.X - a.X;
                var wy = p.Y - a.Y;
                var t = Math.Clamp(((wx * ex) + (wy * ey)) / ((ex * ex) + (ey * ey)), 0.0, 1.0);
                var dx = wx - (ex * t);
                var dy = wy - (ey * t);
                var d2 = (dx * dx) + (dy * dy);

                if (d2 < best)
                {
                    best = d2;
                }

                if ((a.Y > p.Y) != (b.Y > p.Y))
                {
                    var crossX = a.X + ((p.Y - a.Y) * ex / ey);

                    if (p.X < crossX)
                    {
                        inside = !inside;
                    }
                }
            }
        }

        var distance = Math.Sqrt(best);
        return inside ? -distance : distance;
    }

    private static Vec2[] NormalizeLoop(IReadOnlyList<Vec2> loop)
    {
        if (loop is null || loop.Count < 3)
        {
            throw new ArgumentException("Each polygon loop needs at least three points.");
        }

        var points = loop.ToList();

        if (points.Count > 3 && points[0] == points[^1])
        {
            points.RemoveAt(points.Count - 1);
        }

        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];

            if (!double.IsFinite(a.X) || !double.IsFinite(a.Y))
            {
                throw new ArgumentException("Polygon points must be finite numbers.");
            }

            if ((b - a).LengthSquared == 0)
            {
                throw new ArgumentException("Polygon loops cannot contain repeated consecutive points.");
            }
        }

        var area = 0.0;

        for (var i = 0; i < points.Count; i++)
        {
            area += Vec2.Cross(points[i], points[(i + 1) % points.Count]);
        }

        if (Math.Abs(area) < 1e-12)
        {
            throw new ArgumentException("Polygon loop has zero area.");
        }

        return points.ToArray();
    }

    /// <summary>
    /// Builds the full axial section of a solid of revolution from a half-section polyline (radial >= 0)
    /// whose first and last points lie on the axis (radial = 0). The section is mirrored to negative radii so
    /// the rotation axis is not treated as a boundary.
    /// </summary>
    public static PolygonProfile FromHalfSection(IReadOnlyList<Vec2> halfSection)
    {
        ArgumentNullException.ThrowIfNull(halfSection);

        if (halfSection.Count < 3)
        {
            throw new ArgumentException("A half-section needs at least three points.", nameof(halfSection));
        }

        if (Math.Abs(halfSection[0].X) > 1e-9 || Math.Abs(halfSection[^1].X) > 1e-9)
        {
            throw new ArgumentException("A half-section must start and end on the rotation axis (radial = 0).", nameof(halfSection));
        }

        if (halfSection.Any(p => p.X < -1e-9))
        {
            throw new ArgumentException("A half-section must stay on the non-negative radial side.", nameof(halfSection));
        }

        var full = new List<Vec2>();
        full.AddRange(halfSection);

        for (var i = halfSection.Count - 2; i >= 1; i--)
        {
            full.Add(new Vec2(-halfSection[i].X, halfSection[i].Y));
        }

        return new PolygonProfile(full);
    }
}

public sealed class ProfileUnion : IProfile2D
{
    private readonly IProfile2D[] _children;

    public ProfileUnion(IEnumerable<IProfile2D> children)
    {
        _children = children?.ToArray() ?? throw new ArgumentNullException(nameof(children));

        if (_children.Length == 0)
        {
            throw new ArgumentException("A union needs at least one profile.", nameof(children));
        }

        Bounds = _children.Skip(1).Aggregate(_children[0].Bounds, (box, child) => box.Union(child.Bounds));
        Lipschitz = _children.Max(child => child.Lipschitz);
    }

    public IReadOnlyList<IProfile2D> Children => _children;

    public Box2 Bounds { get; }

    public double Lipschitz { get; }

    public double Distance(Vec2 q)
    {
        var d = double.PositiveInfinity;

        foreach (var child in _children)
        {
            d = Math.Min(d, child.Distance(q));
        }

        return d;
    }
}

/// <summary>Region of <c>Base</c> minus region of <c>Cut</c>.</summary>
public sealed class ProfileDifference : IProfile2D
{
    public ProfileDifference(IProfile2D baseProfile, IProfile2D cut)
    {
        Base = baseProfile ?? throw new ArgumentNullException(nameof(baseProfile));
        Cut = cut ?? throw new ArgumentNullException(nameof(cut));
        Lipschitz = Math.Max(Base.Lipschitz, Cut.Lipschitz);
    }

    public IProfile2D Base { get; }

    public IProfile2D Cut { get; }

    public Box2 Bounds => Base.Bounds;

    public double Lipschitz { get; }

    public double Distance(Vec2 q) => Math.Max(Base.Distance(q), -Cut.Distance(q));
}
