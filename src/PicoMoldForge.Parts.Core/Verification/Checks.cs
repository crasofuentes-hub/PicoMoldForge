using System.Globalization;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Verification;

public sealed record PartCheckResult(string Id, string Description, string Expected, string Actual, bool Passed);

public sealed record PartCheckContext(ISdf Sdf, double ResolutionMm);

/// <summary>A verification that the generated solid matches what the parameters promise.</summary>
public interface IPartCheck
{
    string Id { get; }

    string Description { get; }

    PartCheckResult Evaluate(PartCheckContext context);
}

internal static class CheckFormat
{
    public static string Mm(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture) + " mm";

    public static string Deg(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture) + " deg";
}

/// <summary>The surface must pass through a point (verifies a dimension of the solid).</summary>
public sealed class SurfacePointCheck : IPartCheck
{
    private readonly Vec3 _point;
    private readonly double _toleranceMm;

    public SurfacePointCheck(string id, string description, Vec3 point, double toleranceMm = 0.002)
    {
        Id = id;
        Description = description;
        _point = point;
        _toleranceMm = toleranceMm;
    }

    public string Id { get; }

    public string Description { get; }

    public PartCheckResult Evaluate(PartCheckContext context)
    {
        var distance = context.Sdf.Distance(_point);
        return new PartCheckResult(
            Id,
            Description,
            $"|distance| <= {CheckFormat.Mm(_toleranceMm)} at {_point}",
            CheckFormat.Mm(distance),
            Math.Abs(distance) <= _toleranceMm);
    }
}

/// <summary>A point must be inside (material) or outside (empty) the solid by at least a margin.</summary>
public sealed class ContainmentCheck : IPartCheck
{
    private readonly Vec3 _point;
    private readonly bool _inside;
    private readonly double _marginMm;

    public ContainmentCheck(string id, string description, Vec3 point, bool inside, double marginMm = 0.01)
    {
        Id = id;
        Description = description;
        _point = point;
        _inside = inside;
        _marginMm = marginMm;
    }

    public string Id { get; }

    public string Description { get; }

    public PartCheckResult Evaluate(PartCheckContext context)
    {
        var distance = context.Sdf.Distance(_point);
        var passed = _inside ? distance <= -_marginMm : distance >= _marginMm;
        return new PartCheckResult(
            Id,
            Description,
            (_inside ? "material (distance <= -" : "empty (distance >= ") + CheckFormat.Mm(_marginMm) + $") at {_point}",
            CheckFormat.Mm(distance),
            passed);
    }
}

/// <summary>Counts teeth by sign changes of the solid along a circle in a plane z = const.</summary>
public sealed class CircleCrossingCountCheck : IPartCheck
{
    private readonly double _radius;
    private readonly double _z;
    private readonly int _expectedCrossings;

    public CircleCrossingCountCheck(string id, string description, double radius, double z, int expectedCrossings)
    {
        Id = id;
        Description = description;
        _radius = radius;
        _z = z;
        _expectedCrossings = expectedCrossings;
    }

    public string Id { get; }

    public string Description { get; }

    public PartCheckResult Evaluate(PartCheckContext context)
    {
        const int samples = 36000;
        var crossings = 0;
        var previous = context.Sdf.Distance(new Vec3(_radius, 0, _z)) < 0;

        for (var i = 1; i <= samples; i++)
        {
            var angle = 2 * Math.PI * i / samples;
            var inside = context.Sdf.Distance(new Vec3(_radius * Math.Cos(angle), _radius * Math.Sin(angle), _z)) < 0;

            if (inside != previous)
            {
                crossings++;
            }

            previous = inside;
        }

        return new PartCheckResult(
            Id,
            Description,
            _expectedCrossings.ToString(CultureInfo.InvariantCulture) + " surface crossings",
            crossings.ToString(CultureInfo.InvariantCulture) + " surface crossings",
            crossings == _expectedCrossings);
    }
}

/// <summary>Generic check with a custom evaluation (used for gear-specific measurements).</summary>
public sealed class DelegateCheck : IPartCheck
{
    private readonly Func<PartCheckContext, (string Expected, string Actual, bool Passed)> _evaluate;

    public DelegateCheck(string id, string description, Func<PartCheckContext, (string Expected, string Actual, bool Passed)> evaluate)
    {
        Id = id;
        Description = description;
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
    }

    public string Id { get; }

    public string Description { get; }

    public PartCheckResult Evaluate(PartCheckContext context)
    {
        var (expected, actual, passed) = _evaluate(context);
        return new PartCheckResult(Id, Description, expected, actual, passed);
    }
}
