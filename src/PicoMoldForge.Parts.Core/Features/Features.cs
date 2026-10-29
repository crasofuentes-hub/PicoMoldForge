using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Features;

public enum FeatureOperation
{
    Add,
    Subtract,
    Intersect
}

/// <summary>Trajectory of a profile: linear along X/Y/Z (optionally with simultaneous rotation) or rotary about A/B/C.</summary>
public abstract record SweepDefinition
{
    public abstract string AxisName { get; }

    public abstract ISdf BuildSdf(IProfile2D profile);
}

/// <summary>
/// Linear sweep along an axis from <see cref="From"/> to <see cref="To"/> (the direction of motion).
/// <see cref="TwistDeg"/> is the right-hand rotation about the same axis accumulated over the motion
/// (for example Z with C for a helical gear).
/// </summary>
public sealed record LinearSweepDefinition(LinearAxis Axis, double From, double To, double TwistDeg = 0) : SweepDefinition
{
    public override string AxisName => TwistDeg == 0 ? Axis.ToString() : $"{Axis}+{AxisConvention.RotaryAbout(Axis)}";

    public override ISdf BuildSdf(IProfile2D profile) =>
        new LinearSweepSdf(profile, Axis, From, To, AxisConvention.DegreesToRadians(TwistDeg));
}

/// <summary>Rotary sweep about A (X), B (Y) or C (Z) from <see cref="StartDeg"/> to <see cref="EndDeg"/>.</summary>
public sealed record RotarySweepDefinition(RotaryAxis Axis, double StartDeg = 0, double EndDeg = 360) : SweepDefinition
{
    public override string AxisName => Axis.ToString();

    public override ISdf BuildSdf(IProfile2D profile) =>
        new RotarySweepSdf(profile, Axis, AxisConvention.DegreesToRadians(StartDeg), AxisConvention.DegreesToRadians(EndDeg));
}

/// <summary>
/// One modeling step: a profile moved along a trajectory, combined with the part so far.
/// The sweep lives in feature-local coordinates; each instance places a copy in the world
/// (world = instance.Apply(local)). Patterns are expanded into instances.
/// </summary>
public sealed record FeatureDefinition(
    string Id,
    string Label,
    FeatureOperation Operation,
    SweepDefinition Sweep,
    IProfile2D Profile,
    IReadOnlyList<RigidTransform> Instances)
{
    public ISdf BuildSdf()
    {
        if (Instances.Count == 0)
        {
            throw new InvalidOperationException($"Feature '{Id}' has no instances.");
        }

        var local = Sweep.BuildSdf(Profile);
        var placed = Instances
            .Select(instance => instance.Rotation.IsIdentity() && instance.Translation == Vec3.Zero
                ? local
                : (ISdf)new TransformedSdf(local, instance))
            .ToArray();

        return placed.Length == 1 ? placed[0] : new UnionSdf(placed);
    }
}

/// <summary>Instance placement helpers (patterns about ISO 841 axes).</summary>
public static class Placement
{
    public static IReadOnlyList<RigidTransform> Single(Vec3 origin, Orientation orientation = default) =>
        new[] { new RigidTransform(orientation.ToMatrix(), origin) };

    /// <summary>Copies rotated about a rotary axis through <paramref name="center"/> (right-hand rule).</summary>
    public static IReadOnlyList<RigidTransform> Circular(
        IReadOnlyList<RigidTransform> bases,
        RotaryAxis axis,
        int count,
        double startDeg,
        double spanDeg,
        Vec3 center)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Pattern count must be at least 1.");
        }

        var full = Math.Abs(Math.Abs(spanDeg) - 360) < 1e-9;
        var stepDeg = count == 1 ? 0 : full ? spanDeg / count : spanDeg / (count - 1);
        var result = new List<RigidTransform>();

        for (var i = 0; i < count; i++)
        {
            var rotation = AxisConvention.Rotation(axis, AxisConvention.DegreesToRadians(startDeg + (i * stepDeg)));
            var about = new RigidTransform(rotation, center - (rotation * center));

            foreach (var placement in bases)
            {
                result.Add(about.After(placement));
            }
        }

        return result;
    }

    /// <summary>Copies translated along a linear axis by multiples of <paramref name="step"/>.</summary>
    public static IReadOnlyList<RigidTransform> Linear(IReadOnlyList<RigidTransform> bases, LinearAxis axis, int count, double step)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Pattern count must be at least 1.");
        }

        var direction = AxisConvention.Unit(axis);
        var result = new List<RigidTransform>();

        for (var i = 0; i < count; i++)
        {
            var shift = RigidTransform.FromTranslation(direction * (i * step));

            foreach (var placement in bases)
            {
                result.Add(shift.After(placement));
            }
        }

        return result;
    }
}
