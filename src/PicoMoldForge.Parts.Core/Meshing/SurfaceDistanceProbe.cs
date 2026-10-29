using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Meshing;

/// <summary>
/// Measured distance from mesh samples to the ideal surface (millimeters). Every value is an upper bound:
/// a point of the true surface was located at that distance. Samples are vertices plus, per sampled
/// triangle, its centroid and the midpoints of its three edges (where flat triangles sag the most).
/// Maxima are taken over resolved samples; <see cref="Unresolved"/> counts samples with no surface point
/// found within <see cref="SearchLimitMm"/>.
/// </summary>
public sealed record SurfaceDistanceReport(
    int Vertices,
    int VertexSamples,
    double VertexMaxMm,
    int Triangles,
    int TriangleSamples,
    double TriangleMaxMm,
    int Unresolved,
    double SearchLimitMm)
{
    public double MaxMm => Math.Max(VertexMaxMm, TriangleMaxMm);
}

/// <summary>
/// Geometric distance to the surface without trusting the field value: from a sample point the probe walks
/// along candidate directions (the field gradient, which points to the nearest surface for exact fields, the
/// mesh normal and the six axis directions) until the exact sign of the field changes, then bisects. A sign
/// change proves a surface point between the two probes, so every result is an upper bound of the true
/// distance; the smallest one over the candidate directions is kept.
/// </summary>
public static class SurfaceDistanceProbe
{
    private const int BisectionSteps = 26;

    public static SurfaceDistanceReport Measure(TriangleMesh mesh, ISdf sdf, double searchLimitMm, int maxSamples = 12_000, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(sdf);

        if (!(searchLimitMm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(searchLimitMm), "Search limit must be positive.");
        }

        if (maxSamples < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSamples), "At least one sample is required.");
        }

        var vertices = mesh.Vertices;
        var indices = mesh.Indices;
        var normals = new Vec3[vertices.Length];

        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var a = indices[3 * t];
            var b = indices[(3 * t) + 1];
            var c = indices[(3 * t) + 2];
            var areaNormal = Vec3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            normals[a] += areaNormal;
            normals[b] += areaNormal;
            normals[c] += areaNormal;
        }

        var vertexStride = Math.Max(1, (vertices.Length + maxSamples - 1) / maxSamples);
        var triangleStride = Math.Max(1, (mesh.TriangleCount + maxSamples - 1) / maxSamples);
        var vertexSamples = (vertices.Length + vertexStride - 1) / vertexStride;
        var triangleSamples = (mesh.TriangleCount + triangleStride - 1) / triangleStride;
        var vertexResults = new double[vertexSamples];
        var triangleResults = new double[triangleSamples];
        var options = new ParallelOptions { CancellationToken = cancellationToken };

        Parallel.For(0, vertexSamples, options, s =>
        {
            var v = s * vertexStride;
            vertexResults[s] = UpperBound(sdf, vertices[v], normals[v], searchLimitMm);
        });

        Parallel.For(0, triangleSamples, options, s =>
        {
            var t = s * triangleStride;
            var a = vertices[indices[3 * t]];
            var b = vertices[indices[(3 * t) + 1]];
            var c = vertices[indices[(3 * t) + 2]];
            var normal = Vec3.Cross(b - a, c - a);
            var worst = UpperBound(sdf, (a + b + c) * (1.0 / 3.0), normal, searchLimitMm);
            worst = Math.Max(worst, UpperBound(sdf, (a + b) * 0.5, normal, searchLimitMm));
            worst = Math.Max(worst, UpperBound(sdf, (b + c) * 0.5, normal, searchLimitMm));
            worst = Math.Max(worst, UpperBound(sdf, (c + a) * 0.5, normal, searchLimitMm));
            triangleResults[s] = worst;
        });

        return new SurfaceDistanceReport(
            vertices.Length,
            vertexSamples,
            MaxFinite(vertexResults),
            mesh.TriangleCount,
            triangleSamples,
            MaxFinite(triangleResults),
            vertexResults.Count(double.IsPositiveInfinity) + triangleResults.Count(double.IsPositiveInfinity),
            searchLimitMm);
    }

    /// <summary>
    /// Upper bound of the distance from <paramref name="point"/> to the surface, searching along the field
    /// gradient and along <paramref name="normal"/> (outward mesh normal, any length) up to
    /// <paramref name="searchLimitMm"/>. Returns +infinity when no surface point was found.
    /// </summary>
    public static double UpperBound(ISdf sdf, Vec3 point, Vec3 normal, double searchLimitMm)
    {
        ArgumentNullException.ThrowIfNull(sdf);

        var value = sdf.Distance(point);

        if (value == 0)
        {
            return 0;
        }

        var inside = value < 0;
        var start = Math.Clamp(Math.Abs(value), searchLimitMm * 1e-4, searchLimitMm);
        var best = double.PositiveInfinity;

        // Toward the surface: up the field when inside, down the field when outside.
        var step = searchLimitMm * 1e-3;
        var gradient = new Vec3(
            sdf.Distance(point + new Vec3(step, 0, 0)) - sdf.Distance(point - new Vec3(step, 0, 0)),
            sdf.Distance(point + new Vec3(0, step, 0)) - sdf.Distance(point - new Vec3(0, step, 0)),
            sdf.Distance(point + new Vec3(0, 0, step)) - sdf.Distance(point - new Vec3(0, 0, step)));

        if (TryDirection(gradient, out var up))
        {
            best = Math.Min(best, Search(sdf, point, inside ? up : -up, inside, start, Math.Min(best, searchLimitMm)));
        }

        if (TryDirection(normal, out var outward))
        {
            best = Math.Min(best, Search(sdf, point, inside ? outward : -outward, inside, start, Math.Min(best, searchLimitMm)));

            if (double.IsPositiveInfinity(best))
            {
                best = Search(sdf, point, inside ? -outward : outward, inside, start, searchLimitMm);
            }
        }

        // Where several faces tie (corners, symmetric kinks) gradient and normal point along a diagonal;
        // the axis directions find the nearer face. Each search stops as soon as it cannot beat the best.
        foreach (var axis in AxisDirections)
        {
            best = Math.Min(best, Search(sdf, point, axis, inside, start, Math.Min(best, searchLimitMm)));
        }

        // Rare grazing cases (a point a hair above a face, next to an edge): every ray above runs parallel to
        // the face. A fixed set of directions over the sphere finds the edge instead.
        if (double.IsPositiveInfinity(best))
        {
            foreach (var direction in SphereDirections)
            {
                best = Math.Min(best, Search(sdf, point, direction, inside, start, Math.Min(best, searchLimitMm)));
            }
        }

        return best;
    }

    private static readonly Vec3[] AxisDirections =
    {
        Vec3.UnitX, -Vec3.UnitX, Vec3.UnitY, -Vec3.UnitY, Vec3.UnitZ, -Vec3.UnitZ
    };

    /// <summary>64 directions on a Fibonacci sphere (deterministic).</summary>
    private static readonly Vec3[] SphereDirections = Enumerable.Range(0, 64).Select(i =>
    {
        var z = 1 - ((2.0 * i) + 1) / 64;
        var radius = Math.Sqrt(1 - (z * z));
        var angle = i * Math.PI * (3 - Math.Sqrt(5));
        return new Vec3(radius * Math.Cos(angle), radius * Math.Sin(angle), z);
    }).ToArray();

    private static bool TryDirection(Vec3 vector, out Vec3 unit)
    {
        var length = vector.Length;

        if (length > 0 && double.IsFinite(length))
        {
            unit = vector * (1.0 / length);
            return true;
        }

        unit = Vec3.Zero;
        return false;
    }

    private static double Search(ISdf sdf, Vec3 point, Vec3 direction, bool inside, double start, double limit)
    {
        var low = 0.0;
        var high = Math.Min(start, limit);

        while (!Crossed(sdf.Distance(point + (direction * high)), inside))
        {
            if (high >= limit)
            {
                return double.PositiveInfinity;
            }

            low = high;
            high = Math.Min(high * 2, limit);
        }

        for (var i = 0; i < BisectionSteps; i++)
        {
            var middle = (low + high) / 2;

            if (Crossed(sdf.Distance(point + (direction * middle)), inside))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return high;
    }

    private static bool Crossed(double value, bool startedInside) => startedInside ? value >= 0 : value <= 0;

    private static double MaxFinite(double[] values)
    {
        var max = 0.0;

        foreach (var value in values)
        {
            if (double.IsFinite(value) && value > max)
            {
                max = value;
            }
        }

        return max;
    }
}
