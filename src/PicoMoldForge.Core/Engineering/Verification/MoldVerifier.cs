using System.Globalization;
using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Engineering.Clearance;
using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.MoldLayout;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.Verification;

public enum MoldCheckStatus
{
    Pass = 0,
    Warn = 1,
    Fail = 2,
    NotRun = 3
}

/// <summary>One check of the final mold: what was measured and the verdict.</summary>
public sealed record MoldVerificationCheck(string Id, string Title, MoldCheckStatus Status, string Detail);

/// <summary>
/// A connected zone of the part that two halves cannot release: the mechanism it needs, its area and its bounding box
/// (part coordinates, mm). <paramref name="SideTravelMm"/> is the side travel that frees 95 % of its area and
/// <paramref name="MaximumSideTravelMm"/> the longest travel of any of its faces (both 0 for an undercut no straight
/// slide releases).
/// </summary>
public sealed record MoldTrappedZone(
    string Mechanism,
    double AreaMm2,
    MoldLayoutPoint Min,
    MoldLayoutPoint Max,
    double SideTravelMm,
    int Half,
    double MaximumSideTravelMm = 0);

/// <summary>The verdict on the final mold: PASS when no check fails; every check, and the zones that need more than two halves.</summary>
public sealed record MoldVerificationReport(
    MoldCheckStatus Status,
    string Summary,
    IReadOnlyList<MoldVerificationCheck> Checks,
    IReadOnlyList<MoldTrappedZone> Zones,
    int ZoneCount);

/// <summary>
/// What the verifier looks at. <paramref name="Access"/> is the draft/undercut access of the part on its split;
/// <paramref name="HalfFeatures"/> the features cut into (or added to) the halves; <paramref name="Halves"/> the halves
/// built by PicoGK (null when they were not built: those checks are not run).
/// </summary>
public sealed record MoldVerificationInput(
    IReadOnlyList<MeshTriangle> PartTriangles,
    MeshMoldAccessResult? Access,
    bool CavityInsideBlock,
    MoldLayoutPlan? Layout,
    ClearanceCollisionMatrixSummary? Clearances,
    IReadOnlyList<MeshTriangle> CavityTriangles,
    MoldLayoutBlock Block,
    PartingAxis Axis,
    double MoldPartingOffsetMm,
    IReadOnlyList<MoldHalfFeature> HalfFeatures,
    FunctionalMoldHalvesResult? Halves,
    double VoxelSizeMm);

/// <summary>
/// Verification of the final mold (pure .NET). Its checks:
/// <list type="bullet">
/// <item>Whether the part leaves two halves, or which zones need a slide, a lifter or a design change.</item>
/// <item>Whether the feed, ejector pins, cooling and guide pins are in place with their clearances.</item>
/// <item>On the halves built by PicoGK:
/// <list type="bullet">
/// <item>whether each system removed (or added) the steel it should: a Monte Carlo estimate of the exact volume,
/// within half a voxel of its surface, and points on the axes of its features tested against the half;</item>
/// <item>whether the halves overlap;</item>
/// <item>whether they open along the axis;</item>
/// <item>whether their cavity follows the cavity mesh (distance from points of the cavity mesh to the halves, 90th
/// percentile within one voxel);</item>
/// <item>whether each half is a closed surface in one piece.</item>
/// </list>
/// </item>
/// </list>
/// The summary names the checks that passed with a warning.
/// </summary>
public static class MoldVerifier
{
    private const int SamplesPerSystem = 40000;
    private const int CavitySamples = 3000;
    private const int MaxReportedZones = 10;

    public static MoldVerificationReport Verify(MoldVerificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var checks = new List<MoldVerificationCheck>();
        var zones = new List<MoldTrappedZone>();

        checks.Add(input.CavityInsideBlock
            ? Check("cavity-in-block", "The cavity lies inside the mold block", MoldCheckStatus.Pass, "Every point of the cavity is inside the block.")
            : Check("cavity-in-block", "The cavity lies inside the mold block", MoldCheckStatus.Fail, "The cavity touches or crosses a face of the block; see the block fit."));

        checks.Add(TwoHalves(input, zones));
        checks.AddRange(Layout(input));
        checks.AddRange(Halves(input));

        var status = checks.Any(c => c.Status == MoldCheckStatus.Fail) ? MoldCheckStatus.Fail : MoldCheckStatus.Pass;
        var notRun = checks.Count(c => c.Status == MoldCheckStatus.NotRun);
        var failed = checks.Where(c => c.Status == MoldCheckStatus.Fail).Select(c => c.Title.ToLowerInvariant()).ToArray();
        var warned = checks.Where(c => c.Status == MoldCheckStatus.Warn).Select(c => c.Id).ToArray();
        var summary = status == MoldCheckStatus.Pass
            ? "PASS: " + (notRun > 0 ? Invariant($"every check run passed ({notRun} not run without the PicoGK halves).") : "every check passed.")
            : "FAIL: " + string.Join("; ", failed) + ".";

        if (warned.Length > 0)
        {
            summary += Invariant($" {warned.Length} with a warning: {string.Join(", ", warned)}.");
        }

        var ordered = zones.OrderByDescending(z => z.AreaMm2).ToArray();
        return new MoldVerificationReport(status, summary, checks, ordered.Take(MaxReportedZones).ToArray(), ordered.Length);
    }

    // ---------------------------------------------------------------------------------------------------------

    private static MoldVerificationCheck TwoHalves(MoldVerificationInput input, List<MoldTrappedZone> zones)
    {
        const string Title = "The part leaves the mold in two halves";

        if (input.Access is null)
        {
            return Check("two-halves", Title, MoldCheckStatus.NotRun, "No draft/undercut access of the part was given.");
        }

        var faces = input.Access.Faces;
        var total = input.Access.TotalAreaMm2;
        var blocked = faces.Where(f => f.Access is MoldFaceAccess.Trapped or MoldFaceAccess.SideAction).ToArray();
        var blockedArea = blocked.Sum(f => f.AreaMm2);
        var tolerance = Math.Max(1.0, 0.001 * total);

        if (blockedArea <= tolerance)
        {
            return Check("two-halves", Title, MoldCheckStatus.Pass, Invariant($"Every face releases along the opening direction of its half ({blockedArea:0.##} mm2 held, within {tolerance:0.##} mm2 of mesh noise)."));
        }

        // Zones under 1 mm2 are slivers of the mesh: counted, not listed. A zone whose every face is freed by a
        // sideways move shorter than the resolution of the analysis is not resolved: the steel point sits one
        // resolution off the face, so the zone is held by a step under twice the resolution (on the monoblock, an
        // overshoot of 0.125 mm of the mesh at a sharp corner held a 4.6 mm2 strip of wall 18 mm above it). It is
        // reported and not counted.
        var resolution = input.Access.ResolutionMm;
        var all = Zones(input.PartTriangles, blocked, resolution).ToArray();
        var small = all.Where(z => z.Zone.AreaMm2 < 1.0).Select(z => z.Zone).ToArray();
        var unresolved = all.Where(z => z.Zone.AreaMm2 >= 1.0 && z.BelowResolution).Select(z => z.Zone).OrderByDescending(z => z.AreaMm2).ToArray();
        zones.AddRange(all.Where(z => z.Zone.AreaMm2 >= 1.0 && !z.BelowResolution).Select(z => z.Zone));
        var unresolvedArea = unresolved.Sum(z => z.AreaMm2);
        var unresolvedText = unresolved.Length == 0
            ? string.Empty
            : Invariant($"{unresolved.Length} zone(s) ({unresolvedArea:0.#} mm2, the largest {unresolved[0].AreaMm2:0.#} mm2 at {Box(unresolved[0])}) are freed by a sideways move under the {resolution:0.###} mm resolution of the analysis: held by a step under {2 * resolution:0.###} mm (mesh noise at a sharp edge, or a real step that small), they are not counted");
        var heldArea = blockedArea - unresolvedArea;

        if (heldArea <= tolerance)
        {
            return Check("two-halves", Title, MoldCheckStatus.Warn, Invariant($"Every face releases along the opening direction of its half, except: {unresolvedText}; check them on the real part."));
        }

        var text = Invariant($"{heldArea:0.#} mm2 ({100 * heldArea / total:0.#} % of the surface) cannot leave the two halves, in {zones.Count} zone(s)") +
            (small.Length > 0 ? Invariant($" and {small.Length} sliver(s) under 1 mm2 ({small.Sum(z => z.AreaMm2):0.#} mm2)") : string.Empty);
        var largest = zones.OrderByDescending(z => z.AreaMm2).Take(3)
            .Select(z => Invariant($"{z.AreaMm2:0.#} mm2 at {Box(z)}: {z.Mechanism}"))
            .ToArray();
        return Check(
            "two-halves",
            Title,
            MoldCheckStatus.Fail,
            text + (largest.Length > 0 ? ": " + string.Join("; ", largest) : string.Empty) + "." +
            (unresolvedText.Length > 0 ? " Besides, " + unresolvedText + "." : string.Empty));
    }

    private static string Box(MoldTrappedZone z) =>
        Invariant($"X {z.Min.X:0.#}..{z.Max.X:0.#}, Y {z.Min.Y:0.#}..{z.Max.Y:0.#}, Z {z.Min.Z:0.#}..{z.Max.Z:0.#}");

    /// <summary>
    /// Faces that touch (share a vertex) form one zone. <c>BelowResolution</c>: the zone is released by slides only
    /// and none of its faces needs a travel of <paramref name="resolutionMm"/> or more.
    /// </summary>
    private static IEnumerable<(MoldTrappedZone Zone, bool BelowResolution)> Zones(IReadOnlyList<MeshTriangle> triangles, IReadOnlyList<MoldAccessFace> faces, double resolutionMm)
    {
        var parent = Enumerable.Range(0, faces.Count).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        var owner = new Dictionary<MeshVertex, int>();

        for (var k = 0; k < faces.Count; k++)
        {
            var t = triangles[faces[k].TriangleIndex];

            foreach (var v in new[] { t.A, t.B, t.C })
            {
                if (owner.TryGetValue(v, out var other))
                {
                    parent[Find(k)] = Find(other);
                }
                else
                {
                    owner[v] = k;
                }
            }
        }

        foreach (var group in Enumerable.Range(0, faces.Count).GroupBy(Find))
        {
            var members = group.Select(k => faces[k]).ToArray();
            var vertices = group.SelectMany(k =>
            {
                var t = triangles[faces[k].TriangleIndex];
                return new[] { t.A, t.B, t.C };
            }).ToArray();
            // The zone needs more than a slide when a real share of it has no sideways escape: a sliver of a few
            // trapped triangles in a bore that a slide releases is mesh noise.
            var area = members.Sum(f => f.AreaMm2);
            var trappedArea = members.Where(f => f.Access == MoldFaceAccess.Trapped).Sum(f => f.AreaMm2);
            var trapped = trappedArea > Math.Max(1.0, 0.05 * area);
            var sides = members.Where(f => f.Access == MoldFaceAccess.SideAction).OrderBy(f => f.SideTravelMm).ToArray();
            var travel = sides.Length == 0 ? 0 : sides[^1].SideTravelMm;

            // The travel quoted frees 95 % of the zone's area, the share the trapped area is judged by. The longest
            // travel of a single face is no measure of the zone: on the piston, 99 % of a ring groove 2.6 mm deep
            // is freed by 2.9 mm, while one face of 0.18 mm2 read 11.65 mm; on the monoblock, the bore through the
            // 10 mm bulkhead reads 5.06 mm at 95 % (half the bulkhead) and 9.5 mm at its longest face.
            var travel95 = AreaQuantile(sides, 0.95);
            var rest = travel > (1.1 * travel95) + 0.1
                ? Invariant($" for 95 % of its area (up to {travel:0.##} mm for the rest)")
                : string.Empty;

            // Each face is released by some sideways move; whether one slide releases the whole zone (a bore) or it
            // takes several (a groove all around the part) is not decided here.
            var mechanism = trapped
                ? Invariant($"an undercut no straight slide releases ({trappedArea:0.#} mm2 of it): a lifter, a collapsible core or a design change")
                : Invariant($"a side action (one or more slides) with at least {travel95:0.##} mm of travel{rest}");
            var half = members.Sum(f => f.Half * f.AreaMm2) >= 0 ? 1 : -1;

            var zone = new MoldTrappedZone(
                mechanism,
                area,
                new MoldLayoutPoint(vertices.Min(v => v.X), vertices.Min(v => v.Y), vertices.Min(v => v.Z)),
                new MoldLayoutPoint(vertices.Max(v => v.X), vertices.Max(v => v.Y), vertices.Max(v => v.Z)),
                trapped ? 0 : travel95,
                half,
                trapped ? 0 : travel);
            yield return (zone, !trapped && sides.Length > 0 && travel < resolutionMm);
        }
    }

    /// <summary>
    /// The smallest side travel that frees <paramref name="share"/> of the area of the faces (sorted by travel); 0 for
    /// no faces.
    /// </summary>
    private static double AreaQuantile(IReadOnlyList<MoldAccessFace> sortedByTravel, double share)
    {
        var total = sortedByTravel.Sum(f => f.AreaMm2);
        var covered = 0.0;

        foreach (var face in sortedByTravel)
        {
            covered += face.AreaMm2;

            if (covered >= share * total)
            {
                return face.SideTravelMm;
            }
        }

        return sortedByTravel.Count == 0 ? 0 : sortedByTravel[^1].SideTravelMm;
    }

    // ---------------------------------------------------------------------------------------------------------

    private static IEnumerable<MoldVerificationCheck> Layout(MoldVerificationInput input)
    {
        var layout = input.Layout;

        if (layout is null)
        {
            yield return Check("layout", "Feed, ejector pins, cooling and guide pins are placed", MoldCheckStatus.Fail, "No layout was placed (the cavity does not fit in the block).");
            yield break;
        }

        var feed = layout.Feed;
        yield return feed is null
            ? Check("feed", "The feed reaches the cavity", MoldCheckStatus.Fail, "No gate could be placed on the cavity outline in the parting plane.")
            : feed.IsFeasible
                ? Check("feed", "The feed reaches the cavity", MoldCheckStatus.Pass, Invariant($"Edge gate {feed.Segments[0].DiameterMm:0.##} mm into a {feed.EntryThicknessMm:0.##} mm section; runner and sprue at least {feed.Segments.Where(s => s.Type != GateSystem.GateSystemFeatureType.Gate).Min(s => s.CavityClearanceMm):0.##} mm from the cavity and inside the block."))
                : Check("feed", "The feed reaches the cavity", MoldCheckStatus.Fail, string.Join("; ", feed.Problems) + ".");

        yield return layout.RequestedPinCount <= 0
            ? Check("ejector-pins", "Ejector pins push the part off the moving half", MoldCheckStatus.Warn, "No ejector pins configured.")
            : layout.Pins.Count == 0
                ? Check("ejector-pins", "Ejector pins push the part off the moving half", MoldCheckStatus.Fail, "No face of the moving half takes a pin of the configured diameter; see the ejector pin note.")
                : Check("ejector-pins", "Ejector pins push the part off the moving half", MoldCheckStatus.Pass, Invariant($"{layout.Pins.Count} of {layout.RequestedPinCount} pins on the moving half, stroke {layout.Pins[0].StrokeMm:0.##} mm."));

        var positive = layout.Channels.Count(c => c.Half > 0);
        var negative = layout.Channels.Count(c => c.Half < 0);
        yield return positive > 0 && negative > 0
            ? Check("cooling", "Both halves are cooled", MoldCheckStatus.Pass, Invariant($"{positive} channel(s) in the + half and {negative} in the - half, at least {layout.Channels.Min(c => c.CavityClearanceMm):0.##} mm from the cavity."))
            : Check("cooling", "Both halves are cooled", MoldCheckStatus.Warn, Invariant($"{positive} channel(s) in the + half and {negative} in the - half; see the cooling note (a half without channels cools slowly)."));

        yield return layout.GuidePins.Count >= 2
            ? Check("guide-pins", "Guide pins locate the halves", MoldCheckStatus.Pass, Invariant($"{layout.GuidePins.Count} guide pins {layout.GuidePins[0].DiameterMm:0.##} mm in bores of {layout.GuidePins[0].BoreDiameterMm:0.##} mm."))
            : Check("guide-pins", "Guide pins locate the halves", MoldCheckStatus.Warn, Invariant($"{layout.GuidePins.Count} guide pin(s): the halves need at least two to locate; see the guide pin note."));

        if (input.Clearances is { } clearances)
        {
            yield return clearances.CollisionRiskPairCount == 0
                ? Check("clearances", "Features keep their steel between them", MoldCheckStatus.Pass, Invariant($"{clearances.PairCount} pairs of features, the closest {clearances.MinimumSurfaceClearanceMm:0.##} mm apart."))
                : Check("clearances", "Features keep their steel between them", MoldCheckStatus.Fail, Invariant($"{clearances.CollisionRiskPairCount} pairs of features are closer than their clearance."));
        }
    }

    // ---------------------------------------------------------------------------------------------------------

    private static IEnumerable<MoldVerificationCheck> Halves(MoldVerificationInput input)
    {
        var halves = input.Halves;

        if (halves is null)
        {
            const string Why = "The halves were not built (they need the PicoGK runtime).";
            yield return Check("halves-cut", "Each system removed the steel it should", MoldCheckStatus.NotRun, Why);
            yield return Check("halves-overlap", "The halves do not overlap", MoldCheckStatus.NotRun, Why);
            yield return Check("halves-open", "The halves open along the axis", MoldCheckStatus.NotRun, Why);
            yield return Check("cavity-shape", "The cavity in the halves follows the cavity mesh", MoldCheckStatus.NotRun, Why);
            yield return Check("halves-mesh", MeshTitle, MoldCheckStatus.NotRun, Why);
            yield break;
        }

        var voxel = input.VoxelSizeMm;
        var (movingSolid, fixedSolid, missing) = LoadHalves(halves);
        yield return Cut(input, halves, voxel, movingSolid, fixedSolid);

        var o = input.MoldPartingOffsetMm;
        var plane = PlaneArea(input.Block, input.Axis);
        var overlapTolerance = 0.05 * voxel * plane;
        yield return halves.OverlapVolumeMm3 <= overlapTolerance
            ? Check("halves-overlap", "The halves do not overlap", MoldCheckStatus.Pass, Invariant($"{halves.OverlapVolumeMm3:0.##} mm3 shared, within {overlapTolerance:0.##} mm3 (0.05 voxel over the parting plane)."))
            : Check("halves-overlap", "The halves do not overlap", MoldCheckStatus.Fail, Invariant($"{halves.OverlapVolumeMm3:0.##} mm3 shared, more than {overlapTolerance:0.##} mm3: the halves would collide."));

        var protrusion = input.Layout?.GuidePins.Select(g => Math.Abs(Height(g.Tip, input.Axis) - o)).DefaultIfEmpty(0).Max() ?? 0;
        var moving = halves.Moving;
        var fixedHalf = halves.Fixed;
        var m = moving.Half;
        var movingBeyond = m > 0 ? o - moving.MinAlongAxisMm : moving.MaxAlongAxisMm - o;
        var fixedBeyond = m > 0 ? fixedHalf.MaxAlongAxisMm - o : o - fixedHalf.MinAlongAxisMm;
        yield return movingBeyond <= protrusion + voxel && fixedBeyond <= voxel
            ? Check("halves-open", "The halves open along the axis", MoldCheckStatus.Pass, Invariant($"Only the guide pins cross the parting plane ({movingBeyond:0.##} mm, along the axis); the fixed half stays on its side ({fixedBeyond:0.##} mm)."))
            : Check("halves-open", "The halves open along the axis", MoldCheckStatus.Fail, Invariant($"The moving half reaches {movingBeyond:0.##} mm and the fixed half {fixedBeyond:0.##} mm across the parting plane (allowed {protrusion + voxel:0.##} and {voxel:0.##} mm)."));

        yield return CavityShape(input, voxel, movingSolid, fixedSolid, missing);
        yield return MeshCheck(halves, movingSolid, fixedSolid, missing);
    }

    private const string MeshTitle = "Each half is a closed surface in one piece";

    /// <summary>
    /// Reads both half STL files once (null with the reason when a file is missing or too large to check here).
    /// </summary>
    private static (HalfSolid? Moving, HalfSolid? Fixed, string? Missing) LoadHalves(FunctionalMoldHalvesResult halves)
    {
        if (!File.Exists(halves.Moving.OutputPath) || !File.Exists(halves.Fixed.OutputPath))
        {
            return (null, null, "The half STL files were not found.");
        }

        // 50 bytes per triangle in a binary STL; beyond ~20 million triangles the checks would need gigabytes here.
        var bytes = new FileInfo(halves.Moving.OutputPath).Length + new FileInfo(halves.Fixed.OutputPath).Length;

        if (bytes > 1_000_000_000L)
        {
            return (null, null, Invariant($"The half STL files are {bytes / 1_000_000L} MB, too large to check here."));
        }

        return (new HalfSolid(BinaryStlTriangleReader.Read(halves.Moving.OutputPath)), new HalfSolid(BinaryStlTriangleReader.Read(halves.Fixed.OutputPath)), null);
    }

    /// <summary>
    /// The halves are made by voxel booleans and read back as STL triangles. A half is fit to cut or print from that
    /// file when its surface is closed (every edge in two triangles) and it is one piece: a hole in the surface leaves
    /// the solid undefined, and a second piece is steel no half holds. Edges in three or more triangles (sheets that
    /// pinch together) still bound a solid; some CAM and slicer software asks to repair them, so they are a warning.
    /// </summary>
    private static MoldVerificationCheck MeshCheck(FunctionalMoldHalvesResult halves, HalfSolid? moving, HalfSolid? fixedHalf, string? missing)
    {
        if (moving is null || fixedHalf is null)
        {
            return Check("halves-mesh", MeshTitle, MoldCheckStatus.NotRun, missing ?? "The half STL files were not read.");
        }

        var parts = new[] { (halves.Moving.Role, MeshIntegrity.Measure(moving.Triangles), halves.Moving), (halves.Fixed.Role, MeshIntegrity.Measure(fixedHalf.Triangles), halves.Fixed) };
        var broken = parts.Where(p => p.Item2.Triangles == 0 || p.Item2.OpenEdges > 0 || p.Item2.Pieces != 1).ToArray();
        var pinched = parts.Where(p => p.Item2.NonManifoldEdges > 0).ToArray();
        var text = string.Join("; ", parts.Select(p => Invariant(
            $"{p.Role} half: {p.Item2.Triangles} triangles, {p.Item2.OpenEdges} open edges, {p.Item2.Pieces} piece(s), {p.Item2.NonManifoldEdges} edges in more than two triangles") +
            (p.Item3.IslandsRemoved > 0 ? Invariant($", {p.Item3.IslandsRemoved} island(s) under one voxel ({p.Item3.IslandVolumeMm3:0.####} mm3) left out of the file") : string.Empty) +
            (p.Item2.Pieces > 1 ? " (other pieces: " + string.Join(", ", p.Item2.PieceList.Skip(1).Take(4).Select(x => Piece(x, Math.Sign(p.Item2.PieceList[0].VolumeMm3)))) + ")" : string.Empty) +
            (p.Item2.NonManifoldNear.Count > 0 ? " (near " + string.Join(", ", p.Item2.NonManifoldNear.Select(v => Invariant($"({v.X:0.#}, {v.Y:0.#}, {v.Z:0.#})"))) + ")" : string.Empty)));

        // A piece wound against the largest one is a void inside the solid.
        static string Piece(MeshPiece piece, int solidSign) => Invariant(
            $"{(Math.Sign(piece.VolumeMm3) == -solidSign && solidSign != 0 ? "a void of" : "a solid of")} {Math.Abs(piece.VolumeMm3):0.####} mm3 at X {piece.Min.X:0.#}..{piece.Max.X:0.#}, Y {piece.Min.Y:0.#}..{piece.Max.Y:0.#}, Z {piece.Min.Z:0.#}..{piece.Max.Z:0.#}");

        if (broken.Length > 0)
        {
            return Check("halves-mesh", MeshTitle, MoldCheckStatus.Fail, text + ". A half with open edges, no triangles or more than one piece does not define its steel.");
        }

        return pinched.Length > 0
            ? Check("halves-mesh", MeshTitle, MoldCheckStatus.Warn, text + ". The halves are closed and in one piece, but pinched edges make some CAM or slicer software ask for a repair.")
            : Check("halves-mesh", MeshTitle, MoldCheckStatus.Pass, text + ".");
    }

    private static MoldVerificationCheck Cut(MoldVerificationInput input, FunctionalMoldHalvesResult halves, double voxel, HalfSolid? movingSolid, HalfSolid? fixedSolid)
    {
        const string Title = "Each system removed the steel it should";
        var lines = new List<string>();
        var failed = false;
        var unresolved = false;
        var cavity = new InsideTester(input.CavityTriangles);

        foreach (var half in new[] { halves.Moving, halves.Fixed })
        {
            var removedBefore = new List<Capsule>();
            var solid = ReferenceEquals(half, halves.Moving) ? movingSolid : fixedSolid;

            foreach (var system in FunctionalMoldHalvesRequest.SystemOrder)
            {
                var features = input.HalfFeatures
                    .Where(f => f.System == system && (f.Half == half.Half || f.Half == 0))
                    .ToArray();
                var capsules = features.Select(Capsule.From).ToArray();

                if (capsules.Length == 0)
                {
                    continue;
                }

                var adds = input.HalfFeatures.First(f => f.System == system).AddsSteel;
                var (expected, error, area) = Expected(capsules, input, half.Half, cavity, removedBefore, adds);
                var measured = half.Systems.FirstOrDefault(s => s.System == system);
                var value = measured is null ? 0 : adds ? measured.AddedVolumeMm3 : measured.RemovedVolumeMm3;
                var tolerance = (0.5 * voxel * area) + (3 * error);
                var ok = Math.Abs(value - expected) <= tolerance;

                // The volume alone cannot tell a thin feature from no feature when the tolerance reaches its volume;
                // the points on the axes of the features say whether the half has the hole (or the pin) there.
                var (tested, wrong) = solid is null ? (0, 0) : Presence(features, capsules, input, half.Half, cavity, removedBefore, adds, solid, voxel);
                var present = wrong == 0;
                var resolved = expected > tolerance || tested > 0;
                failed |= !ok || !present;
                unresolved |= ok && present && !resolved;
                var axis = tested == 0
                    ? string.Empty
                    : Invariant($", {tested - wrong} of {tested} points on the axes {(adds ? "in steel" : "empty")}");
                var verdict = !ok || !present ? " (FAIL)" : resolved ? string.Empty : Invariant($" (cannot tell at {voxel:0.###} mm voxels)");
                lines.Add(Invariant($"{half.Role} half {system}: {(adds ? "added" : "removed")} {value:0.#} mm3, expected {expected:0.#} +- {tolerance:0.#}{axis}{verdict}"));

                if (!adds)
                {
                    removedBefore.AddRange(capsules);
                }
            }
        }

        var status = failed ? MoldCheckStatus.Fail : unresolved ? MoldCheckStatus.Warn : MoldCheckStatus.Pass;
        return Check("halves-cut", Title, status, lines.Count == 0 ? "No feature to cut." : string.Join("; ", lines) + ".");
    }

    /// <summary>
    /// Points on the axes of the features of one system, where the plan says the system changes the steel of the half
    /// (in the half's box, out of the cavity and of what earlier systems removed; for added steel, out of that steel),
    /// tested against the half's solid: a removed feature leaves them empty, an added one fills them. A feature on the
    /// parting plane (in both halves) is tested half a radius into this half. Features whose points would lie within
    /// half a voxel of their surface are finer than the voxels resolve and are left out.
    /// Returns how many points were tested and how many disagree.
    /// </summary>
    private static (int Tested, int Wrong) Presence(
        IReadOnlyList<MoldHalfFeature> features,
        IReadOnlyList<Capsule> capsules,
        MoldVerificationInput input,
        int half,
        InsideTester cavity,
        IReadOnlyList<Capsule> removedBefore,
        bool adds,
        HalfSolid solid,
        double voxel)
    {
        var tested = 0;
        var wrong = 0;
        var into = AxisVector(input.Axis) * half;

        for (var i = 0; i < features.Count; i++)
        {
            var c = capsules[i];
            var onPlane = features[i].Half == 0;
            var margin = onPlane ? c.R / 2 : c.R;

            if (margin < 0.5 * voxel)
            {
                continue;
            }

            foreach (var t in new[] { 0.125, 0.375, 0.625, 0.875 })
            {
                var p = c.A + ((c.B - c.A) * t) + (onPlane ? into * (c.R / 2) : new Vec3d(0, 0, 0));
                var steel = InHalfBox(p, input.Block, input.Axis, input.MoldPartingOffsetMm, half) &&
                    !cavity.Contains(p) &&
                    !removedBefore.Any(r => r.Contains(p));

                if (steel == adds)
                {
                    continue;
                }

                tested++;

                if (solid.Contains(p) != adds)
                {
                    wrong++;
                }
            }
        }

        return (tested, wrong);
    }

    private static Vec3d AxisVector(PartingAxis axis) => axis switch
    {
        PartingAxis.X => new Vec3d(1, 0, 0),
        PartingAxis.Y => new Vec3d(0, 1, 0),
        _ => new Vec3d(0, 0, 1)
    };

    /// <summary>
    /// Monte Carlo estimate of the steel a set of capsules removes from (or adds to) a half: the volume of their union
    /// inside the half's box, outside the cavity and outside what earlier systems removed (for an added system, the
    /// part of the union outside that steel). Each sample counts once: only in the first capsule that holds it.
    /// Returns the estimate, its standard error and the surface area of the capsules (for the voxel tolerance).
    /// </summary>
    private static (double Volume, double Error, double Area) Expected(
        IReadOnlyList<Capsule> capsules,
        MoldVerificationInput input,
        int half,
        InsideTester cavity,
        IReadOnlyList<Capsule> removedBefore,
        bool adds)
    {
        var random = new Random(12345 + (half * 17) + capsules.Count);
        var totalVolume = capsules.Sum(c => c.Volume);
        var volume = 0.0;
        var variance = 0.0;

        for (var i = 0; i < capsules.Count; i++)
        {
            var c = capsules[i];
            var n = Math.Max(200, (int)(SamplesPerSystem * c.Volume / totalVolume));
            var hits = 0;

            for (var s = 0; s < n; s++)
            {
                var p = c.Sample(random);

                if (capsules.Take(i).Any(other => other.Contains(p)))
                {
                    continue;
                }

                var steel = InHalfBox(p, input.Block, input.Axis, input.MoldPartingOffsetMm, half) &&
                    !cavity.Contains(p) &&
                    !removedBefore.Any(r => r.Contains(p));

                if (steel != adds)
                {
                    hits++;
                }
            }

            var fraction = (double)hits / n;
            volume += c.Volume * fraction;
            variance += c.Volume * c.Volume * fraction * (1 - fraction) / n;
        }

        return (volume, Math.Sqrt(variance), capsules.Sum(c => c.Area));
    }

    private static MoldVerificationCheck CavityShape(MoldVerificationInput input, double voxel, HalfSolid? moving, HalfSolid? fixedHalf, string? missing)
    {
        const string Title = "The cavity in the halves follows the cavity mesh";

        if (moving is null || fixedHalf is null)
        {
            return Check("cavity-shape", Title, MoldCheckStatus.NotRun, missing ?? "The half STL files were not read.");
        }

        if (moving.Triangles.Count + fixedHalf.Triangles.Count == 0)
        {
            return Check("cavity-shape", Title, MoldCheckStatus.Fail, "The half STL files hold no triangles.");
        }

        var distances = SurfaceSamples(input.CavityTriangles, CavitySamples)
            .Select(p => Math.Min(moving.Nearest(p), fixedHalf.Nearest(p)))
            .OrderBy(d => d)
            .ToArray();
        var median = distances[distances.Length / 2];
        var p90 = distances[(int)(0.9 * (distances.Length - 1))];
        var ok = p90 <= voxel + 1e-9;
        return Check(
            "cavity-shape",
            Title,
            ok ? MoldCheckStatus.Pass : MoldCheckStatus.Fail,
            Invariant($"Distance from {distances.Length} points of the cavity mesh to the halves: median {median:0.###} mm, 90th percentile {p90:0.###} mm, largest {distances[^1]:0.###} mm (features opening into the cavity); allowed one voxel, {voxel:0.###} mm. The shrinkage is checked at this resolution only."));
    }

    /// <summary>Points spread over the surface by area, deterministic.</summary>
    private static IEnumerable<Vec3d> SurfaceSamples(IReadOnlyList<MeshTriangle> triangles, int count)
    {
        var areas = triangles.Select(t => Vec3d.Cross(Vec3d.From(t.B) - Vec3d.From(t.A), Vec3d.From(t.C) - Vec3d.From(t.A)).Length / 2).ToArray();
        var total = areas.Sum();
        var random = new Random(4242);

        if (!(total > 0))
        {
            yield break;
        }

        for (var i = 0; i < triangles.Count; i++)
        {
            var n = (int)Math.Round(count * areas[i] / total);

            for (var k = 0; k < n; k++)
            {
                var r1 = Math.Sqrt(random.NextDouble());
                var r2 = random.NextDouble();
                var a = Vec3d.From(triangles[i].A);
                var b = Vec3d.From(triangles[i].B);
                var c = Vec3d.From(triangles[i].C);
                yield return (a * (1 - r1)) + (b * (r1 * (1 - r2))) + (c * (r1 * r2));
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------

    private static bool InHalfBox(Vec3d p, MoldLayoutBlock b, PartingAxis axis, double offset, int half)
    {
        if (p.X < b.MinX || p.X > b.MaxX || p.Y < b.MinY || p.Y > b.MaxY || p.Z < b.MinZ || p.Z > b.MaxZ)
        {
            return false;
        }

        var h = axis == PartingAxis.X ? p.X : axis == PartingAxis.Y ? p.Y : p.Z;
        return half > 0 ? h >= offset : h <= offset;
    }

    private static double PlaneArea(MoldLayoutBlock b, PartingAxis axis) => axis switch
    {
        PartingAxis.X => (b.MaxY - b.MinY) * (b.MaxZ - b.MinZ),
        PartingAxis.Y => (b.MaxZ - b.MinZ) * (b.MaxX - b.MinX),
        _ => (b.MaxX - b.MinX) * (b.MaxY - b.MinY)
    };

    private static double Height(MoldLayoutPoint p, PartingAxis axis) => axis == PartingAxis.X ? p.X : axis == PartingAxis.Y ? p.Y : p.Z;

    private static MoldVerificationCheck Check(string id, string title, MoldCheckStatus status, string detail) => new(id, title, status, detail);

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A cylinder of radius R around [A, B], with half-sphere ends when Round.</summary>
    private sealed record Capsule(Vec3d A, Vec3d B, double R, bool Round)
    {
        public double Length => (B - A).Length;

        public double Volume => (Math.PI * R * R * Length) + (Round ? 4.0 / 3.0 * Math.PI * R * R * R : 0);

        public double Area => (2 * Math.PI * R * Length) + (Round ? 4 * Math.PI * R * R : 2 * Math.PI * R * R);

        public static Capsule From(MoldHalfFeature f) =>
            new(new Vec3d(f.StartXmm, f.StartYmm, f.StartZmm), new Vec3d(f.EndXmm, f.EndYmm, f.EndZmm), f.RadiusMm, f.RoundEnds);

        public bool Contains(Vec3d p)
        {
            var axis = B - A;
            var lengthSquared = Vec3d.Dot(axis, axis);
            var t = lengthSquared > 0 ? Vec3d.Dot(p - A, axis) / lengthSquared : 0;

            if (!Round && (t < 0 || t > 1))
            {
                return false;
            }

            var closest = A + (axis * Math.Clamp(t, 0, 1));
            var d = p - closest;
            return Vec3d.Dot(d, d) <= R * R;
        }

        /// <summary>A point uniform in the capsule (rejection from its bounding cylinder).</summary>
        public Vec3d Sample(Random random)
        {
            var length = Length;
            var direction = length > 0 ? (B - A) * (1 / length) : new Vec3d(0, 0, 1);
            var helper = Math.Abs(direction.X) < 0.9 ? new Vec3d(1, 0, 0) : new Vec3d(0, 1, 0);
            var e1 = Vec3d.Cross(direction, helper);
            e1 *= 1 / e1.Length;
            var e2 = Vec3d.Cross(direction, e1);
            var extra = Round ? R : 0;

            for (var attempt = 0; attempt < 1000; attempt++)
            {
                var t = (-extra) + (random.NextDouble() * (length + (2 * extra)));
                var x = ((2 * random.NextDouble()) - 1) * R;
                var y = ((2 * random.NextDouble()) - 1) * R;
                var p = A + (direction * t) + (e1 * x) + (e2 * y);

                if ((x * x) + (y * y) <= R * R && Contains(p))
                {
                    return p;
                }
            }

            return A + (direction * (length / 2));
        }
    }

    /// <summary>
    /// One half read back from its STL: its triangles, the nearest-surface distance, and an inside test that takes the
    /// majority of three oblique rays (a ray leaves a closed mesh through the first face it meets).
    /// </summary>
    private sealed class HalfSolid
    {
        private static readonly Vec3d[] Directions =
        {
            new(0.3093, 0.5162, 0.7987),
            new(-0.6402, 0.2871, 0.7125),
            new(0.4479, -0.8017, 0.3958)
        };

        private readonly TriangleBvh? _bvh;
        private readonly Vec3d[] _normals;

        public HalfSolid(IReadOnlyList<MeshTriangle> triangles)
        {
            Triangles = triangles;
            _normals = new Vec3d[triangles.Count];

            if (triangles.Count == 0)
            {
                return;
            }

            var volume = 0.0;

            foreach (var t in triangles)
            {
                volume += Vec3d.Dot(Vec3d.From(t.A), Vec3d.Cross(Vec3d.From(t.B), Vec3d.From(t.C))) / 6.0;
            }

            var sign = volume < 0 ? -1.0 : 1.0;

            for (var i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                _normals[i] = Vec3d.Cross(Vec3d.From(t.B) - Vec3d.From(t.A), Vec3d.From(t.C) - Vec3d.From(t.A)) * sign;
            }

            _bvh = new TriangleBvh(triangles);
        }

        public IReadOnlyList<MeshTriangle> Triangles { get; }

        public double Nearest(Vec3d p) => _bvh is null ? double.PositiveInfinity : _bvh.Nearest(p).Distance;

        public bool Contains(Vec3d p)
        {
            if (_bvh is null)
            {
                return false;
            }

            var votes = 0;

            foreach (var direction in Directions)
            {
                var (_, triangle) = _bvh.RayHit(p, direction, -1);

                if (triangle >= 0 && Vec3d.Dot(_normals[triangle], direction) > 0)
                {
                    votes++;
                }
            }

            return votes >= 2;
        }
    }

    /// <summary>Inside test of a closed mesh: a ray leaves through the first face it meets (oblique direction).</summary>
    private sealed class InsideTester
    {
        private static readonly Vec3d Direction = new(0.3093, 0.5162, 0.7987);
        private readonly TriangleBvh? _bvh;
        private readonly Vec3d[] _normals;

        public InsideTester(IReadOnlyList<MeshTriangle> triangles)
        {
            _normals = new Vec3d[triangles.Count];

            if (triangles.Count == 0)
            {
                return;
            }

            var volume = 0.0;

            foreach (var t in triangles)
            {
                volume += Vec3d.Dot(Vec3d.From(t.A), Vec3d.Cross(Vec3d.From(t.B), Vec3d.From(t.C))) / 6.0;
            }

            var sign = volume < 0 ? -1.0 : 1.0;

            for (var i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                _normals[i] = Vec3d.Cross(Vec3d.From(t.B) - Vec3d.From(t.A), Vec3d.From(t.C) - Vec3d.From(t.A)) * sign;
            }

            _bvh = new TriangleBvh(triangles);
        }

        public bool Contains(Vec3d p)
        {
            if (_bvh is null)
            {
                return false;
            }

            var (_, triangle) = _bvh.RayHit(p, Direction, -1);
            return triangle >= 0 && Vec3d.Dot(_normals[triangle], Direction) > 0;
        }
    }
}
