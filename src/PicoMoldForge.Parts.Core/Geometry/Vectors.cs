namespace PicoMoldForge.Parts.Geometry;

/// <summary>Immutable 2D vector in millimeters (double precision).</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);

    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);

    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);

    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public double LengthSquared => (X * X) + (Y * Y);

    public static double Dot(Vec2 a, Vec2 b) => (a.X * b.X) + (a.Y * b.Y);

    public static double Cross(Vec2 a, Vec2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>Rotates counterclockwise by the given angle (radians).</summary>
    public Vec2 Rotate(double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new Vec2((c * X) - (s * Y), (s * X) + (c * Y));
    }

    public override string ToString() => FormattableString.Invariant($"({X:0.######}, {Y:0.######})");
}

/// <summary>Immutable 3D vector in millimeters (double precision).</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 UnitX = new(1, 0, 0);
    public static readonly Vec3 UnitY = new(0, 1, 0);
    public static readonly Vec3 UnitZ = new(0, 0, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);

    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public static Vec3 operator *(double s, Vec3 a) => new(a.X * s, a.Y * s, a.Z * s);

    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    public static double Dot(Vec3 a, Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    public static Vec3 Cross(Vec3 a, Vec3 b) => new(
        (a.Y * b.Z) - (a.Z * b.Y),
        (a.Z * b.X) - (a.X * b.Z),
        (a.X * b.Y) - (a.Y * b.X));

    public static Vec3 Min(Vec3 a, Vec3 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));

    public static Vec3 Max(Vec3 a, Vec3 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

    public Vec3 Normalized()
    {
        var length = Length;
        return length > 0 ? this * (1.0 / length) : Zero;
    }

    public double this[int axis] => axis switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    public override string ToString() => FormattableString.Invariant($"({X:0.######}, {Y:0.######}, {Z:0.######})");
}
