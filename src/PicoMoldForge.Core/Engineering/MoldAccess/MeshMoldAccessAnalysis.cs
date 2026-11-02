using System.Globalization;
using PicoMoldForge.Core.Engineering.DraftAnalysis;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.Undercuts;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.MoldAccess;

public sealed record MeshMoldAccessAnalysisResult(
    DraftBasicGeometryAnalysisResult Draft,
    UndercutRiskAnalysisSummary Undercut,
    MeshMoldAccessResult Access,
    IReadOnlyList<string> Notes);

/// <summary>
/// Draft and undercut summaries of a part in a two-half mold, measured on its surface mesh by
/// <see cref="MeshMoldAccessAnalyzer"/>. The draft of each triangle is taken against the opening direction of its
/// own half and classified by <see cref="DraftBasicGeometryAnalyzer"/>. The undercut summary counts triangles by
/// access: clear, side-action candidate (trap depth = side travel) and undercut (no sideways escape); low draft is
/// reported by the draft summary, so no triangle is counted as low pull clearance. Both summaries carry the sampled
/// area, so the readiness score uses areas, which do not depend on the tessellation.
/// </summary>
public static class MeshMoldAccessAnalysis
{
    /// <summary>sin(1 deg): negative draft steeper than this is reported apart from tessellation-level tilts.</summary>
    private static readonly double SteepNegativeDot = Math.Sin(Math.PI / 180);

    public static MeshMoldAccessAnalysisResult Analyze(
        IReadOnlyList<MeshTriangle> triangles,
        PartingAxis axis,
        decimal partingOffsetMm,
        decimal minimumDraftDeg,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        if (minimumDraftDeg < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDraftDeg), "Minimum draft cannot be negative.");
        }

        var access = MeshMoldAccessAnalyzer.Analyze(
            triangles,
            axis,
            (double)partingOffsetMm,
            cancellationToken: cancellationToken);

        var draft = AnalyzeDraft(triangles, access, minimumDraftDeg);
        var undercut = SummarizeUndercut(access);

        return new MeshMoldAccessAnalysisResult(
            draft,
            undercut,
            access,
            Notes(access, draft, minimumDraftDeg));
    }

    private static DraftBasicGeometryAnalysisResult AnalyzeDraft(
        IReadOnlyList<MeshTriangle> triangles,
        MeshMoldAccessResult access,
        decimal minimumDraftDeg)
    {
        var pull = Pull(access.Axis);
        var sign = access.OrientationFlipped ? -1.0 : 1.0;
        var samples = new DraftFaceSample[triangles.Count];

        for (var i = 0; i < triangles.Count; i++)
        {
            var face = access.Faces[i];
            var area = (decimal)face.AreaMm2;

            // Degenerate triangles and folded slivers have no usable normal for the draft either.
            if (face.Access == MoldFaceAccess.Invalid)
            {
                samples[i] = new DraftFaceSample($"triangle-{i}", 0m, 0m, 0m, area);
                continue;
            }

            // Against the fixed pull +axis, the normal times the side of its half gives the draft of its own half.
            var t = triangles[i];
            var a = Vec3d.From(t.A);
            var cross = Vec3d.Cross(Vec3d.From(t.B) - a, Vec3d.From(t.C) - a);
            var normal = cross * (sign * face.Half / cross.Length);

            samples[i] = new DraftFaceSample(
                $"triangle-{i}",
                (decimal)normal.X,
                (decimal)normal.Y,
                (decimal)normal.Z,
                area);
        }

        return new DraftBasicGeometryAnalyzer().Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: (decimal)pull.X,
            PullDirectionY: (decimal)pull.Y,
            PullDirectionZ: (decimal)pull.Z,
            MinimumRequiredDraftDeg: minimumDraftDeg,
            Faces: samples));
    }

    private static UndercutRiskAnalysisSummary SummarizeUndercut(MeshMoldAccessResult access)
    {
        var faces = access.Faces;
        var sideAction = faces.Where(face => face.Access == MoldFaceAccess.SideAction).ToArray();

        return new UndercutRiskAnalysisSummary(
            FaceCount: faces.Count,
            ClearPullCount: faces.Count(face => face.Access == MoldFaceAccess.Clear),
            LowPullClearanceCount: 0,
            SideActionCandidateCount: sideAction.Length,
            UndercutCount: faces.Count(face => face.Access == MoldFaceAccess.Trapped),
            InvalidNormalCount: faces.Count(face => face.Access == MoldFaceAccess.Invalid),
            RiskySurfaceAreaMm2: Round(faces.Where(face => face.Access != MoldFaceAccess.Clear).Sum(face => face.AreaMm2)),
            MaximumTrapDepthMm: Round(sideAction.Length == 0 ? 0 : sideAction.Max(face => face.SideTravelMm)),
            SampledSurfaceAreaMm2: Round(access.TotalAreaMm2));
    }

    private static IReadOnlyList<string> Notes(
        MeshMoldAccessResult access,
        DraftBasicGeometryAnalysisResult draft,
        decimal minimumDraftDeg)
    {
        var notes = new List<string>
        {
            Invariant($"Draft and undercut measured on the input STL for a two-half mold split by the plane {access.Axis} = {access.PartingOffsetMm:0.###} mm; each half opens away from the plane.")
        };

        if (access.Faces.Count == 0 || !(access.TotalAreaMm2 > 0))
        {
            notes.Add("Draft and undercut could not be measured: the STL has no surface area.");
            return notes;
        }

        var total = access.TotalAreaMm2;
        var faces = access.Faces;
        var results = faces.Count;

        var steepNegative = faces
            .Where(face => face.Access != MoldFaceAccess.Invalid && face.PullDot < -SteepNegativeDot)
            .Sum(face => face.AreaMm2);

        notes.Add(Invariant(
            $"Draft: {Percent(AreaOf(draft, DraftFaceClassification.PositiveDraft), total)} of the surface has at least {minimumDraftDeg:0.###} deg, {Percent(AreaOf(draft, DraftFaceClassification.LowDraft), total)} less than that, {Percent(AreaOf(draft, DraftFaceClassification.ZeroDraft), total)} none (0.1 deg or less) and {Percent(AreaOf(draft, DraftFaceClassification.NegativeDraft), total)} negative draft ({Percent(steepNegative, total)} steeper than -1 deg)."));

        var trapped = faces.Where(face => face.Access is MoldFaceAccess.SideAction or MoldFaceAccess.Trapped).ToArray();

        if (trapped.Length == 0)
        {
            notes.Add("Undercut: every face releases along the opening direction of its half.");
        }
        else
        {
            var trappedArea = trapped.Sum(face => face.AreaMm2);
            notes.Add(Invariant(
                $"Undercut: {trappedArea:0.0} mm2 ({Percent(trappedArea, total)}) of the surface is trapped along the opening direction of its half."));

            var sideAction = trapped.Where(face => face.Access == MoldFaceAccess.SideAction).ToArray();

            if (sideAction.Length > 0)
            {
                var longest = sideAction.OrderByDescending(face => face.SideTravelMm).ThenBy(face => face.TriangleIndex).First();
                notes.Add(Invariant(
                    $"{sideAction.Sum(face => face.AreaMm2):0.0} mm2 ({sideAction.Length} triangles) can be released by moving sideways first (side action or lifter), with up to {longest.SideTravelMm:0.00} mm of travel, the longest at {Point(longest.Centroid)}."));
            }

            var noEscape = trapped.Where(face => face.Access == MoldFaceAccess.Trapped).ToArray();

            if (noEscape.Length > 0)
            {
                notes.Add(Invariant(
                    $"{noEscape.Sum(face => face.AreaMm2):0.0} mm2 ({noEscape.Length} triangles) has no sideways escape (undercut), between {Point(Min(noEscape))} and {Point(Max(noEscape))}."));
            }
        }

        var invalid = faces.Where(face => face.Access == MoldFaceAccess.Invalid).ToArray();

        if (invalid.Length > 0)
        {
            notes.Add(Invariant(
                $"{invalid.Length} of {results} triangles ({invalid.Sum(face => face.AreaMm2):0.0} mm2) are degenerate or face into the part within {access.ResolutionMm:0.###} mm (folded slivers of the mesh or gaps narrower than that); they count as invalid normals."));
        }

        if (access.OrientationFlipped)
        {
            notes.Add("The STL is inside out (negative signed volume); draft and undercut were measured with its normals flipped.");
        }

        return notes;
    }

    private static double AreaOf(DraftBasicGeometryAnalysisResult draft, DraftFaceClassification classification)
    {
        return (double)draft.Faces
            .Where(face => face.Classification == classification)
            .Sum(face => face.SurfaceAreaMm2);
    }

    private static Vec3d Pull(PartingAxis axis) => axis switch
    {
        PartingAxis.X => new Vec3d(1, 0, 0),
        PartingAxis.Y => new Vec3d(0, 1, 0),
        PartingAxis.Z => new Vec3d(0, 0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
    };

    private static MeshVertex Min(IEnumerable<MoldAccessFace> faces) => new(
        faces.Min(face => face.Centroid.X),
        faces.Min(face => face.Centroid.Y),
        faces.Min(face => face.Centroid.Z));

    private static MeshVertex Max(IEnumerable<MoldAccessFace> faces) => new(
        faces.Max(face => face.Centroid.X),
        faces.Max(face => face.Centroid.Y),
        faces.Max(face => face.Centroid.Z));

    private static string Point(MeshVertex v) => Invariant($"({v.X:0.0}, {v.Y:0.0}, {v.Z:0.0}) mm");

    private static string Percent(double part, double total) => Invariant($"{100 * part / total:0.0} %");

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
