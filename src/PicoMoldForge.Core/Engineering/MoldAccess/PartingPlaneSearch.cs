using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.MoldAccess;

/// <summary>
/// Settings of <see cref="PartingPlaneSearch"/>. <paramref name="ResolutionMm"/> and the trapped-face test are those
/// of <see cref="MeshMoldAccessAnalyzer"/>; <paramref name="MinimumDraftDeg"/> separates drafted faces from the rest;
/// <paramref name="UniformLevels"/> evenly spaced offsets are tried on each axis besides the flat-face levels.
/// </summary>
public sealed record PartingPlaneSearchOptions(
    double ResolutionMm = 0.1,
    double MinimumDraftDeg = 1.0,
    int UniformLevels = 32,
    int MaxDegreeOfParallelism = -1);

/// <summary>
/// One flat split. <paramref name="TrappedAreaMm2"/> is the area that does not release along the opening direction
/// of its half (side-action candidates, undercuts and invalid triangles, as in the undercut summary);
/// <paramref name="UndraftedAreaMm2"/> the area with less than the minimum draft against its own half (and invalid
/// triangles, as in the draft summary); <paramref name="FlatAreaInPlaneMm2"/> the area of faces lying in the plane.
/// </summary>
public sealed record PartingPlaneCandidateResult(
    PartingAxis Axis,
    double OffsetMm,
    string Source,
    double TrappedAreaMm2,
    double UndraftedAreaMm2,
    double FlatAreaInPlaneMm2);

public sealed record PartingPlaneSearchResult(
    PartingPlaneCandidateResult Best,
    IReadOnlyList<PartingPlaneCandidateResult> BestPerAxis,
    PartingPlaneCandidateResult? Reference,
    double TotalAreaMm2,
    double ToleranceMm2);

/// <summary>
/// Chooses a flat two-half split from the part mesh. For each axis X, Y and Z every triangle is tested once for each
/// half (the steel point r off the face, a ray along +axis and one along -axis), so any offset can then be scored by
/// sums. Offsets tried: the levels of faces lying across the axis, evenly spaced levels, the bounding-box centre and
/// the part ends. On each axis the best offset has the least trapped area, then the most flat area lying in the plane
/// (a parting surface on a face instead of across walls), then the least undrafted area, then the offset nearest the
/// centre. Across axes the least trapped area decides, then the least undrafted area, then the flat area. Areas
/// within max(1 mm2, 0.1 % of the surface) of the best count as equal, undrafted areas within max(1 mm2, 1 %): the
/// tilts of a tessellated wall change the undrafted area by a few tenths of a percent.
/// </summary>
public static class PartingPlaneSearch
{
    private const double FlatDot = 0.9999;
    private const int MaxFlatLevels = 16;

    public static PartingPlaneSearchResult Search(
        IReadOnlyList<MeshTriangle> triangles,
        PartingPlaneSearchOptions? options = null,
        (PartingAxis Axis, double OffsetMm)? reference = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        options ??= new PartingPlaneSearchOptions();

        if (triangles.Count == 0)
        {
            throw new ArgumentException("At least one triangle is required.", nameof(triangles));
        }

        if (!(options.ResolutionMm > 0) || options.UniformLevels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ResolutionMm must be positive and UniformLevels at least 1.");
        }

        var faces = Faces.Build(triangles, options, cancellationToken);
        var tolerance = Math.Max(1.0, faces.TotalArea * 0.001);
        var draftTolerance = Math.Max(1.0, faces.TotalArea * 0.01);
        var sinMinimumDraft = Math.Sin(options.MinimumDraftDeg * Math.PI / 180);
        var bestPerAxis = new List<PartingPlaneCandidateResult>();
        PartingPlaneCandidateResult? referenceResult = null;

        foreach (var axis in new[] { PartingAxis.X, PartingAxis.Y, PartingAxis.Z })
        {
            var scan = faces.Scan(axis, cancellationToken);
            var results = Offsets(faces, scan, options)
                .Select(c => scan.Evaluate(axis, c.Offset, c.Source, sinMinimumDraft))
                .ToList();

            // Same axis: the undrafted area hardly depends on the offset (only through tessellation tilts), so a
            // flat parting surface decides before it.
            var kept = Band(results, c => c.TrappedAreaMm2, tolerance, lowest: true);
            kept = Band(kept, c => c.FlatAreaInPlaneMm2, tolerance, lowest: false);
            kept = Band(kept, c => c.UndraftedAreaMm2, draftTolerance, lowest: true);
            bestPerAxis.Add(kept.OrderBy(c => Math.Abs(c.OffsetMm - scan.Center)).ThenBy(c => c.OffsetMm).First());

            if (reference is { } r && r.Axis == axis)
            {
                referenceResult = scan.Evaluate(axis, r.OffsetMm, "Reference", sinMinimumDraft);
            }
        }

        // Across axes the undrafted area matters: it is set by the walls parallel to the axis.
        var overall = Band(bestPerAxis, c => c.TrappedAreaMm2, tolerance, lowest: true);
        overall = Band(overall, c => c.UndraftedAreaMm2, draftTolerance, lowest: true);
        overall = Band(overall, c => c.FlatAreaInPlaneMm2, tolerance, lowest: false);

        return new PartingPlaneSearchResult(overall[0], bestPerAxis, referenceResult, faces.TotalArea, tolerance);
    }

    /// <summary>The candidates within <paramref name="tolerance"/> of the best value, in their original order.</summary>
    private static List<PartingPlaneCandidateResult> Band(
        IReadOnlyList<PartingPlaneCandidateResult> candidates,
        Func<PartingPlaneCandidateResult, double> value,
        double tolerance,
        bool lowest)
    {
        var best = lowest ? candidates.Min(value) : candidates.Max(value);
        return candidates
            .Where(c => lowest ? value(c) <= best + tolerance : value(c) >= best - tolerance)
            .ToList();
    }

    private static IEnumerable<(double Offset, string Source)> Offsets(Faces faces, AxisScan scan, PartingPlaneSearchOptions options)
    {
        var list = new List<(double Offset, string Source)>();
        var r = options.ResolutionMm;

        // Levels of faces lying across the axis, merged within r, largest first.
        var flat = Enumerable.Range(0, faces.Count)
            .Where(i => !faces.Invalid[i] && Math.Abs(scan.AxisDot[i]) >= FlatDot)
            .OrderBy(i => scan.Height[i])
            .ToArray();
        var levels = new List<(double Height, double Area)>();
        var k = 0;

        while (k < flat.Length)
        {
            var start = scan.Height[flat[k]];
            double area = 0, weighted = 0;

            while (k < flat.Length && scan.Height[flat[k]] - start <= r)
            {
                area += faces.Area[flat[k]];
                weighted += faces.Area[flat[k]] * scan.Height[flat[k]];
                k++;
            }

            levels.Add((weighted / area, area));
        }

        list.AddRange(levels.OrderByDescending(l => l.Area).Take(MaxFlatLevels).Select(l => (l.Height, "Flat face level")));
        list.Add((scan.Min, "Part end"));
        list.Add((scan.Max, "Part end"));
        list.Add((scan.Center, "Bounding-box centre"));

        for (var i = 1; i <= options.UniformLevels; i++)
        {
            list.Add((scan.Min + ((scan.Max - scan.Min) * i / (options.UniformLevels + 1)), "Even level"));
        }

        // Drop near duplicates (within r / 2), keeping the first source; deterministic order by offset.
        var kept = new List<(double Offset, string Source)>();

        foreach (var candidate in list)
        {
            if (kept.All(c => Math.Abs(c.Offset - candidate.Offset) > r / 2))
            {
                kept.Add(candidate);
            }
        }

        return kept.OrderBy(c => c.Offset);
    }

    /// <summary>Per-triangle data shared by the three axes.</summary>
    private sealed class Faces
    {
        private readonly IReadOnlyList<MeshTriangle> _triangles;
        private readonly TriangleBvh _bvh;
        private readonly double _resolution;
        private readonly int _parallelism;
        private readonly Vec3d[] _centroid;
        private readonly Vec3d[] _normal;

        private Faces(IReadOnlyList<MeshTriangle> triangles, PartingPlaneSearchOptions options)
        {
            _triangles = triangles;
            _resolution = options.ResolutionMm;
            _parallelism = options.MaxDegreeOfParallelism;
            Count = triangles.Count;
            Area = new double[Count];
            Invalid = new bool[Count];
            _centroid = new Vec3d[Count];
            _normal = new Vec3d[Count];
            _bvh = new TriangleBvh(triangles);
        }

        public int Count { get; }

        public double[] Area { get; }

        public bool[] Invalid { get; }

        public double TotalArea { get; private set; }

        public double CenterAlong(PartingAxis axis) => (Axis(_bvh.Min, axis) + Axis(_bvh.Max, axis)) / 2;

        public static Faces Build(IReadOnlyList<MeshTriangle> triangles, PartingPlaneSearchOptions options, CancellationToken cancellationToken)
        {
            var faces = new Faces(triangles, options);
            var signedVolume = 0.0;

            foreach (var t in triangles)
            {
                signedVolume += Vec3d.Dot(Vec3d.From(t.A), Vec3d.Cross(Vec3d.From(t.B), Vec3d.From(t.C))) / 6.0;
            }

            var sign = signedVolume < 0 ? -1.0 : 1.0;

            for (var i = 0; i < faces.Count; i++)
            {
                var t = triangles[i];
                var a = Vec3d.From(t.A);
                var cross = Vec3d.Cross(Vec3d.From(t.B) - a, Vec3d.From(t.C) - a);
                var length = cross.Length;
                faces.Area[i] = double.IsFinite(length) ? length / 2 : 0;
                faces._centroid[i] = (a + Vec3d.From(t.B) + Vec3d.From(t.C)) * (1.0 / 3.0);
                faces.Invalid[i] = !(faces.Area[i] > 1e-12);
                faces._normal[i] = faces.Invalid[i] ? default : cross * (sign / length);
            }

            // A steel point inside the part (normal pointing into the solid) makes the triangle invalid on every axis.
            var parallel = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };
            Parallel.For(0, faces.Count, parallel, i =>
            {
                if (faces.Invalid[i])
                {
                    return;
                }

                var steel = faces._centroid[i] + (faces._normal[i] * faces._resolution);
                var (_, beyond) = faces._bvh.RayHit(steel, faces._normal[i], i);

                if (beyond >= 0 && Vec3d.Dot(faces.OutwardNormal(beyond, sign), faces._normal[i]) > 0)
                {
                    faces.Invalid[i] = true;
                }
            });

            faces.TotalArea = faces.Area.Sum();
            return faces;
        }

        public AxisScan Scan(PartingAxis axis, CancellationToken cancellationToken)
        {
            var pull = axis switch
            {
                PartingAxis.X => new Vec3d(1, 0, 0),
                PartingAxis.Y => new Vec3d(0, 1, 0),
                _ => new Vec3d(0, 0, 1)
            };
            var scan = new AxisScan(Count, Area, Invalid, _resolution, Axis(_bvh.Min, axis), Axis(_bvh.Max, axis));
            var parallel = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = _parallelism };

            Parallel.For(0, Count, parallel, i =>
            {
                scan.Height[i] = Vec3d.Dot(_centroid[i], pull);

                if (Invalid[i])
                {
                    return;
                }

                scan.AxisDot[i] = Vec3d.Dot(_normal[i], pull);
                var steel = _centroid[i] + (_normal[i] * _resolution);
                scan.ClearPositive[i] = _bvh.RayHit(steel, pull, -1).Triangle < 0;
                scan.ClearNegative[i] = _bvh.RayHit(steel, -pull, -1).Triangle < 0;
            });

            return scan;
        }

        private Vec3d OutwardNormal(int index, double sign)
        {
            var t = _triangles[index];
            var a = Vec3d.From(t.A);
            return Vec3d.Cross(Vec3d.From(t.B) - a, Vec3d.From(t.C) - a) * sign;
        }

        private static double Axis(Vec3d v, PartingAxis axis) => axis switch
        {
            PartingAxis.X => v.X,
            PartingAxis.Y => v.Y,
            _ => v.Z
        };
    }

    /// <summary>Per-triangle results for one axis: height of the centroid, normal . axis, clear for each half.</summary>
    private sealed class AxisScan
    {
        private readonly double[] _area;
        private readonly bool[] _invalid;
        private readonly double _resolution;

        public AxisScan(int count, double[] area, bool[] invalid, double resolution, double min, double max)
        {
            _area = area;
            _invalid = invalid;
            _resolution = resolution;
            Min = min;
            Max = max;
            Height = new double[count];
            AxisDot = new double[count];
            ClearPositive = new bool[count];
            ClearNegative = new bool[count];
        }

        public double Min { get; }

        public double Max { get; }

        public double Center => (Min + Max) / 2;

        public double[] Height { get; }

        public double[] AxisDot { get; }

        public bool[] ClearPositive { get; }

        public bool[] ClearNegative { get; }

        /// <summary>Same half rule as <see cref="MeshMoldAccessAnalyzer"/>: centroid side, or the normal within r.</summary>
        public PartingPlaneCandidateResult Evaluate(PartingAxis axis, double offset, string source, double sinMinimumDraft)
        {
            double trapped = 0, undrafted = 0, flatInPlane = 0;

            for (var i = 0; i < Height.Length; i++)
            {
                if (_invalid[i])
                {
                    trapped += _area[i];
                    undrafted += _area[i];
                    continue;
                }

                var height = Height[i] - offset;
                var half = Math.Abs(height) > _resolution ? Math.Sign(height) : AxisDot[i] >= 0 ? 1 : -1;
                var clear = half > 0 ? ClearPositive[i] : ClearNegative[i];

                if (!clear)
                {
                    trapped += _area[i];
                }

                if (AxisDot[i] * half < sinMinimumDraft)
                {
                    undrafted += _area[i];
                }

                if (Math.Abs(height) <= _resolution && Math.Abs(AxisDot[i]) >= FlatDot)
                {
                    flatInPlane += _area[i];
                }
            }

            return new PartingPlaneCandidateResult(axis, offset, source, trapped, undrafted, flatInPlane);
        }
    }
}
