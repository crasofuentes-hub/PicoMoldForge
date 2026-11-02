namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>
/// Settings of <see cref="MeshWallThicknessSampler"/>.
/// <paramref name="OpposingFaceDotLimit"/>: a measurement counts as a wall when the face that stops the inscribed
/// ball has a normal n_stop with n_stop . n &lt; this limit (n = normal at the sample; -0.5 means the two faces
/// are within 60 degrees of facing each other).
/// </summary>
public sealed record MeshWallThicknessOptions(double OpposingFaceDotLimit = -0.5, int MaxDegreeOfParallelism = -1);

/// <summary>
/// Wall thickness at one triangle. <paramref name="IsWall"/> is true when an opposing face limits the inscribed
/// ball (a real wall); false for edges, rims and end faces, where the ball is stopped by a neighbouring face, and
/// for degenerate triangles. <paramref name="ThicknessMm"/> is the ball diameter (0 when unknown).
/// </summary>
public sealed record WallThicknessMeasurement(int TriangleIndex, double ThicknessMm, double AreaMm2, bool IsWall);

public sealed record MeshWallThicknessResult(
    IReadOnlyList<WallThicknessMeasurement> Measurements,
    int TriangleCount,
    int WallSampleCount,
    double WallAreaMm2,
    double TotalAreaMm2,
    bool OrientationFlipped);

/// <summary>
/// Wall thickness measured on a closed surface mesh, with no voxels and no PicoGK runtime.
/// At each triangle centroid p with outward normal n, the thickness is the diameter of the largest ball that
/// touches p and stays inside the part (centre p - r n, no surface closer than r): the inscribed-ball or
/// "rolling ball" definition. Balls tangent at p are nested, so the radius is found by bisection, bounded by
/// half the distance to the first surface along -n. The face that stops the ball tells walls from edges.
/// Results are deterministic: each triangle is computed independently and stored by index.
/// </summary>
public static class MeshWallThicknessSampler
{
    private const int BisectionSteps = 22;

    public static MeshWallThicknessResult Sample(
        IReadOnlyList<MeshTriangle> triangles,
        MeshWallThicknessOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        options ??= new MeshWallThicknessOptions();

        if (!(options.OpposingFaceDotLimit >= -1 && options.OpposingFaceDotLimit <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "OpposingFaceDotLimit must be between -1 and 0.");
        }

        if (triangles.Count == 0)
        {
            return new MeshWallThicknessResult(Array.Empty<WallThicknessMeasurement>(), 0, 0, 0, 0, false);
        }

        // A mesh written inside out has a negative signed volume; flip its normals instead of measuring outside.
        var signedVolume = 0.0;

        foreach (var t in triangles)
        {
            signedVolume += Vec3d.Dot(Vec3d.From(t.A), Vec3d.Cross(Vec3d.From(t.B), Vec3d.From(t.C))) / 6.0;
        }

        var flip = signedVolume < 0;
        var bvh = new TriangleBvh(triangles);
        var diagonal = (bvh.Max - bvh.Min).Length;
        var unitNormals = new Vec3d[triangles.Count];
        var areas = new double[triangles.Count];

        for (var i = 0; i < triangles.Count; i++)
        {
            var t = triangles[i];
            var cross = Vec3d.Cross(Vec3d.From(t.B) - Vec3d.From(t.A), Vec3d.From(t.C) - Vec3d.From(t.A));
            var length = cross.Length;
            areas[i] = length / 2;
            unitNormals[i] = length > 0 ? cross * ((flip ? -1.0 : 1.0) / length) : default;
        }

        var measurements = new WallThicknessMeasurement[triangles.Count];
        var parallel = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
        };

        Parallel.For(0, triangles.Count, parallel, i =>
        {
            measurements[i] = Measure(bvh, triangles[i], unitNormals, areas, i, diagonal, options.OpposingFaceDotLimit);
        });

        var walls = measurements.Where(m => m.IsWall).ToArray();

        return new MeshWallThicknessResult(
            measurements,
            triangles.Count,
            walls.Length,
            walls.Sum(m => m.AreaMm2),
            areas.Sum(),
            flip);
    }

    private static WallThicknessMeasurement Measure(
        TriangleBvh bvh,
        MeshTriangle triangle,
        Vec3d[] unitNormals,
        double[] areas,
        int index,
        double diagonal,
        double opposingFaceDotLimit)
    {
        var area = areas[index];

        // Degenerate (zero-area) triangles have no normal.
        if (!(area > 1e-12))
        {
            return new WallThicknessMeasurement(index, 0, area, false);
        }

        var n = unitNormals[index];
        var p = (Vec3d.From(triangle.A) + Vec3d.From(triangle.B) + Vec3d.From(triangle.C)) * (1.0 / 3.0);
        var scale = Math.Max(1e-9, diagonal * 1e-9);

        // Along -n the ball cannot grow past the first surface: half that distance bounds the radius. No surface
        // along -n means the mesh is open there; no wall can be measured.
        var ray = bvh.Ray(p - (n * scale), -n);

        if (double.IsPositiveInfinity(ray))
        {
            return new WallThicknessMeasurement(index, 0, area, false);
        }

        var low = 0.0;
        var high = (ray / 2) + scale;

        for (var step = 0; step < BisectionSteps; step++)
        {
            var radius = (low + high) / 2;

            // The ball touches p (distance exactly r to its own triangle); a nearer surface ends the growth.
            if (bvh.AnyCloserThan(p - (n * radius), (radius * (1 - 1e-6)) - scale))
            {
                high = radius;
            }
            else
            {
                low = radius;
            }
        }

        // The face nearest to the centre of a slightly larger ball is the one that stopped it.
        var (_, stopper) = bvh.Nearest(p - (n * ((high * 1.02) + scale)));
        var isWall = stopper >= 0 && Vec3d.Dot(unitNormals[stopper], n) < opposingFaceDotLimit;

        return new WallThicknessMeasurement(index, 2 * low, area, isWall);
    }
}
