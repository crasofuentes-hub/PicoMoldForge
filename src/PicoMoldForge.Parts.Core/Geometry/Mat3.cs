namespace PicoMoldForge.Parts.Geometry;

/// <summary>Immutable row-major 3x3 matrix used for rigid rotations.</summary>
public readonly record struct Mat3(
    double M00, double M01, double M02,
    double M10, double M11, double M12,
    double M20, double M21, double M22)
{
    public static readonly Mat3 Identity = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    public static Vec3 operator *(Mat3 m, Vec3 v) => new(
        (m.M00 * v.X) + (m.M01 * v.Y) + (m.M02 * v.Z),
        (m.M10 * v.X) + (m.M11 * v.Y) + (m.M12 * v.Z),
        (m.M20 * v.X) + (m.M21 * v.Y) + (m.M22 * v.Z));

    public static Mat3 operator *(Mat3 a, Mat3 b) => new(
        (a.M00 * b.M00) + (a.M01 * b.M10) + (a.M02 * b.M20),
        (a.M00 * b.M01) + (a.M01 * b.M11) + (a.M02 * b.M21),
        (a.M00 * b.M02) + (a.M01 * b.M12) + (a.M02 * b.M22),
        (a.M10 * b.M00) + (a.M11 * b.M10) + (a.M12 * b.M20),
        (a.M10 * b.M01) + (a.M11 * b.M11) + (a.M12 * b.M21),
        (a.M10 * b.M02) + (a.M11 * b.M12) + (a.M12 * b.M22),
        (a.M20 * b.M00) + (a.M21 * b.M10) + (a.M22 * b.M20),
        (a.M20 * b.M01) + (a.M21 * b.M11) + (a.M22 * b.M21),
        (a.M20 * b.M02) + (a.M21 * b.M12) + (a.M22 * b.M22));

    public Mat3 Transposed() => new(M00, M10, M20, M01, M11, M21, M02, M12, M22);

    public bool IsIdentity(double tolerance = 1e-12) =>
        Math.Abs(M00 - 1) <= tolerance && Math.Abs(M11 - 1) <= tolerance && Math.Abs(M22 - 1) <= tolerance &&
        Math.Abs(M01) <= tolerance && Math.Abs(M02) <= tolerance &&
        Math.Abs(M10) <= tolerance && Math.Abs(M12) <= tolerance &&
        Math.Abs(M20) <= tolerance && Math.Abs(M21) <= tolerance;
}

/// <summary>Rigid transform: world = Rotation * local + Translation.</summary>
public readonly record struct RigidTransform(Mat3 Rotation, Vec3 Translation)
{
    public static readonly RigidTransform Identity = new(Mat3.Identity, Vec3.Zero);

    public static RigidTransform FromTranslation(Vec3 translation) => new(Mat3.Identity, translation);

    public Vec3 Apply(Vec3 local) => (Rotation * local) + Translation;

    public Vec3 ApplyInverse(Vec3 world) => Rotation.Transposed() * (world - Translation);

    /// <summary>Returns the transform that applies <paramref name="first"/> and then this transform.</summary>
    public RigidTransform After(RigidTransform first) =>
        new(Rotation * first.Rotation, (Rotation * first.Translation) + Translation);
}
