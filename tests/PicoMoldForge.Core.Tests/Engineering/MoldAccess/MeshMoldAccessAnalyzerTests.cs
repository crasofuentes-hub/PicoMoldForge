using PicoMoldForge.Core.Engineering.DraftAnalysis;
using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Tests.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.MoldAccess;

public sealed class MeshMoldAccessAnalyzerTests
{
    [Theory]
    [InlineData(PartingAxis.X, 10.0)]
    [InlineData(PartingAxis.Y, 5.0)]
    [InlineData(PartingAxis.Z, 2.5)]
    public void Box_SplitThroughItsMiddle_HasNoNegativeDraftAndNoUndercut(PartingAxis axis, double offset)
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();

        var result = MeshMoldAccessAnalyzer.Analyze(box, axis, offset);

        Assert.All(result.Faces, face => Assert.Equal(MoldFaceAccess.Clear, face.Access));
        Assert.All(result.Faces, face => Assert.True(face.PullDot >= 0, $"triangle {face.TriangleIndex}"));

        // The two faces across the axis open along their own normal; the other four are parallel to it.
        Assert.Equal(4, result.Faces.Count(face => Math.Abs(face.PullDot - 1) < 1e-12));
        Assert.Equal(8, result.Faces.Count(face => Math.Abs(face.PullDot) < 1e-12));
        Assert.Equal(700, result.TotalAreaMm2, 9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    public void FaceLyingInThePartingPlane_BelongsToTheHalfItFaces(double offset)
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();

        var result = MeshMoldAccessAnalyzer.Analyze(box, PartingAxis.Z, offset);

        Assert.Equal(-1, result.Faces[0].Half);
        Assert.Equal(1, result.Faces[2].Half);
        Assert.Equal(1.0, result.Faces[0].PullDot, 12);
        Assert.Equal(1.0, result.Faces[2].PullDot, 12);
        Assert.All(result.Faces, face => Assert.Equal(MoldFaceAccess.Clear, face.Access));
    }

    [Fact]
    public void NoisyFaceInThePartingPlane_BelongsToTheHalfItFaces()
    {
        // Top face of the box 0.01 mm off flat at two corners: its centroids sit 0.0067 mm under the plane z = 5.
        MeshVertex Noise(MeshVertex v) =>
            v.Z == 5 && ((v.X == 0 && v.Y == 0) || (v.X == 20 && v.Y == 10)) ? v with { Z = 4.99 } : v;
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox()
            .Select(t => new MeshTriangle(Noise(t.A), Noise(t.B), Noise(t.C)))
            .ToArray();

        var result = MeshMoldAccessAnalyzer.Analyze(box, PartingAxis.Z, 5.0);
        var exact = MeshMoldAccessAnalyzer.Analyze(box, PartingAxis.Z, 5.0, new MeshMoldAccessOptions(ResolutionMm: 0.001));

        Assert.Equal(1, result.Faces[2].Half);
        Assert.Equal(1, result.Faces[3].Half);
        Assert.All(result.Faces, face => Assert.Equal(MoldFaceAccess.Clear, face.Access));

        // Resolved finer than the noise, the top face falls in the lower half and is trapped by itself.
        Assert.Equal(-1, exact.Faces[2].Half);
        Assert.Equal(MoldFaceAccess.SideAction, exact.Faces[2].Access);
    }

    [Fact]
    public void TShape_SplitBelowTheCap_NeedsASideAction_SplitInsideTheCap_DoesNot()
    {
        var t = TShape();

        var belowCap = MeshMoldAccessAnalyzer.Analyze(t, PartingAxis.Z, 5.0);
        var insideCap = MeshMoldAccessAnalyzer.Analyze(t, PartingAxis.Z, 12.5);

        // Below the cap: the cap underside (4 triangles, negative draft for the upper half) and the upper halves
        // of the two stem sides are trapped; each slides out 3.33 mm along the extrusion before opening.
        var trapped = belowCap.Faces.Where(face => face.Access != MoldFaceAccess.Clear).ToArray();
        Assert.Equal(6, trapped.Length);
        Assert.All(trapped, face => Assert.Equal(MoldFaceAccess.SideAction, face.Access));
        Assert.All(trapped, face => Assert.Equal(10.0 / 3.0, face.SideTravelMm, 4));
        Assert.Equal(4, trapped.Count(face => face.PullDot < -0.999));

        Assert.All(insideCap.Faces, face => Assert.Equal(MoldFaceAccess.Clear, face.Access));
        Assert.All(insideCap.Faces, face => Assert.True(face.PullDot >= 0));
    }

    [Fact]
    public void HoleAcrossThePull_NeedsASideCore_HoleAlongThePull_DoesNot()
    {
        var frame = SquareFrame();

        var alongHole = MeshMoldAccessAnalyzer.Analyze(frame, PartingAxis.Z, 5.0);
        var acrossHole = MeshMoldAccessAnalyzer.Analyze(frame, PartingAxis.X, 10.0);

        Assert.All(alongHole.Faces, face => Assert.Equal(MoldFaceAccess.Clear, face.Access));

        // Pulled along X, the 8 hole wall triangles are trapped; a core leaving through the top or the bottom of
        // the hole (3.33 mm from each centroid) frees them. The outer faces stay clear.
        var holeWalls = Enumerable.Range(8, 8).ToArray();
        Assert.All(holeWalls, i => Assert.Equal(MoldFaceAccess.SideAction, acrossHole.Faces[i].Access));
        Assert.All(holeWalls, i => Assert.Equal(10.0 / 3.0, acrossHole.Faces[i].SideTravelMm, 4));
        Assert.All(
            acrossHole.Faces.Where(face => !holeWalls.Contains(face.TriangleIndex)),
            face => Assert.Equal(MoldFaceAccess.Clear, face.Access));
    }

    [Fact]
    public void InternalVoid_IsTrappedWithNoEscape()
    {
        var hollow = MeshWallThicknessSamplerTests.Box(0, 0, 0, 20, 20, 20)
            .Concat(MeshWallThicknessSamplerTests.Box(5, 5, 5, 10, 10, 10).Select(Reverse))
            .ToArray();

        var result = MeshMoldAccessAnalyzer.Analyze(hollow, PartingAxis.Z, 10.0);

        Assert.All(result.Faces.Take(12), face => Assert.Equal(MoldFaceAccess.Clear, face.Access));
        Assert.All(result.Faces.Skip(12), face => Assert.Equal(MoldFaceAccess.Trapped, face.Access));
        Assert.All(result.Faces, face => Assert.Equal(0.0, face.SideTravelMm));
    }

    [Fact]
    public void TaperedWalls_MeasureTheirDraft_AndANegativeDraftWallSlidesOutByItsOffset()
    {
        var taper = 10 * Math.Tan(2 * Math.PI / 180);
        var block = Tapered(taper);

        // Split at the base: the sloped walls belong to the upper half and have 2 deg of draft.
        var atBase = MeshMoldAccessAnalyzer.Analyze(block, PartingAxis.Z, 0.0);
        var walls = atBase.Faces.Where(face => Math.Abs(face.PullDot) > 1e-9 && Math.Abs(face.PullDot) < 0.5).ToArray();

        Assert.Equal(4, walls.Length);
        Assert.All(walls, face => Assert.Equal(Math.Sin(2 * Math.PI / 180), face.PullDot, 12));
        Assert.All(atBase.Faces, face => Assert.Equal(MoldFaceAccess.Clear, face.Access));

        // Split at the top: the walls belong to the lower half (-2 deg). The steel point, 0.1 mm off each wall,
        // must move out over the base edge: taper * z / 10 - 0.1 cos(2 deg) at the centroid height z (10/3, 20/3).
        var atTop = MeshMoldAccessAnalyzer.Analyze(block, PartingAxis.Z, 10.0);
        var negative = atTop.Faces.Where(face => face.PullDot < -1e-9).ToArray();
        var travels = negative.Select(face => face.SideTravelMm).OrderBy(value => value).ToArray();
        var lift = 0.1 * Math.Cos(2 * Math.PI / 180);

        Assert.Equal(4, negative.Length);
        Assert.All(negative, face => Assert.Equal(MoldFaceAccess.SideAction, face.Access));
        Assert.Equal((taper / 3) - lift, travels[0], 6);
        Assert.Equal((taper / 3) - lift, travels[1], 6);
        Assert.Equal((2 * taper / 3) - lift, travels[2], 6);
        Assert.Equal((2 * taper / 3) - lift, travels[3], 6);
    }

    [Fact]
    public void InsideOutMesh_IsAnalyzedTheSameWay()
    {
        var t = TShape();
        var reversed = t.Select(Reverse).ToArray();

        var normal = MeshMoldAccessAnalyzer.Analyze(t, PartingAxis.Z, 5.0);
        var flipped = MeshMoldAccessAnalyzer.Analyze(reversed, PartingAxis.Z, 5.0);

        Assert.False(normal.OrientationFlipped);
        Assert.True(flipped.OrientationFlipped);
        Assert.Equal(normal.Faces.Select(face => (face.Half, face.Access)), flipped.Faces.Select(face => (face.Half, face.Access)));
        Assert.Equal(normal.Faces.Select(face => Math.Round(face.PullDot, 9)), flipped.Faces.Select(face => Math.Round(face.PullDot, 9)));
        Assert.Equal(normal.Faces.Select(face => Math.Round(face.SideTravelMm, 6)), flipped.Faces.Select(face => Math.Round(face.SideTravelMm, 6)));
    }

    [Fact]
    public void Results_DoNotDependOnThreads()
    {
        var frame = SquareFrame();

        var one = MeshMoldAccessAnalyzer.Analyze(frame, PartingAxis.X, 10.0, new MeshMoldAccessOptions(MaxDegreeOfParallelism: 1));
        var four = MeshMoldAccessAnalyzer.Analyze(frame, PartingAxis.X, 10.0, new MeshMoldAccessOptions(MaxDegreeOfParallelism: 4));

        Assert.Equal(one.Faces, four.Faces);
    }

    [Fact]
    public void DegenerateTriangle_IsInvalid()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox().ToList();
        box.Add(new MeshTriangle(new MeshVertex(0, 0, 0), new MeshVertex(1, 0, 0), new MeshVertex(2, 0, 0)));

        var result = MeshMoldAccessAnalyzer.Analyze(box, PartingAxis.Z, 2.5);

        Assert.Equal(MoldFaceAccess.Invalid, result.Faces[12].Access);
        Assert.Equal(0, result.Faces[12].Half);
    }

    [Fact]
    public void FoldedSliver_FacingIntoThePart_IsInvalid_NotAnUndercut()
    {
        // A sliver folded back just under the top face: its normal points down into the box.
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox().ToList();
        box.Add(new MeshTriangle(new MeshVertex(5, 5, 4.99), new MeshVertex(5, 5.5, 4.99), new MeshVertex(5.5, 5, 4.99)));

        var result = MeshMoldAccessAnalyzer.Analyze(box, PartingAxis.Z, 2.5);
        var analysis = MeshMoldAccessAnalysis.Analyze(box, PartingAxis.Z, 2.5m, minimumDraftDeg: 1.0m);

        Assert.Equal(MoldFaceAccess.Invalid, result.Faces[12].Access);
        Assert.All(result.Faces.Take(12), face => Assert.Equal(MoldFaceAccess.Clear, face.Access));
        Assert.Equal(1, analysis.Undercut.InvalidNormalCount);
        Assert.Equal(0, analysis.Undercut.UndercutCount);
        Assert.Equal(DraftFaceClassification.InvalidNormal, analysis.Draft.Faces[12].Classification);
        Assert.Contains(analysis.Notes, note => note.StartsWith("1 of 13 triangles (0.1 mm2) are degenerate or face into the part within 0.1 mm", StringComparison.Ordinal));
    }

    [Fact]
    public void Analysis_OfTheGeneratorTestBox_SplitAcrossX_ReportsNoNegativeDraftAndNoUndercut()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();

        var result = MeshMoldAccessAnalysis.Analyze(box, PartingAxis.X, 10m, minimumDraftDeg: 1.0m);

        Assert.Equal(12, result.Draft.Summary.FaceCount);
        Assert.Equal(4, result.Draft.Summary.PositiveDraftCount);
        Assert.Equal(8, result.Draft.Summary.ZeroDraftCount);
        Assert.Equal(0, result.Draft.Summary.NegativeDraftCount);
        Assert.Equal(600m, result.Draft.Summary.RiskySurfaceAreaMm2);
        Assert.Equal(700m, result.Draft.Summary.SampledSurfaceAreaMm2);
        Assert.Equal(0m, result.Draft.Summary.MinimumObservedDraftDeg);

        Assert.Equal(12, result.Undercut.FaceCount);
        Assert.Equal(12, result.Undercut.ClearPullCount);
        Assert.Equal(0, result.Undercut.SideActionCandidateCount);
        Assert.Equal(0, result.Undercut.UndercutCount);
        Assert.Equal(0m, result.Undercut.RiskySurfaceAreaMm2);
        Assert.Equal(700m, result.Undercut.SampledSurfaceAreaMm2);

        Assert.Equal("Draft and undercut measured on the input STL for a two-half mold split by the plane X = 10 mm; each half opens away from the plane.", result.Notes[0]);
        Assert.Contains("Draft: 14.3 % of the surface has at least 1 deg, 0.0 % less than that, 85.7 % none (0.1 deg or less) and 0.0 % negative draft (0.0 % steeper than -1 deg).", result.Notes);
        Assert.Contains("Undercut: every face releases along the opening direction of its half.", result.Notes);
    }

    [Fact]
    public void Analysis_OfTheTShape_ReportsTheSideActionAndItsTravel()
    {
        var result = MeshMoldAccessAnalysis.Analyze(TShape(), PartingAxis.Z, 5m, minimumDraftDeg: 1.0m);

        Assert.Equal(6, result.Undercut.SideActionCandidateCount);
        Assert.Equal(0, result.Undercut.UndercutCount);
        Assert.Equal(0, result.Undercut.LowPullClearanceCount);
        Assert.Equal(3.3333m, result.Undercut.MaximumTrapDepthMm);
        Assert.Equal(4, result.Draft.Summary.NegativeDraftCount);

        // Trapped: the cap underside (2 x 10 x 10 mm2) and the upper halves of the stem sides (2 x 50 mm2).
        Assert.Equal(300m, result.Undercut.RiskySurfaceAreaMm2);
        Assert.Contains(result.Notes, note => note.StartsWith("Undercut: 300.0 mm2 (", StringComparison.Ordinal));
        Assert.Contains(result.Notes, note => note.StartsWith("300.0 mm2 (6 triangles) can be released by moving sideways first (side action or lifter), with up to 3.33 mm of travel", StringComparison.Ordinal));
    }

    [Fact]
    public void Analysis_OfAnInternalVoid_ReportsAnUndercutAndWhere()
    {
        var hollow = MeshWallThicknessSamplerTests.Box(0, 0, 0, 20, 20, 20)
            .Concat(MeshWallThicknessSamplerTests.Box(5, 5, 5, 10, 10, 10).Select(Reverse))
            .ToArray();

        var result = MeshMoldAccessAnalysis.Analyze(hollow, PartingAxis.Z, 10m, minimumDraftDeg: 1.0m);

        Assert.Equal(12, result.Undercut.UndercutCount);
        Assert.Equal(600m, result.Undercut.RiskySurfaceAreaMm2);
        Assert.Equal(0m, result.Undercut.MaximumTrapDepthMm);
        Assert.Contains(result.Notes, note => note.StartsWith("600.0 mm2 (12 triangles) has no sideways escape (undercut), between (", StringComparison.Ordinal));
    }

    [Fact]
    public void Draft_UsesTheOpeningDirectionOfEachHalf()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();

        var result = MeshMoldAccessAnalysis.Analyze(box, PartingAxis.Z, 2.5m, minimumDraftDeg: 1.0m);

        // Against a single pull +Z the bottom would have negative draft; against its own half it has 90 deg.
        Assert.Equal(DraftFaceClassification.PositiveDraft, result.Draft.Faces[0].Classification);
        Assert.Equal(90m, Math.Round(result.Draft.Faces[0].DraftAngleDeg, 4));
        Assert.Equal(0, result.Draft.Summary.NegativeDraftCount);
    }

    /// <summary>
    /// Stem 10 x 10 mm (x 10..20, z 0..10) under a 30 x 5 mm cap (z 10..15), extruded 10 mm along Y.
    /// </summary>
    internal static IReadOnlyList<MeshTriangle> TShape() => MeshWallThicknessSamplerTests.Extrude(
        new[] { (10.0, 0.0), (20.0, 0.0), (20.0, 10.0), (30.0, 10.0), (30.0, 15.0), (0.0, 15.0), (0.0, 10.0), (10.0, 10.0) },
        10,
        new[] { (0, 1, 2), (0, 2, 7), (5, 6, 7), (5, 7, 2), (5, 2, 3), (5, 3, 4) });

    /// <summary>20 x 10 mm base narrowing by <paramref name="taper"/> on each side over its 10 mm height, 10 mm deep.</summary>
    private static IReadOnlyList<MeshTriangle> Tapered(double taper) => MeshWallThicknessSamplerTests.Extrude(
        new[] { (0.0, 0.0), (20.0, 0.0), (20.0 - taper, 10.0), (taper, 10.0) },
        10,
        new[] { (0, 1, 2), (0, 2, 3) });

    /// <summary>
    /// 20 x 20 x 10 mm block with an 8 x 8 mm hole along Z (x and y 6..14). Triangles 0-7: outer walls, 8-15: hole
    /// walls, 16-23: top, 24-31: bottom.
    /// </summary>
    internal static IReadOnlyList<MeshTriangle> SquareFrame()
    {
        const double h = 10;
        var outer = new[] { (0.0, 0.0), (20.0, 0.0), (20.0, 20.0), (0.0, 20.0) };
        var inner = new[] { (6.0, 6.0), (14.0, 6.0), (14.0, 14.0), (6.0, 14.0) };
        var triangles = new List<MeshTriangle>();

        MeshVertex V((double X, double Y) p, double z) => new(p.X, p.Y, z);

        void Quad(MeshVertex a, MeshVertex b, MeshVertex c, MeshVertex d)
        {
            triangles.Add(new MeshTriangle(a, b, c));
            triangles.Add(new MeshTriangle(a, c, d));
        }

        for (var k = 0; k < 4; k++)
        {
            Quad(V(outer[k], 0), V(outer[(k + 1) % 4], 0), V(outer[(k + 1) % 4], h), V(outer[k], h));
        }

        for (var k = 0; k < 4; k++)
        {
            Quad(V(inner[(k + 1) % 4], 0), V(inner[k], 0), V(inner[k], h), V(inner[(k + 1) % 4], h));
        }

        for (var k = 0; k < 4; k++)
        {
            Quad(V(outer[k], h), V(outer[(k + 1) % 4], h), V(inner[(k + 1) % 4], h), V(inner[k], h));
        }

        for (var k = 0; k < 4; k++)
        {
            Quad(V(outer[k], 0), V(inner[k], 0), V(inner[(k + 1) % 4], 0), V(outer[(k + 1) % 4], 0));
        }

        return triangles;
    }

    private static MeshTriangle Reverse(MeshTriangle t) => new(t.A, t.C, t.B);
}
