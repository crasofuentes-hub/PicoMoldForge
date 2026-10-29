namespace PicoMoldForge.Parts.Geometry;

/// <summary>Axis-aligned 2D bounding box.</summary>
public readonly record struct Box2(Vec2 Min, Vec2 Max)
{
    public static Box2 FromPoints(IEnumerable<Vec2> points)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var p in points)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        if (double.IsInfinity(minX))
        {
            throw new ArgumentException("At least one point is required.", nameof(points));
        }

        return new Box2(new Vec2(minX, minY), new Vec2(maxX, maxY));
    }

    public Vec2 Size => Max - Min;

    public Box2 Union(Box2 other) => new(
        new Vec2(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y)),
        new Vec2(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y)));

    public Box2 Intersect(Box2 other) => new(
        new Vec2(Math.Max(Min.X, other.Min.X), Math.Max(Min.Y, other.Min.Y)),
        new Vec2(Math.Min(Max.X, other.Max.X), Math.Min(Max.Y, other.Max.Y)));

    public Box2 Expand(double margin) => new(
        new Vec2(Min.X - margin, Min.Y - margin),
        new Vec2(Max.X + margin, Max.Y + margin));

    /// <summary>Largest distance from the origin to any corner (circumscribed radius about the origin).</summary>
    public double MaxRadiusFromOrigin()
    {
        var x = Math.Max(Math.Abs(Min.X), Math.Abs(Max.X));
        var y = Math.Max(Math.Abs(Min.Y), Math.Abs(Max.Y));
        return Math.Sqrt((x * x) + (y * y));
    }
}

/// <summary>Axis-aligned 3D bounding box.</summary>
public readonly record struct Box3(Vec3 Min, Vec3 Max)
{
    public static Box3 FromPoints(IEnumerable<Vec3> points)
    {
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = new Vec3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
        var any = false;

        foreach (var p in points)
        {
            min = Vec3.Min(min, p);
            max = Vec3.Max(max, p);
            any = true;
        }

        if (!any)
        {
            throw new ArgumentException("At least one point is required.", nameof(points));
        }

        return new Box3(min, max);
    }

    public Vec3 Size => Max - Min;

    public Vec3 Center => (Min + Max) * 0.5;

    public Box3 Union(Box3 other) => new(Vec3.Min(Min, other.Min), Vec3.Max(Max, other.Max));

    public Box3 Expand(double margin) => new(
        Min - new Vec3(margin, margin, margin),
        Max + new Vec3(margin, margin, margin));

    public IEnumerable<Vec3> Corners()
    {
        for (var i = 0; i < 8; i++)
        {
            yield return new Vec3(
                (i & 1) == 0 ? Min.X : Max.X,
                (i & 2) == 0 ? Min.Y : Max.Y,
                (i & 4) == 0 ? Min.Z : Max.Z);
        }
    }

    /// <summary>Euclidean distance from p to the box (0 when p is inside).</summary>
    public double DistanceOutside(Vec3 p)
    {
        var dx = Math.Max(Math.Max(Min.X - p.X, 0), p.X - Max.X);
        var dy = Math.Max(Math.Max(Min.Y - p.Y, 0), p.Y - Max.Y);
        var dz = Math.Max(Math.Max(Min.Z - p.Z, 0), p.Z - Max.Z);
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    public Box3 Transform(RigidTransform transform) => FromPoints(Corners().Select(transform.Apply));
}
