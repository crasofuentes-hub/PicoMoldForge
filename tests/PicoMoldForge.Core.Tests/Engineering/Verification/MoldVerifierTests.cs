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
        Assert.Equal("PASS: every check run passed (5 not run without the PicoGK halves).", report.Summary);
        Assert.Equal(MoldCheckStatus.NotRun, Status(report, "halves-mesh"));
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

    [Theory]
    [InlineData(true, MoldCheckStatus.Pass)]
    [InlineData(false, MoldCheckStatus.Fail)]
    public void HalvesCut_TestsTheAxisOfEachFeatureAgainstTheHalf(bool drilled, MoldCheckStatus expected)
    {
        // A 6 mm cooling channel along X through the + half. The volume figure says the steel was removed; the axis
        // points say whether the half really has the hole: a drilled half (a square tunnel round the axis) passes, a
        // solid half fails, whatever the volume reads.
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var channel = new MoldHalfFeature("c1", "Cooling", 1, -40, 5, 25, 60, 5, 25, 3, RoundEnds: false, AddsSteel: false);
        var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var moving = Path.Combine(directory, "moving.stl");
            var fixedPath = Path.Combine(directory, "fixed.stl");
            BinaryStlTriangleWriter.Write(moving, drilled ? BoxWithTunnelAlongX(-40, -40, 0, 60, 50, 45, 2, 22, 8, 28) : BoxMesh(-40, -40, 0, 60, 50, 45));
            BinaryStlTriangleWriter.Write(fixedPath, BoxMesh(-40, -40, -40, 60, 50, 0));
            var halves = Halves(movingRemoved: Math.PI * 9 * 100, overlap: 0, moving, fixedPath);

            var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, halves, new[] { channel }) with { Layout = null });

            Assert.Equal(expected, Status(report, "halves-cut"));
            Assert.Contains(drilled ? "4 of 4 points on the axes empty" : "0 of 4 points on the axes empty", Detail(report, "halves-cut"), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void HalvesCut_ConfirmsAThinFeatureByItsAxis()
    {
        // A 1.6 mm hole at 1 mm voxels: half a voxel over its surface (about 253 mm3) exceeds its volume (about
        // 201 mm3), so the volume alone cannot tell it from no hole; its axis, 0.8 mm from its wall, still can.
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var hole = new MoldHalfFeature("h1", "Cooling", 1, -40, 5, 25, 60, 5, 25, 0.8, RoundEnds: false, AddsSteel: false);
        var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var moving = Path.Combine(directory, "moving.stl");
            var fixedPath = Path.Combine(directory, "fixed.stl");
            BinaryStlTriangleWriter.Write(moving, BoxWithTunnelAlongX(-40, -40, 0, 60, 50, 45, 4.2, 24.2, 5.8, 25.8));
            BinaryStlTriangleWriter.Write(fixedPath, BoxMesh(-40, -40, -40, 60, 50, 0));

            var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, Halves(Math.PI * 0.64 * 100, 0, moving, fixedPath), new[] { hole }) with { Layout = null });

            Assert.Equal(MoldCheckStatus.Pass, Status(report, "halves-cut"));
            Assert.DoesNotContain("cannot tell", Detail(report, "halves-cut"), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void HalvesCut_WarnsWhenTheVoxelsCannotTellAFeatureFromNone()
    {
        // A 0.4 mm hole at 1 mm voxels: half a voxel over its surface is more than its volume, and its axis lies within
        // half a voxel of its wall, so neither the volume nor the axis can confirm it.
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var hole = new MoldHalfFeature("h1", "Cooling", 1, -40, 5, 25, 60, 5, 25, 0.2, RoundEnds: false, AddsSteel: false);

        var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, Halves(movingRemoved: Math.PI * 0.04 * 100, overlap: 0), new[] { hole }) with { Layout = null });

        Assert.Equal(MoldCheckStatus.Warn, Status(report, "halves-cut"));
        Assert.Contains("(cannot tell at 1 mm voxels)", Detail(report, "halves-cut"), StringComparison.Ordinal);
        Assert.Contains("1 with a warning: halves-cut.", report.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("closed", MoldCheckStatus.Pass)]
    [InlineData("pinched", MoldCheckStatus.Warn)]
    [InlineData("open", MoldCheckStatus.Fail)]
    [InlineData("two pieces", MoldCheckStatus.Fail)]
    public void HalvesMesh_SaysWhetherEachHalfIsAClosedSurfaceInOnePiece(string moving, MoldCheckStatus expected)
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var solid = BoxMesh(-40, -40, 0, 60, 50, 45);
            var triangles = moving switch
            {
                "pinched" => BoxMesh(-40, -40, 0, 10, 5, 45).Concat(BoxMesh(10, 5, 0, 60, 50, 45)).ToArray(),
                "open" => solid.Skip(1).ToArray(),
                "two pieces" => BoxMesh(-40, -40, 0, 0, 50, 45).Concat(BoxMesh(10, -40, 0, 60, 50, 45)).ToArray(),
                _ => solid
            };
            var movingPath = Path.Combine(directory, "moving.stl");
            var fixedPath = Path.Combine(directory, "fixed.stl");
            BinaryStlTriangleWriter.Write(movingPath, triangles);
            BinaryStlTriangleWriter.Write(fixedPath, BoxMesh(-40, -40, -40, 60, 50, 0));

            var report = MoldVerifier.Verify(Input(box, PartingAxis.Z, 0, Halves(0, 0, movingPath, fixedPath)) with { Layout = null });

            Assert.Equal(expected, Status(report, "halves-mesh"));

            if (moving == "two pieces")
            {
                // The second piece is named with its volume and where it is.
                Assert.Contains("(other pieces: a solid of 162000 mm3 at X -40..0, Y -40..50, Z 0..45)", Detail(report, "halves-mesh"), StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The closed surface of an axis-aligned box, outward triangles.</summary>
    internal static MeshTriangle[] BoxMesh(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        var t = new List<MeshTriangle>();
        Quad(t, new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), new(x1, y0, z0), 0, 0, -1);
        Quad(t, new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), 0, 0, 1);
        Quad(t, new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1), 0, -1, 0);
        Quad(t, new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), 0, 1, 0);
        Quad(t, new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0), -1, 0, 0);
        Quad(t, new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), new(x1, y0, z1), 1, 0, 0);
        return t.ToArray();
    }

    /// <summary>
    /// A box with a square tunnel along X, y in [ty0, ty1] and z in [tz0, tz1]: one closed surface of genus one.
    /// </summary>
    internal static MeshTriangle[] BoxWithTunnelAlongX(double x0, double y0, double z0, double x1, double y1, double z1, double ty0, double tz0, double ty1, double tz1)
    {
        var t = new List<MeshTriangle>();
        MeshVertex[] Outer(double x) => new MeshVertex[] { new(x, y0, z0), new(x, y1, z0), new(x, y1, z1), new(x, y0, z1) };
        MeshVertex[] Inner(double x) => new MeshVertex[] { new(x, ty0, tz0), new(x, ty1, tz0), new(x, ty1, tz1), new(x, ty0, tz1) };

        foreach (var (x, nx) in new[] { (x0, -1.0), (x1, 1.0) })
        {
            var o = Outer(x);
            var i = Inner(x);

            for (var k = 0; k < 4; k++)
            {
                Quad(t, o[k], o[(k + 1) % 4], i[(k + 1) % 4], i[k], nx, 0, 0);
            }
        }

        Quad(t, new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), new(x1, y0, z0), 0, 0, -1);
        Quad(t, new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), 0, 0, 1);
        Quad(t, new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1), 0, -1, 0);
        Quad(t, new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), 0, 1, 0);

        // The tunnel walls face into the tunnel (out of the steel).
        Quad(t, new(x0, ty0, tz0), new(x1, ty0, tz0), new(x1, ty1, tz0), new(x0, ty1, tz0), 0, 0, 1);
        Quad(t, new(x0, ty0, tz1), new(x0, ty1, tz1), new(x1, ty1, tz1), new(x1, ty0, tz1), 0, 0, -1);
        Quad(t, new(x0, ty0, tz0), new(x0, ty0, tz1), new(x1, ty0, tz1), new(x1, ty0, tz0), 1, 0, 0);
        Quad(t, new(x0, ty1, tz0), new(x1, ty1, tz0), new(x1, ty1, tz1), new(x0, ty1, tz1), -1, 0, 0);
        return t.ToArray();
    }

    /// <summary>Two triangles of the quad a-b-c-d, wound so their normal points along (nx, ny, nz).</summary>
    private static void Quad(List<MeshTriangle> t, MeshVertex a, MeshVertex b, MeshVertex c, MeshVertex d, double nx, double ny, double nz)
    {
        var ux = b.X - a.X;
        var uy = b.Y - a.Y;
        var uz = b.Z - a.Z;
        var vx = c.X - a.X;
        var vy = c.Y - a.Y;
        var vz = c.Z - a.Z;
        var dot = (((uy * vz) - (uz * vy)) * nx) + (((uz * vx) - (ux * vz)) * ny) + (((ux * vy) - (uy * vx)) * nz);

        if (dot >= 0)
        {
            t.Add(new MeshTriangle(a, b, c));
            t.Add(new MeshTriangle(a, c, d));
        }
        else
        {
            t.Add(new MeshTriangle(a, c, b));
            t.Add(new MeshTriangle(a, d, c));
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
