using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Engineering.Clearance;
using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.MoldLayout;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.Verification;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Tests.Engineering.MoldAccess;
using PicoMoldForge.Core.Tests.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.Verification;

public sealed class MoldVerifierTests
{
    private static readonly MoldLayoutBlock Block = new(-40, -40, -40, 60, 50, 45);

    [Fact]
    public void Box_WithoutHalves_PassesWhatCanBeCheckedAndSaysWhatWasNotRun()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();

        var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, halves: null));

        Assert.Equal(MoldCheckStatus.Pass, report.Status);
        Assert.StartsWith("PASS: every check run passed (4 not run", report.Summary, StringComparison.Ordinal);
        Assert.Equal(MoldCheckStatus.Pass, Status(report, "two-halves"));
        Assert.Equal(MoldCheckStatus.Pass, Status(report, "feed"));
        Assert.Equal(MoldCheckStatus.Pass, Status(report, "ejector-pins"));
        Assert.Equal(MoldCheckStatus.Pass, Status(report, "cooling"));
        Assert.Equal(MoldCheckStatus.Pass, Status(report, "guide-pins"));
        Assert.Equal(MoldCheckStatus.NotRun, Status(report, "halves-cut"));
        Assert.Empty(report.Zones);
    }

    [Fact]
    public void TShape_SplitUnderTheBar_FailsWithTheZonesThatNeedASlide()
    {
        // Split at the bottom of the stem: the steel under both arms of the bar is held by the bar when the + half
        // opens, and can only leave sideways.
        var shape = MeshMoldAccessAnalyzerTests.TShape();

        var report = MoldVerifier.Verify(Input(shape, PartingAxis.Z, 0, halves: null));

        Assert.Equal(MoldCheckStatus.Fail, report.Status);
        Assert.Equal(MoldCheckStatus.Fail, Status(report, "two-halves"));
        Assert.Equal(2, report.ZoneCount);
        Assert.All(report.Zones, zone =>
        {
            Assert.Contains("side action", zone.Mechanism, StringComparison.Ordinal);
            Assert.True(zone.SideTravelMm > 0);
            Assert.Equal(10, zone.Max.Z, 6);
        });
        Assert.StartsWith("FAIL: the part leaves the mold in two halves", report.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.1, "a side action (one or more slides)")]
    [InlineData(50.0, "an undercut no straight slide releases")]
    public void Zone_IsAnUndercutOnlyWhenARealShareOfItIsTrapped(double trappedArea, string mechanism)
    {
        // Two touching triangles: one released by a slide (100 mm2), one with no sideways escape. A trapped sliver
        // of 0.1 mm2 in a bore that a slide releases is mesh noise (the monoblock showed one such face flipping a
        // 565 mm2 zone into an undercut on one side only of a symmetric part).
        var part = new[]
        {
            new MeshTriangle(new MeshVertex(0, 0, 0), new MeshVertex(10, 0, 0), new MeshVertex(0, 20, 0)),
            new MeshTriangle(new MeshVertex(0, 0, 0), new MeshVertex(0, 20, 0), new MeshVertex(-10, 0, 0))
        };
        var access = new MeshMoldAccessResult(
            new[]
            {
                new MoldAccessFace(0, 100, -1, 0, MoldFaceAccess.SideAction, 4.5, new MeshVertex(3, 6, 0)),
                new MoldAccessFace(1, trappedArea, -1, 0, MoldFaceAccess.Trapped, 0, new MeshVertex(-3, 6, 0))
            },
            PartingAxis.Z,
            0,
            1000,
            false,
            0.1);

        var report = MoldVerifier.Verify(Input(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 0, halves: null) with
        {
            PartTriangles = part,
            Access = access
        });

        var zone = Assert.Single(report.Zones);
        Assert.StartsWith(mechanism, zone.Mechanism, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.05, MoldCheckStatus.Warn, 0)]
    [InlineData(0.1, MoldCheckStatus.Fail, 1)]
    [InlineData(0.15, MoldCheckStatus.Fail, 1)]
    public void Zone_FreedByLessThanTheResolution_IsReportedButNotCounted(double travel, MoldCheckStatus expected, int zoneCount)
    {
        // The monoblock showed a 4.6 mm2 strip of crankcase wall held by an overshoot of 0.125 mm of the mesh at a
        // sharp corner 18 mm below it: every face of the strip is freed by moving its steel point 0.053 mm, under the
        // 0.1 mm resolution of the analysis. A zone that needs the resolution or more still fails.
        var part = new[]
        {
            new MeshTriangle(new MeshVertex(0, 0, 0), new MeshVertex(10, 0, 0), new MeshVertex(0, 20, 0)),
            new MeshTriangle(new MeshVertex(0, 0, 0), new MeshVertex(0, 20, 0), new MeshVertex(-10, 0, 0))
        };
        var access = new MeshMoldAccessResult(
            new[]
            {
                new MoldAccessFace(0, 100, -1, 0, MoldFaceAccess.SideAction, travel, new MeshVertex(3, 6, 0)),
                new MoldAccessFace(1, 50, -1, 0, MoldFaceAccess.SideAction, travel, new MeshVertex(-3, 6, 0))
            },
            PartingAxis.Z,
            0,
            1000,
            false,
            0.1);

        var report = MoldVerifier.Verify(Input(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 0, halves: null) with
        {
            PartTriangles = part,
            Access = access
        });

        Assert.Equal(expected, Status(report, "two-halves"));
        Assert.Equal(zoneCount, report.ZoneCount);
        Assert.Equal(expected == MoldCheckStatus.Warn ? MoldCheckStatus.Pass : MoldCheckStatus.Fail, report.Status);

        if (expected == MoldCheckStatus.Warn)
        {
            Assert.Contains("under the 0.1 mm resolution of the analysis", Detail(report, "two-halves"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Halves_WithTheSteelEachSystemShouldRemove_Pass()
    {
        // One cooling channel 6 mm across the + half of the block, clear of the box: the expected steel is the
        // cylinder, 100 x pi x 9 mm3.
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var channel = new MoldHalfFeature("c1", "Cooling", 1, -40, 5, 25, 60, 5, 25, 3, RoundEnds: false, AddsSteel: false);
        var expected = Math.PI * 9 * 100;
        var halves = Halves(movingRemoved: expected, overlap: 0);

        var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, halves, new[] { channel }) with { Layout = null });

        Assert.Equal(MoldCheckStatus.Pass, Status(report, "halves-cut"));
        Assert.Equal(MoldCheckStatus.Pass, Status(report, "halves-overlap"));
        Assert.Equal(MoldCheckStatus.Pass, Status(report, "halves-open"));
    }

    [Fact]
    public void Halves_WithNothingCut_FailTheCutCheck()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var channel = new MoldHalfFeature("c1", "Cooling", 1, -40, 5, 25, 60, 5, 25, 3, RoundEnds: false, AddsSteel: false);

        var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, Halves(movingRemoved: 0, overlap: 0), new[] { channel }));

        Assert.Equal(MoldCheckStatus.Fail, Status(report, "halves-cut"));
        Assert.Contains("(FAIL)", Detail(report, "halves-cut"), StringComparison.Ordinal);
    }

    [Fact]
    public void Halves_ThatShareSteel_FailTheOverlapCheck()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();

        // 0.05 voxel x 100 x 90 mm2 of parting plane at 1 mm voxels: 450 mm3 allowed.
        var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, Halves(movingRemoved: 0, overlap: 2000)));

        Assert.Equal(MoldCheckStatus.Fail, Status(report, "halves-overlap"));
    }

    [Fact]
    public void CavityShape_MeasuresTheHalvesAgainstTheCavityMesh()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            // Halves whose cavity surface is the cavity itself, then one 2 % too large.
            var exact = WriteHalves(directory, "exact", box);
            var large = WriteHalves(directory, "large", Scaled(box, 1.02));

            var passing = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, exact, voxel: 0.05));
            var failing = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, large, voxel: 0.05));

            Assert.Equal(MoldCheckStatus.Pass, Status(passing, "cavity-shape"));
            Assert.Equal(MoldCheckStatus.Fail, Status(failing, "cavity-shape"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MoldVerificationInput Input(
        IReadOnlyList<MeshTriangle> part,
        PartingAxis axis,
        double offset,
        FunctionalMoldHalvesResult? halves,
        IReadOnlyList<MoldHalfFeature>? features = null,
        double voxel = 1.0)
    {
        var layout = MoldLayoutPlanner.Plan(new MoldLayoutRequest(
            part, axis, offset, Block, 5, 4, 4, 6, 15, 3, 10, 10));
        var access = MeshMoldAccessAnalyzer.Analyze(part, axis, offset);

        return new MoldVerificationInput(
            part,
            access,
            CavityInsideBlock: true,
            layout,
            new ClearanceCollisionMatrixSummary(10, 40, 0, 0, 5m, 2m),
            part,
            Block,
            axis,
            offset,
            features ?? Array.Empty<MoldHalfFeature>(),
            halves,
            voxel);
    }

    private static FunctionalMoldHalvesResult Halves(double movingRemoved, double overlap, string? movingPath = null, string? fixedPath = null)
    {
        var moving = new FunctionalMoldHalfResult(
            "Moving", 1, movingPath ?? "missing-moving.stl", 1000, 900,
            new[] { new MoldHalfSystemVolume("Cooling", movingRemoved, 0) },
            MinAlongAxisMm: 0, MaxAlongAxisMm: 45, TriangleCount: 12);
        var fixedHalf = new FunctionalMoldHalfResult(
            "Fixed", -1, fixedPath ?? "missing-fixed.stl", 1000, 1000,
            Array.Empty<MoldHalfSystemVolume>(),
            MinAlongAxisMm: -40, MaxAlongAxisMm: 0, TriangleCount: 12);
        return new FunctionalMoldHalvesResult(moving, fixedHalf, overlap, 1.0, Array.Empty<string>());
    }

    private static FunctionalMoldHalvesResult WriteHalves(string directory, string name, IReadOnlyList<MeshTriangle> cavitySurface)
    {
        var moving = Path.Combine(directory, name + "-moving.stl");
        var fixedPath = Path.Combine(directory, name + "-fixed.stl");
        BinaryStlTriangleWriter.Write(moving, cavitySurface);
        BinaryStlTriangleWriter.Write(fixedPath, new[] { new MeshTriangle(new MeshVertex(-40, -40, -40), new MeshVertex(-39, -40, -40), new MeshVertex(-40, -39, -40)) });
        return Halves(0, 0, moving, fixedPath);
    }

    private static IReadOnlyList<MeshTriangle> Scaled(IReadOnlyList<MeshTriangle> triangles, double factor)
    {
        MeshVertex S(MeshVertex v) => new(10 + ((v.X - 10) * factor), 5 + ((v.Y - 5) * factor), 2.5 + ((v.Z - 2.5) * factor));
        return triangles.Select(t => new MeshTriangle(S(t.A), S(t.B), S(t.C))).ToArray();
    }

    private static MoldCheckStatus Status(MoldVerificationReport report, string id) => report.Checks.Single(c => c.Id == id).Status;

    private static string Detail(MoldVerificationReport report, string id) => report.Checks.Single(c => c.Id == id).Detail;
}
