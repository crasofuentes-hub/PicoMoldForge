using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;

namespace PicoMoldForge.Parts.Gears;

/// <summary>
/// Transverse tooth profile of an involute gear (teeth only, no bore), generated the way a gear is cut:
/// a basic rack cutter (ISO 53 proportions, rounded tip) rolls without slipping on the pitch circle and the
/// gear is whatever material of the blank survives every rack position. The involute flank, the trochoidal
/// root fillet, undercut and profile shift therefore come out of the generation itself, not from separate
/// formulas. The exact distance field is precomputed on a polar raster over one half tooth (the profile is
/// symmetric and z-periodic) and interpolated bilinearly.
/// Tooth 0 is centered on the +X axis (C = 0); tooth k is centered at C = k * 360 / z.
/// </summary>
public sealed class GearToothProfile : IProfile2D
{
    private const double GoldenRatioConjugate = 0.6180339887498949;

    private readonly RackCutter _cutter;
    private readonly double _pitchRadius;
    private readonly double _referenceLine;
    private readonly double _rhoMin;
    private readonly double _rhoMax;
    private readonly double _halfSector;
    private readonly double _period;
    private readonly int _rhoCount;
    private readonly int _thetaCount;
    private readonly double _rhoStep;
    private readonly double _thetaStep;
    private readonly double[] _values;

    public GearToothProfile(GearSpec spec, double? rasterStepMm = null)
    {
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));

        var errors = spec.Validate();

        if (errors.Count > 0)
        {
            throw new ArgumentException("Invalid gear: " + string.Join(" ", errors), nameof(spec));
        }

        var module = spec.NormalModule;
        RasterStepMm = rasterStepMm ?? (module / 80.0);

        if (!(RasterStepMm > 0) || RasterStepMm > module / 4)
        {
            throw new ArgumentOutOfRangeException(nameof(rasterStepMm), "Raster step must be positive and at most module / 4.");
        }

        _pitchRadius = spec.PitchRadius;
        _referenceLine = spec.PitchRadius + spec.ProfileShiftMm;
        TipRadius = spec.TipRadius;
        RootRadius = spec.RootRadius;

        _cutter = new RackCutter(
            pitch: spec.TransversePitch,
            toothThicknessAtReference: (spec.TransversePitch / 2.0) + spec.ThicknessAllowanceMm,
            pressureAngleRad: spec.TransversePressureAngleRad,
            tipDepth: spec.DedendumMm,
            topHeight: spec.AddendumMm + (2.5 * module),
            requestedTipFilletRadius: spec.CutterTipFilletRadius);

        EffectiveTipFilletRadius = _cutter.TipFilletRadius;

        _halfSector = Math.PI / spec.Teeth;
        _period = 2.0 * Math.PI / spec.Teeth;
        _rhoMin = Math.Max(RootRadius - (0.5 * module), 1e-3);
        _rhoMax = TipRadius + (0.5 * module);
        _rhoCount = (int)Math.Ceiling((_rhoMax - _rhoMin) / RasterStepMm) + 1;
        _rhoStep = (_rhoMax - _rhoMin) / (_rhoCount - 1);
        _thetaCount = Math.Max(16, (int)Math.Ceiling(_rhoMax * _halfSector / RasterStepMm) + 1);
        _thetaStep = _halfSector / (_thetaCount - 1);
        _values = new double[_rhoCount * _thetaCount];

        Parallel.For(0, _rhoCount, i =>
        {
            var rho = _rhoMin + (i * _rhoStep);

            for (var j = 0; j < _thetaCount; j++)
            {
                _values[(i * _thetaCount) + j] = ExactDistancePolar(rho, j * _thetaStep);
            }
        });

        Bounds = new Box2(new Vec2(-TipRadius, -TipRadius), new Vec2(TipRadius, TipRadius));
    }

    public GearSpec Spec { get; }

    public double RasterStepMm { get; }

    public double TipRadius { get; }

    public double RootRadius { get; }

    /// <summary>Rack tip fillet radius actually used (reduced only if the requested one does not fit).</summary>
    public double EffectiveTipFilletRadius { get; }

    public Box2 Bounds { get; }

    /// <summary>Bilinear interpolation of an exact distance raster in polar coordinates (conservative bound).</summary>
    public double Lipschitz => 1.25;

    public double Distance(Vec2 q)
    {
        var rho = Math.Sqrt((q.X * q.X) + (q.Y * q.Y));

        if (rho >= _rhoMax)
        {
            return rho - TipRadius;
        }

        if (rho <= _rhoMin)
        {
            return rho - RootRadius;
        }

        var theta = Math.Atan2(q.Y, q.X);
        var folded = theta - (_period * Math.Floor(theta / _period));

        if (folded > _halfSector)
        {
            folded = _period - folded;
        }

        var fi = (rho - _rhoMin) / _rhoStep;
        var i0 = Math.Clamp((int)fi, 0, _rhoCount - 2);
        var tx = fi - i0;
        var fj = folded / _thetaStep;
        var j0 = Math.Clamp((int)fj, 0, _thetaCount - 2);
        var ty = fj - j0;

        var row0 = i0 * _thetaCount;
        var row1 = row0 + _thetaCount;
        var v00 = _values[row0 + j0];
        var v01 = _values[row0 + j0 + 1];
        var v10 = _values[row1 + j0];
        var v11 = _values[row1 + j0 + 1];
        var a = v00 + ((v01 - v00) * ty);
        var b = v10 + ((v11 - v10) * ty);
        return a + ((b - a) * tx);
    }

    /// <summary>Exact (non-interpolated) generated distance at a point. Slow; for verification.</summary>
    public double ExactDistance(Vec2 q) => ExactDistancePolar(q.Length, Math.Atan2(q.Y, q.X));

    private double ExactDistancePolar(double rho, double theta)
    {
        var blank = rho - TipRadius;
        var cut = MinimumCutterDistance(rho, theta);
        return Math.Max(blank, -cut);
    }

    /// <summary>Distance from the gear-frame point to the union of all rack cutter positions.</summary>
    private double MinimumCutterDistance(double rho, double theta)
    {
        var module = Spec.NormalModule;
        var reach = Math.Clamp((RootRadius - (2.0 * module)) / Math.Max(rho, 1e-9), -1.0, 1.0);
        var window = Math.Acos(reach) + 0.35;
        var low = -window - theta;
        var high = window - theta;
        var coarseStep = (module / 8.0) / (rho + _pitchRadius);
        var count = Math.Clamp((int)Math.Ceiling((high - low) / coarseStep) + 1, 16, 20000);
        var delta = (high - low) / (count - 1);

        Span<double> samples = count <= 4096 ? stackalloc double[count] : new double[count];
        var best = double.PositiveInfinity;

        for (var i = 0; i < count; i++)
        {
            var value = CutterDistance(rho, theta, low + (i * delta));
            samples[i] = value;
            best = Math.Min(best, value);
        }

        var threshold = best + (module / 4.0);
        var result = best;

        for (var i = 0; i < count; i++)
        {
            var value = samples[i];

            if (value > threshold)
            {
                continue;
            }

            var isLocalMinimum = (i == 0 || value <= samples[i - 1]) && (i == count - 1 || value <= samples[i + 1]);

            if (!isLocalMinimum)
            {
                continue;
            }

            var a = low + (Math.Max(i - 1, 0) * delta);
            var b = low + (Math.Min(i + 1, count - 1) * delta);
            result = Math.Min(result, GoldenSectionMinimum(rho, theta, a, b));
        }

        return result;
    }

    private double GoldenSectionMinimum(double rho, double theta, double a, double b)
    {
        var c = b - (GoldenRatioConjugate * (b - a));
        var d = a + (GoldenRatioConjugate * (b - a));
        var fc = CutterDistance(rho, theta, c);
        var fd = CutterDistance(rho, theta, d);

        for (var iteration = 0; iteration < 60 && (b - a) > 1e-13; iteration++)
        {
            if (fc < fd)
            {
                b = d;
                d = c;
                fd = fc;
                c = b - (GoldenRatioConjugate * (b - a));
                fc = CutterDistance(rho, theta, c);
            }
            else
            {
                a = c;
                c = d;
                fc = fd;
                d = a + (GoldenRatioConjugate * (b - a));
                fd = CutterDistance(rho, theta, d);
            }
        }

        return Math.Min(fc, fd);
    }

    /// <summary>
    /// Gear rotated by +phi (right-hand about Z) while the rack translates by pitchRadius * phi along +Y.
    /// Rack coordinates: s along the rack (teeth centered at s = k * pitch), t normal to the reference line
    /// (t &lt; 0 toward the gear center). At phi = 0 a gap of the rack faces gear tooth 0 on +X.
    /// </summary>
    private double CutterDistance(double rho, double theta, double phi)
    {
        var angle = theta + phi;
        var x = rho * Math.Cos(angle);
        var y = rho * Math.Sin(angle);
        var s = y - (_pitchRadius * phi) - (_cutter.Pitch / 2.0);
        var t = x - _referenceLine;
        return _cutter.Distance(s, t);
    }
}

/// <summary>
/// Basic rack cutter: periodic trapezoidal teeth (pressure angle alpha) with rounded tips, in rack coordinates.
/// The tooth centered at s = 0 has half width w(t) = thickness/2 + t * tan(alpha) and its tip at t = -tipDepth.
/// </summary>
internal sealed class RackCutter
{
    private readonly Vec2[] _inset;

    public RackCutter(double pitch, double toothThicknessAtReference, double pressureAngleRad, double tipDepth, double topHeight, double requestedTipFilletRadius)
    {
        Pitch = pitch;
        var tanAlpha = Math.Tan(pressureAngleRad);
        var secAlpha = 1.0 / Math.Cos(pressureAngleRad);
        var halfReference = toothThicknessAtReference / 2.0;
        var margin = 1e-3 * pitch;

        // Largest fillet that still leaves a positive inset tip width.
        var maxFillet = (margin - halfReference + (tipDepth * tanAlpha)) / (tanAlpha - secAlpha);
        TipFilletRadius = Math.Max(0.0, Math.Min(requestedTipFilletRadius, maxFillet));

        var rho = TipFilletRadius;
        var bottom = -tipDepth + rho;
        var top = topHeight - rho;
        var halfBottom = halfReference + (bottom * tanAlpha) - (rho * secAlpha);
        var halfTop = halfReference + (top * tanAlpha) - (rho * secAlpha);

        if (!(halfBottom > 0))
        {
            throw new ArgumentException("Rack cutter tooth is too thin for the requested proportions.");
        }

        _inset = new[]
        {
            new Vec2(-halfBottom, bottom),
            new Vec2(halfBottom, bottom),
            new Vec2(halfTop, top),
            new Vec2(-halfTop, top)
        };
    }

    public double Pitch { get; }

    public double TipFilletRadius { get; }

    public double Distance(double s, double t)
    {
        var k = Math.Round(s / Pitch);
        var local = s - (k * Pitch);
        var d = Tooth(local, t);
        d = Math.Min(d, Tooth(local - Pitch, t));
        d = Math.Min(d, Tooth(local + Pitch, t));
        return d;
    }

    private double Tooth(double s, double t) => ConvexDistance(s, t) - TipFilletRadius;

    private double ConvexDistance(double px, double py)
    {
        var best = double.PositiveInfinity;
        var inside = true;

        for (var i = 0; i < _inset.Length; i++)
        {
            var a = _inset[i];
            var b = _inset[(i + 1) % _inset.Length];
            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var wx = px - a.X;
            var wy = py - a.Y;
            var t = Math.Clamp(((wx * ex) + (wy * ey)) / ((ex * ex) + (ey * ey)), 0.0, 1.0);
            var dx = wx - (ex * t);
            var dy = wy - (ey * t);
            best = Math.Min(best, (dx * dx) + (dy * dy));

            if ((ex * wy) - (ey * wx) < 0)
            {
                inside = false;
            }
        }

        var distance = Math.Sqrt(best);
        return inside ? -distance : distance;
    }
}
