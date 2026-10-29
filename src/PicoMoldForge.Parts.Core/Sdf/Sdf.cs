using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Sdf;

/// <summary>
/// Solid described by a signed distance bound: negative inside, positive outside, zero on the surface.
/// Contract (what the mesher, the volume estimator and CSG culling rely on):
/// <list type="bullet">
/// <item>The sign is exact.</item>
/// <item>|Distance(p)| &lt;= Lipschitz * (true Euclidean distance from p to the surface). Exact fields have
/// Lipschitz = 1; a larger factor allows sampled profiles that may overestimate slightly. Values can
/// underestimate the distance (near feature edges, in twisted sweeps), so they are not a distance measurement.</item>
/// <item>Outside <see cref="Bounds"/>, Distance(p) * Lipschitz &gt;= Bounds.DistanceOutside(p).</item>
/// </list>
/// The geometric distance from a point to the surface is measured with
/// <see cref="Meshing.SurfaceDistanceProbe"/> (bisection on the exact sign), not read from the value.
/// Implementations must be immutable and thread-safe: the mesher and PicoGK evaluate them in parallel.
/// </summary>
public interface ISdf
{
    double Distance(Vec3 p);

    Box3 Bounds { get; }

    double Lipschitz { get; }
}

/// <summary>Rigidly transformed solid: world = Transform.Apply(local).</summary>
public sealed class TransformedSdf : ISdf
{
    private readonly ISdf _inner;
    private readonly RigidTransform _transform;
    private readonly Mat3 _inverseRotation;

    public TransformedSdf(ISdf inner, RigidTransform transform)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _transform = transform;
        _inverseRotation = transform.Rotation.Transposed();
        Bounds = inner.Bounds.Transform(transform);
    }

    public Box3 Bounds { get; }

    public double Lipschitz => _inner.Lipschitz;

    public double Distance(Vec3 p) => _inner.Distance(_inverseRotation * (p - _transform.Translation));
}

/// <summary>Union of several solids (exact minimum of the children).</summary>
public sealed class UnionSdf : ISdf
{
    private readonly ISdf[] _children;
    private readonly Box3[] _childBounds;
    private readonly double[] _childLipschitz;

    public UnionSdf(IEnumerable<ISdf> children)
    {
        _children = children?.ToArray() ?? throw new ArgumentNullException(nameof(children));

        if (_children.Length == 0)
        {
            throw new ArgumentException("A union needs at least one child.", nameof(children));
        }

        _childBounds = _children.Select(child => child.Bounds).ToArray();
        _childLipschitz = _children.Select(child => child.Lipschitz).ToArray();
        Bounds = _childBounds.Skip(1).Aggregate(_childBounds[0], (box, next) => box.Union(next));
        Lipschitz = _childLipschitz.Max();
    }

    public Box3 Bounds { get; }

    public double Lipschitz { get; }

    public double Distance(Vec3 p)
    {
        var d = double.PositiveInfinity;

        for (var i = 0; i < _children.Length; i++)
        {
            // Outside its bounds a child's value is at least boxDistance / Lipschitz, so it cannot win the
            // minimum when that lower bound already exceeds d. The result stays exactly min(children).
            var boxDistance = _childBounds[i].DistanceOutside(p);

            if (boxDistance > 0 && boxDistance / _childLipschitz[i] >= d)
            {
                continue;
            }

            d = Math.Min(d, _children[i].Distance(p));
        }

        return d;
    }
}

public enum CsgOperation
{
    Add,
    Subtract,
    Intersect
}

/// <summary>
/// Left-fold constructive solid geometry: result = op_n(... op_2(op_1(first, s1), s2) ..., sn).
/// The first step must be an Add. Bounding-box lower bounds are used to skip children that provably cannot
/// change the result at a point; children are always evaluated inside their bounds, so the value is exactly
/// the min/max fold of the children (no approximation is introduced by the culling).
/// </summary>
public sealed class CsgSdf : ISdf
{
    private readonly CsgOperation[] _operations;
    private readonly ISdf[] _solids;
    private readonly Box3[] _bounds;
    private readonly double[] _lipschitz;

    public CsgSdf(IReadOnlyList<(CsgOperation Operation, ISdf Solid)> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
        {
            throw new ArgumentException("CSG needs at least one step.", nameof(steps));
        }

        if (steps[0].Operation != CsgOperation.Add)
        {
            throw new ArgumentException("The first CSG step must add material.", nameof(steps));
        }

        _operations = steps.Select(step => step.Operation).ToArray();
        _solids = steps.Select(step => step.Solid ?? throw new ArgumentException("CSG step solid cannot be null.")).ToArray();
        _bounds = _solids.Select(solid => solid.Bounds).ToArray();
        _lipschitz = _solids.Select(solid => solid.Lipschitz).ToArray();

        var bounds = _bounds[0];

        for (var i = 1; i < _solids.Length; i++)
        {
            if (_operations[i] == CsgOperation.Add)
            {
                bounds = bounds.Union(_bounds[i]);
            }
        }

        Bounds = bounds;
        Lipschitz = _lipschitz.Max();
    }

    public Box3 Bounds { get; }

    public double Lipschitz { get; }

    public double Distance(Vec3 p)
    {
        var d = _solids[0].Distance(p);

        for (var i = 1; i < _solids.Length; i++)
        {
            switch (_operations[i])
            {
                case CsgOperation.Add:
                {
                    var boxDistance = _bounds[i].DistanceOutside(p);

                    if (boxDistance > 0 && boxDistance / _lipschitz[i] >= d)
                    {
                        break;
                    }

                    d = Math.Min(d, _solids[i].Distance(p));
                    break;
                }

                case CsgOperation.Subtract:
                {
                    var boxDistance = _bounds[i].DistanceOutside(p);

                    if (boxDistance > 0 && boxDistance / _lipschitz[i] >= -d)
                    {
                        break;
                    }

                    d = Math.Max(d, -_solids[i].Distance(p));
                    break;
                }

                case CsgOperation.Intersect:
                {
                    d = Math.Max(d, _solids[i].Distance(p));
                    break;
                }

                default:
                    throw new InvalidOperationException("Unknown CSG operation.");
            }
        }

        return d;
    }
}
