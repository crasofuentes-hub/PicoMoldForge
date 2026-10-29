using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Axes;

/// <summary>Linear axes (ISO 841).</summary>
public enum LinearAxis
{
    X,
    Y,
    Z
}

/// <summary>
/// Rotary axes (ISO 841): A rotates about X, B about Y, C about Z.
/// Positive rotation follows the right-hand rule: the direction that advances a right-hand screw
/// in the positive direction of the linear axis (counterclockwise when looking from +axis toward the origin).
/// </summary>
public enum RotaryAxis
{
    A,
    B,
    C
}

/// <summary>
/// Right-handed local frame attached to a linear axis W. Profiles of linear sweeps live in the (U, V) plane
/// and sweep along W. Profiles of rotary sweeps live in the (radial, axial) half-plane where the radial
/// direction starts at U and the axial direction is W.
/// </summary>
public readonly record struct AxisFrame(Vec3 U, Vec3 V, Vec3 W)
{
    public Vec3 ToLocal(Vec3 p) => new(Vec3.Dot(p, U), Vec3.Dot(p, V), Vec3.Dot(p, W));

    public Vec3 ToWorld(double u, double v, double w) => (U * u) + (V * v) + (W * w);
}

/// <summary>
/// Axis convention used by every PicoMoldForge Parts recipe: ISO 841 machine axes.
/// Linear axes X, Y, Z form a right-handed system with Z as the tool/build axis.
/// Rotary axes A, B, C rotate about X, Y, Z with the right-hand rule.
/// Combined orientations are expressed with fixed (extrinsic) axes applied in the order A, then B, then C:
/// R = Rz(C) * Ry(B) * Rx(A). Rotations do not commute, so the order is part of the contract.
/// </summary>
public static class AxisConvention
{
    public const string Name = "ISO-841";

    public const string OrientationOrder = "fixed-axes A->B->C (R = Rz(C) * Ry(B) * Rx(A))";

    public static LinearAxis AxisOf(RotaryAxis axis) => axis switch
    {
        RotaryAxis.A => LinearAxis.X,
        RotaryAxis.B => LinearAxis.Y,
        RotaryAxis.C => LinearAxis.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    public static RotaryAxis RotaryAbout(LinearAxis axis) => axis switch
    {
        LinearAxis.X => RotaryAxis.A,
        LinearAxis.Y => RotaryAxis.B,
        LinearAxis.Z => RotaryAxis.C,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    public static Vec3 Unit(LinearAxis axis) => axis switch
    {
        LinearAxis.X => Vec3.UnitX,
        LinearAxis.Y => Vec3.UnitY,
        LinearAxis.Z => Vec3.UnitZ,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    /// <summary>Cyclic right-handed frames: Z -> (X, Y, Z), X -> (Y, Z, X), Y -> (Z, X, Y).</summary>
    public static AxisFrame Frame(LinearAxis axis) => axis switch
    {
        LinearAxis.Z => new AxisFrame(Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ),
        LinearAxis.X => new AxisFrame(Vec3.UnitY, Vec3.UnitZ, Vec3.UnitX),
        LinearAxis.Y => new AxisFrame(Vec3.UnitZ, Vec3.UnitX, Vec3.UnitY),
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };

    /// <summary>Right-hand rotation about a rotary axis by an angle in radians.</summary>
    public static Mat3 Rotation(RotaryAxis axis, double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);

        return axis switch
        {
            RotaryAxis.A => new Mat3(1, 0, 0, 0, c, -s, 0, s, c),
            RotaryAxis.B => new Mat3(c, 0, s, 0, 1, 0, -s, 0, c),
            RotaryAxis.C => new Mat3(c, -s, 0, s, c, 0, 0, 0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };
    }

    public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    public static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

    public static bool TryParseLinear(string? text, out LinearAxis axis)
    {
        switch (text?.Trim().ToUpperInvariant())
        {
            case "X":
                axis = LinearAxis.X;
                return true;
            case "Y":
                axis = LinearAxis.Y;
                return true;
            case "Z":
                axis = LinearAxis.Z;
                return true;
            default:
                axis = default;
                return false;
        }
    }

    public static bool TryParseRotary(string? text, out RotaryAxis axis)
    {
        switch (text?.Trim().ToUpperInvariant())
        {
            case "A":
                axis = RotaryAxis.A;
                return true;
            case "B":
                axis = RotaryAxis.B;
                return true;
            case "C":
                axis = RotaryAxis.C;
                return true;
            default:
                axis = default;
                return false;
        }
    }
}

/// <summary>
/// Orientation expressed as ISO 841 rotary angles in degrees, applied about fixed axes in the order
/// A (about X), then B (about Y), then C (about Z): R = Rz(C) * Ry(B) * Rx(A).
/// </summary>
public readonly record struct Orientation(double ADeg, double BDeg, double CDeg)
{
    public static readonly Orientation None = new(0, 0, 0);

    public bool IsNone => ADeg == 0 && BDeg == 0 && CDeg == 0;

    public Mat3 ToMatrix() =>
        AxisConvention.Rotation(RotaryAxis.C, AxisConvention.DegreesToRadians(CDeg)) *
        AxisConvention.Rotation(RotaryAxis.B, AxisConvention.DegreesToRadians(BDeg)) *
        AxisConvention.Rotation(RotaryAxis.A, AxisConvention.DegreesToRadians(ADeg));
}
