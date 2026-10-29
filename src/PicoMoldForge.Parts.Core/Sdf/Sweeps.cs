using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;

namespace PicoMoldForge.Parts.Sdf;

/// <summary>
/// Linear sweep ("dejar caer"): a 2D profile in the (U, V) plane of a linear axis moves along that axis
/// from <c>From</c> to <c>To</c>. An optional twist rotates the profile about the same axis while it moves
/// (linear + rotary motion, e.g. Z + C for helical gears). Twist is the total right-hand rotation accumulated
/// from <c>From</c> to <c>To</c>. Coordinates are local to the feature (instances place it in the world).
/// </summary>
public sealed class LinearSweepSdf : ISdf
{
    private readonly IProfile2D _profile;
    private readonly AxisFrame _frame;
    private readonly double _low;
    private readonly double _high;
    private readonly double _twistPerUnit;
    private readonly double _twistRadius;
    private readonly double _inverseTwistStretch;

    public LinearSweepSdf(IProfile2D profile, LinearAxis axis, double from, double to, double twistRadians)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));

        if (!double.IsFinite(from) || !double.IsFinite(to) || from == to)
        {
            throw new ArgumentException("A linear sweep needs two different finite positions (from, to).");
        }

        if (!double.IsFinite(twistRadians))
        {
            throw new ArgumentException("Twist must be finite.", nameof(twistRadians));
        }

        Axis = axis;
        From = from;
        To = to;
        TwistRadians = twistRadians;
        _frame = AxisConvention.Frame(axis);
        _low = Math.Min(from, to);
        _high = Math.Max(from, to);
        _twistPerUnit = twistRadians / (to - from);

        var profileBounds = profile.Bounds;
        Box2 planeBounds;

        if (twistRadians != 0)
        {
            _twistRadius = profileBounds.MaxRadiusFromOrigin() * (1 + 1e-9) + 1e-9;
            planeBounds = new Box2(new Vec2(-_twistRadius, -_twistRadius), new Vec2(_twistRadius, _twistRadius));

            // The twist maps world points to profile coordinates with Jacobian [[Rot, -k Rot' x], [0, 1]]. Its
            // largest singular value is that of a shear of factor c = k r: c / 2 + sqrt(1 + c^2 / 4), maximal at
            // the twist radius. Dividing by it keeps the value a lower bound of the profile-space distance.
            var shear = _twistRadius * Math.Abs(_twistPerUnit);
            var twistStretch = (shear / 2) + Math.Sqrt(1 + (shear * shear / 4));
            _inverseTwistStretch = 1.0 / twistStretch;
            Lipschitz = profile.Lipschitz * twistStretch;
        }
        else
        {
            planeBounds = profileBounds;
            _inverseTwistStretch = 1.0;
            Lipschitz = profile.Lipschitz;
        }
        Bounds = Box3.FromPoints(new[]
        {
            _frame.ToWorld(planeBounds.Min.X, planeBounds.Min.Y, _low),
            _frame.ToWorld(planeBounds.Max.X, planeBounds.Max.Y, _high)
        });
    }

    public LinearAxis Axis { get; }

    public double From { get; }

    public double To { get; }

    public double TwistRadians { get; }

    public Box3 Bounds { get; }

    public double Lipschitz { get; }

    public double Distance(Vec3 p)
    {
        var u = Vec3.Dot(p, _frame.U);
        var v = Vec3.Dot(p, _frame.V);
        var w = Vec3.Dot(p, _frame.W);
        var slab = Math.Max(_low - w, w - _high);

        if (_twistPerUnit != 0)
        {
            var radiusSquared = (u * u) + (v * v);

            if (radiusSquared > _twistRadius * _twistRadius)
            {
                // Outside the cylinder that contains the twisted solid: exact lower bound.
                var radial = Math.Sqrt(radiusSquared) - _twistRadius;
                var axial = Math.Max(slab, 0);
                return Math.Sqrt((radial * radial) + (axial * axial));
            }

            var angle = -_twistPerUnit * (w - From);
            var c = Math.Cos(angle);
            var s = Math.Sin(angle);
            var ru = (c * u) - (s * v);
            var rv = (s * u) + (c * v);
            return Combine(_profile.Distance(new Vec2(ru, rv)), slab) * _inverseTwistStretch;
        }

        return Combine(_profile.Distance(new Vec2(u, v)), slab);
    }

    /// <summary>Profile angle (radians, right-hand about the axis) at a position along the axis.</summary>
    public double TwistAt(double w) => _twistPerUnit * (w - From);

    private static double Combine(double profileDistance, double slabDistance)
    {
        var ox = Math.Max(profileDistance, 0);
        var oy = Math.Max(slabDistance, 0);
        return Math.Min(Math.Max(profileDistance, slabDistance), 0) + Math.Sqrt((ox * ox) + (oy * oy));
    }
}

/// <summary>
/// Rotary sweep ("girar"): a 2D profile in the (radial, axial) plane turns about a rotary axis
/// (A about X, B about Y, C about Z) from <c>Start</c> to <c>End</c> (right-hand rule, measured from the
/// frame's U direction toward V). A full turn produces a solid of revolution.
/// Profiles that touch the axis must be full axial sections (mirrored to negative radii) so the axis is not
/// treated as a boundary; see <see cref="PolygonProfile.FromHalfSection"/>.
/// </summary>
public sealed class RotarySweepSdf : ISdf
{
    private readonly IProfile2D _profile;
    private readonly AxisFrame _frame;
    private readonly bool _full;
    private readonly Vec2 _startDirection;
    private readonly Vec2 _endDirection;
    private readonly double _span;

    public RotarySweepSdf(IProfile2D profile, RotaryAxis axis, double startRadians, double endRadians)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));

        if (!double.IsFinite(startRadians) || !double.IsFinite(endRadians) || startRadians == endRadians)
        {
            throw new ArgumentException("A rotary sweep needs two different finite angles.");
        }

        Axis = axis;
        StartRadians = Math.Min(startRadians, endRadians);
        EndRadians = Math.Max(startRadians, endRadians);
        _span = EndRadians - StartRadians;
        _full = _span >= (2 * Math.PI) - 1e-12;
        _frame = AxisConvention.Frame(AxisConvention.AxisOf(axis));
        _startDirection = new Vec2(Math.Cos(StartRadians), Math.Sin(StartRadians));
        _endDirection = new Vec2(Math.Cos(EndRadians), Math.Sin(EndRadians));

        var profileBounds = profile.Bounds;
        var radius = Math.Max(Math.Abs(profileBounds.Min.X), Math.Abs(profileBounds.Max.X));
        Bounds = Box3.FromPoints(new[]
        {
            _frame.ToWorld(-radius, -radius, profileBounds.Min.Y),
            _frame.ToWorld(radius, radius, profileBounds.Max.Y)
        });
        Lipschitz = profile.Lipschitz;
    }

    public RotaryAxis Axis { get; }

    public double StartRadians { get; }

    public double EndRadians { get; }

    public bool IsFullTurn => _full;

    public Box3 Bounds { get; }

    public double Lipschitz { get; }

    public double Distance(Vec3 p)
    {
        var u = Vec3.Dot(p, _frame.U);
        var v = Vec3.Dot(p, _frame.V);
        var w = Vec3.Dot(p, _frame.W);
        var radial = Math.Sqrt((u * u) + (v * v));
        var profileDistance = _profile.Distance(new Vec2(radial, w));

        if (_full)
        {
            return profileDistance;
        }

        return Math.Max(profileDistance, WedgeDistance(new Vec2(u, v)));
    }

    private double WedgeDistance(Vec2 q)
    {
        var angle = Math.Atan2(q.Y, q.X);
        var relative = angle - StartRadians;
        relative -= 2 * Math.PI * Math.Floor(relative / (2 * Math.PI));
        var inside = relative <= _span;
        var distance = Math.Min(DistanceToRay(q, _startDirection), DistanceToRay(q, _endDirection));
        return inside ? -distance : distance;
    }

    private static double DistanceToRay(Vec2 q, Vec2 direction)
    {
        var along = Vec2.Dot(q, direction);
        return along <= 0 ? q.Length : Math.Abs(Vec2.Cross(direction, q));
    }
}
