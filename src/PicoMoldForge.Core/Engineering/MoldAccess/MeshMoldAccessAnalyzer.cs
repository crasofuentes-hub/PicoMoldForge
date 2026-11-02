using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.MoldAccess;

/// <summary>How the mold steel next to a face leaves it when the two halves open.</summary>
public enum MoldFaceAccess
{
    /// <summary>The steel moves straight along the opening direction of its half without meeting the part.</summary>
    Clear = 0,

    /// <summary>
    /// Trapped along the opening direction, but freed by first moving sideways (in the parting plane) and then
    /// opening: a side action or lifter.
    /// </summary>
    SideAction = 1,

    /// <summary>Trapped along the opening direction, with no sideways escape found.</summary>
    Trapped = 2,

    /// <summary>
    /// Degenerate (zero-area) triangle, or a triangle whose steel point lies inside the part: its normal points
    /// into the solid within the resolution (a folded sliver of the mesh, or a gap narrower than the resolution).
    /// </summary>
    Invalid = 3
}

/// <summary>
/// Settings of <see cref="MeshMoldAccessAnalyzer"/>. <paramref name="ResolutionMm"/>: the steel next to a face is
/// tested this far off the face, so obstacles closer than that (mesh noise, folded slivers, steps of a few
/// hundredths of a millimetre) are ignored; gaps and undercuts smaller than it are not resolved.
/// <paramref name="SideDirectionCount"/> evenly spaced sideways directions in the parting plane are tried,
/// together with the face normal projected on the plane.
/// </summary>
public sealed record MeshMoldAccessOptions(double ResolutionMm = 0.1, int SideDirectionCount = 16, int MaxDegreeOfParallelism = -1);

/// <summary>
/// One triangle of the part. <paramref name="Half"/> is +1 for the half on the positive side of the parting plane
/// (it opens along +axis), -1 for the negative side (it opens along -axis) and 0 for an invalid triangle.
/// <paramref name="PullDot"/> is the dot product of the outward normal with the opening direction of its half: the
/// draft angle is asin(PullDot). <paramref name="SideTravelMm"/> is the shortest sideways travel that frees a
/// <see cref="MoldFaceAccess.SideAction"/> face, 0 otherwise.
/// </summary>
public sealed record MoldAccessFace(
    int TriangleIndex,
    double AreaMm2,
    int Half,
    double PullDot,
    MoldFaceAccess Access,
    double SideTravelMm,
    MeshVertex Centroid);

public sealed record MeshMoldAccessResult(
    IReadOnlyList<MoldAccessFace> Faces,
    PartingAxis Axis,
    double PartingOffsetMm,
    double TotalAreaMm2,
    bool OrientationFlipped,
    double ResolutionMm);

/// <summary>
/// Draft and undercut of a closed surface mesh in a two-half mold split by the plane {axis = offset}, with no
/// voxels and no PicoGK runtime. Each triangle belongs to the half that holds its centroid and that half opens away
/// from the plane; a triangle whose centroid lies within the resolution r of the plane (a face lying in the plane,
/// up to mesh noise) belongs to the half its normal faces.
/// The steel next to a face is represented by the point p = centroid + r n, r = ResolutionMm off the face. The face
/// is clear when the ray from p along the opening direction of its half leaves the part (a face with negative
/// draft is hit by that ray when its overhang exceeds r). A trapped face is a side-action candidate when, for one
/// of the sideways directions u, p can move by some s along u without meeting the part and then open freely; its
/// side travel is the smallest such s. It is measured from p, so it can be up to r less than the travel of the face
/// itself, and it is a lower bound of the travel of a real slide (a point can escape through openings narrower than
/// a slide). A trapped face with no such escape is an undercut. Faces are computed independently and stored by
/// index, so the result does not depend on threads.
/// </summary>
public static class MeshMoldAccessAnalyzer
{
    /// <summary>A sideways direction may not move the steel into its own face beyond this dot product.</summary>
    public const double NegativeDotTolerance = 0.0001;

    private const int MaxGridCells = 2048;
    private const int MaxConfirmations = 8;

    public static MeshMoldAccessResult Analyze(
        IReadOnlyList<MeshTriangle> triangles,
        PartingAxis axis,
        double partingOffsetMm,
        MeshMoldAccessOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        options ??= new MeshMoldAccessOptions();

        if (!double.IsFinite(partingOffsetMm))
        {
            throw new ArgumentOutOfRangeException(nameof(partingOffsetMm), "Parting offset must be finite.");
        }

        if (!(options.ResolutionMm > 0) || !double.IsFinite(options.ResolutionMm))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ResolutionMm must be positive and finite.");
        }

        if (options.SideDirectionCount < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "SideDirectionCount must be at least 4.");
        }

        var (pull, sideA, sideB) = Frame(axis);

        if (triangles.Count == 0)
        {
            return new MeshMoldAccessResult(Array.Empty<MoldAccessFace>(), axis, partingOffsetMm, 0, false, options.ResolutionMm);
        }

        var signedVolume = 0.0;

        foreach (var t in triangles)
        {
            signedVolume += Vec3d.Dot(Vec3d.From(t.A), Vec3d.Cross(Vec3d.From(t.B), Vec3d.From(t.C))) / 6.0;
        }

        var flip = signedVolume < 0;
        var bvh = new TriangleBvh(triangles);
        var extent = bvh.Max - bvh.Min;
        var planeExtent = Math.Max(Math.Abs(Vec3d.Dot(extent, sideA)), Math.Abs(Vec3d.Dot(extent, sideB)));
        var cell = Math.Max(options.ResolutionMm / 2, planeExtent / MaxGridCells);
        var context = new Context(
            triangles,
            bvh,
            new ShadowMap(triangles, pull, sideA, sideB, cell),
            pull,
            sideA,
            sideB,
            SideDirections(sideA, sideB, options.SideDirectionCount),
            partingOffsetMm,
            flip ? -1.0 : 1.0,
            options.ResolutionMm);

        var faces = new MoldAccessFace[triangles.Count];
        var parallel = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
        };

        Parallel.For(0, triangles.Count, parallel, i => faces[i] = AnalyzeFace(context, i));

        return new MeshMoldAccessResult(faces, axis, partingOffsetMm, faces.Sum(face => face.AreaMm2), flip, options.ResolutionMm);
    }

    private static MoldAccessFace AnalyzeFace(Context context, int index)
    {
        var t = context.Triangles[index];
        var a = Vec3d.From(t.A);
        var b = Vec3d.From(t.B);
        var c = Vec3d.From(t.C);
        var cross = Vec3d.Cross(b - a, c - a);
        var length = cross.Length;
        var area = length / 2;
        var centroid = (a + b + c) * (1.0 / 3.0);
        var centroidVertex = new MeshVertex(centroid.X, centroid.Y, centroid.Z);

        if (!(area > 1e-12) || !double.IsFinite(length))
        {
            return new MoldAccessFace(index, double.IsFinite(area) ? area : 0, 0, 0, MoldFaceAccess.Invalid, 0, centroidVertex);
        }

        var normal = cross * (context.NormalSign / length);
        var height = Vec3d.Dot(centroid, context.Pull) - context.PartingOffsetMm;
        var half = Math.Abs(height) > context.ResolutionMm
            ? Math.Sign(height)
            : Vec3d.Dot(normal, context.Pull) >= 0 ? 1 : -1;
        var opening = context.Pull * half;
        var pullDot = Math.Clamp(Vec3d.Dot(normal, opening), -1.0, 1.0);
        var steel = centroid + (normal * context.ResolutionMm);

        if (context.Bvh.RayHit(steel, opening, -1).Triangle < 0)
        {
            return new MoldAccessFace(index, area, half, pullDot, MoldFaceAccess.Clear, 0, centroidVertex);
        }

        // On a closed surface the first face met from outside faces the ray. Seen along the normal, away from the
        // face itself, a face met from behind means the steel point is inside the part.
        var (_, beyond) = context.Bvh.RayHit(steel, normal, index);

        if (beyond >= 0 && Vec3d.Dot(OutwardNormal(context, beyond), normal) > 0)
        {
            return new MoldAccessFace(index, area, half, pullDot, MoldFaceAccess.Invalid, 0, centroidVertex);
        }

        var travel = SideTravel(context, steel, normal, opening, half);

        return double.IsFinite(travel)
            ? new MoldAccessFace(index, area, half, pullDot, MoldFaceAccess.SideAction, travel, centroidVertex)
            : new MoldAccessFace(index, area, half, pullDot, MoldFaceAccess.Trapped, 0, centroidVertex);
    }

    /// <summary>
    /// Smallest s over the candidate directions u: the first position along u at which the column of the shadow
    /// map holds nothing beyond the steel point (confirmed by a ray), reached before the sideways path meets the
    /// part. +infinity when no direction frees the face.
    /// </summary>
    private static double SideTravel(Context context, Vec3d steel, Vec3d normal, Vec3d opening, int half)
    {
        var best = double.PositiveInfinity;
        var projectedNormal = normal - (context.Pull * Vec3d.Dot(normal, context.Pull));
        var projectedLength = projectedNormal.Length;
        var candidates = context.SideDirections.Length + (projectedLength > 1e-6 ? 1 : 0);
        var x = Vec3d.Dot(steel, context.SideA);
        var y = Vec3d.Dot(steel, context.SideB);
        var level = half * Vec3d.Dot(steel, context.Pull);

        for (var k = 0; k < candidates; k++)
        {
            // The direction away from the face comes first: it is the usual slide direction.
            var u = projectedLength > 1e-6
                ? k == 0 ? projectedNormal * (1.0 / projectedLength) : context.SideDirections[k - 1]
                : context.SideDirections[k];

            if (Vec3d.Dot(normal, u) < -NegativeDotTolerance)
            {
                continue;
            }

            var limit = Math.Min(context.Bvh.RayHit(steel, u, -1).Distance, best);
            var ux = Vec3d.Dot(u, context.SideA);
            var uy = Vec3d.Dot(u, context.SideB);
            var s = 0.0;

            for (var attempt = 0; attempt < MaxConfirmations && s < limit; attempt++)
            {
                var free = context.Map.FirstFree(x + (ux * s), y + (uy * s), ux, uy, half, level, limit - s);

                if (!double.IsFinite(free))
                {
                    break;
                }

                s += free;
                var probe = steel + (u * (s + (context.Map.Cell * 0.01)));

                if (s < limit && context.Bvh.RayHit(probe, opening, -1).Triangle < 0)
                {
                    best = s;
                    break;
                }

                s += context.Map.Cell / 2;
            }
        }

        return best;
    }

    private static Vec3d OutwardNormal(Context context, int index)
    {
        var t = context.Triangles[index];
        var a = Vec3d.From(t.A);
        return Vec3d.Cross(Vec3d.From(t.B) - a, Vec3d.From(t.C) - a) * context.NormalSign;
    }

    private static (Vec3d Pull, Vec3d SideA, Vec3d SideB) Frame(PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => (new Vec3d(1, 0, 0), new Vec3d(0, 1, 0), new Vec3d(0, 0, 1)),
            PartingAxis.Y => (new Vec3d(0, 1, 0), new Vec3d(0, 0, 1), new Vec3d(1, 0, 0)),
            PartingAxis.Z => (new Vec3d(0, 0, 1), new Vec3d(1, 0, 0), new Vec3d(0, 1, 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static Vec3d[] SideDirections(Vec3d sideA, Vec3d sideB, int count)
    {
        var directions = new Vec3d[count];

        for (var k = 0; k < count; k++)
        {
            var angle = 2 * Math.PI * k / count;
            var cos = Snap(Math.Cos(angle));
            var sin = Snap(Math.Sin(angle));
            directions[k] = (sideA * cos) + (sideB * sin);
        }

        return directions;
    }

    private static double Snap(double value) => Math.Abs(value) < 1e-12 ? 0 : value;

    private sealed record Context(
        IReadOnlyList<MeshTriangle> Triangles,
        TriangleBvh Bvh,
        ShadowMap Map,
        Vec3d Pull,
        Vec3d SideA,
        Vec3d SideB,
        Vec3d[] SideDirections,
        double PartingOffsetMm,
        double NormalSign,
        double ResolutionMm);
}
