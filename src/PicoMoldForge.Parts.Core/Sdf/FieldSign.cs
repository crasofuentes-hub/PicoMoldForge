using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Sdf;

/// <summary>
/// Inside/outside classification of field samples, with a deterministic tie-break for exact zeros.
/// A sample can sit exactly on a zero of the field without being on the surface: where two added features
/// only touch, both fields are 0 on the shared face although that face lies inside the solid; where a cut is
/// flush with a face, the field is 0 on a face that lies outside. Such a sample takes the sign of a point
/// nudged along a fixed generic direction (simulation of simplicity), so touching features join and flush cuts
/// leave no skin. Samples with a nonzero value are classified by their sign alone, as before.
/// </summary>
public static class FieldSign
{
    /// <summary>Nudge length as a fraction of the sampling step.</summary>
    public const double NudgeFraction = 1e-7;

    /// <summary>Fixed direction, parallel to no axis, face diagonal or space diagonal.</summary>
    public static readonly Vec3 NudgeDirection = new Vec3(0.70710678, 0.57735027, 0.44721360).Normalized();

    /// <summary>
    /// True when <paramref name="point"/> counts as inside. <paramref name="value"/> must be
    /// <c>sdf.Distance(point)</c>; <paramref name="stepMm"/> is the sampling step that scales the nudge.
    /// </summary>
    public static bool IsInside(ISdf sdf, Vec3 point, double value, double stepMm)
    {
        ArgumentNullException.ThrowIfNull(sdf);

        if (value != 0)
        {
            return value < 0;
        }

        return sdf.Distance(point + (NudgeDirection * (stepMm * NudgeFraction))) < 0;
    }

    /// <summary>
    /// True when a sample with an exact zero lies on the real boundary: the nudged points on both sides of it
    /// fall on different sides. A zero on a face shared by touching features (inside on both sides) or on a
    /// flush cut (outside on both sides) is not a boundary point.
    /// </summary>
    public static bool IsBoundaryZero(ISdf sdf, Vec3 point, double stepMm)
    {
        ArgumentNullException.ThrowIfNull(sdf);

        var nudge = NudgeDirection * (stepMm * NudgeFraction);
        return (sdf.Distance(point + nudge) < 0) != (sdf.Distance(point - nudge) < 0);
    }
}
