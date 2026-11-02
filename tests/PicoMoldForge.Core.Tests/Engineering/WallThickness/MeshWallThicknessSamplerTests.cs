using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.WallThickness;

public sealed class MeshWallThicknessSamplerTests
{
    // The 20 x 10 x 5 mm box of GeneratorEndToEndPipelineTests, triangle for triangle.
    internal static IReadOnlyList<MeshTriangle> GeneratorTestBox(double x = 20, double y = 10, double z = 5) => new[]
    {
        T(0, 0, 0, x, y, 0, x, 0, 0), T(0, 0, 0, 0, y, 0, x, y, 0),
        T(0, 0, z, x, 0, z, x, y, z), T(0, 0, z, x, y, z, 0, y, z),
        T(0, 0, 0, x, 0, 0, x, 0, z), T(0, 0, 0, x, 0, z, 0, 0, z),
        T(0, y, 0, x, y, z, x, y, 0), T(0, y, 0, 0, y, z, x, y, z),
        T(0, 0, 0, 0, 0, z, 0, y, z), T(0, 0, 0, 0, y, z, 0, y, 0),
        T(x, 0, 0, x, y, 0, x, y, z), T(x, 0, 0, x, y, z, x, 0, z)
    };

    [Fact]
    public void Box_TopAndBottomAreWallsOfTheBoxHeight_SidesAreEdges()
    {
        var result = MeshWallThicknessSampler.Sample(GeneratorTestBox());

        Assert.False(result.OrientationFlipped);
        Assert.Equal(4, result.WallSampleCount);
        Assert.All(result.Measurements.Take(4), m =>
        {
            Assert.True(m.IsWall);
            Assert.InRange(m.ThicknessMm, 4.999, 5.001);
        });
        Assert.All(result.Measurements.Skip(4), m => Assert.False(m.IsWall));
        Assert.Equal(400.0, result.WallAreaMm2, 6);
    }

    [Fact]
    public void TProfile_RibIsMeasuredAtItsOwnThickness_AndGroupedAsThin()
    {
        var mesh = TProfile();

        var result = MeshWallThicknessSampler.Sample(mesh);
        var nominal = WallThicknessRegionBuilder.NominalThickness(result.Measurements);
        var regions = WallThicknessRegionBuilder.Build(mesh, result.Measurements, new WallThicknessBands(1.2, 3 * nominal, nominal, 0.5)).Regions;

        Assert.InRange(nominal, 3.999, 4.001);
        var thin = regions.Where(r => r.Band == WallThicknessBand.Thin).ToArray();
        Assert.Equal(2, thin.Length);
        Assert.All(thin, r =>
        {
            Assert.InRange(r.ThicknessMm, 0.999, 1.001);
            Assert.Equal(600.0, r.AreaMm2, 6);
            Assert.InRange(r.Centroid.Z, 4, 14);
        });
        Assert.Equal(new[] { "wall-thin-001", "wall-thin-002" }, thin.Select(r => r.RegionId));
        Assert.All(regions.Where(r => r.Band == WallThicknessBand.Nominal), r => Assert.InRange(r.ThicknessMm, 3.999, 4.001));
        Assert.Equal(3, regions.Count(r => r.Band == WallThicknessBand.Nominal));
    }

    [Fact]
    public void Wedge_MatchesTheLocalThickness()
    {
        // Thickness 4 mm at x = 0 down to 1 mm at x = 50 (3.4 degree taper), 40 mm deep.
        var mesh = Extrude(new[] { (0.0, 0.0), (50.0, 0.0), (50.0, 1.0), (0.0, 4.0) }, 40, new[] { (0, 1, 2), (0, 2, 3) });

        var result = MeshWallThicknessSampler.Sample(mesh);

        var bottom = result.Measurements.Where(m => Normal(mesh[m.TriangleIndex]).Z < -0.99).ToArray();
        Assert.NotEmpty(bottom);
        Assert.All(bottom, m =>
        {
            var x = Centroid(mesh[m.TriangleIndex]).X;
            var local = 4 - (0.06 * x);
            Assert.True(m.IsWall);
            Assert.InRange(m.ThicknessMm / local, 0.99, 1.0);
        });
    }

    [Fact]
    public void InsideOutMesh_IsMeasuredTheSameWay()
    {
        var inverted = GeneratorTestBox().Select(t => new MeshTriangle(t.A, t.C, t.B)).ToArray();

        var result = MeshWallThicknessSampler.Sample(inverted);

        Assert.True(result.OrientationFlipped);
        Assert.Equal(4, result.WallSampleCount);
        Assert.All(result.Measurements.Where(m => m.IsWall), m => Assert.InRange(m.ThicknessMm, 4.999, 5.001));
    }

    [Fact]
    public void Results_DoNotDependOnThreads()
    {
        var mesh = TProfile();

        var serial = MeshWallThicknessSampler.Sample(mesh, new MeshWallThicknessOptions(MaxDegreeOfParallelism: 1));
        var parallel = MeshWallThicknessSampler.Sample(mesh, new MeshWallThicknessOptions(MaxDegreeOfParallelism: 4));

        Assert.Equal(serial.Measurements, parallel.Measurements);
    }

    [Fact]
    public void BinaryStl_RoundTrips_AndATruncatedFileIsRejected()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(new byte[80]);
            var box = GeneratorTestBox();
            writer.Write((uint)box.Count);

            foreach (var t in box)
            {
                writer.Write(new byte[12]);

                foreach (var v in new[] { t.A, t.B, t.C })
                {
                    writer.Write((float)v.X);
                    writer.Write((float)v.Y);
                    writer.Write((float)v.Z);
                }

                writer.Write((ushort)0);
            }
        }

        stream.Position = 0;
        Assert.Equal(GeneratorTestBox(), BinaryStlTriangleReader.Read(stream));

        using var truncated = new MemoryStream(stream.ToArray()[..^7]);
        Assert.Throws<InvalidDataException>(() => BinaryStlTriangleReader.Read(truncated));
    }

    // R3': regions smaller than the thin limit squared are left out and counted. A 0.3 mm thick speck of
    // 0.49 mm2 per face stands in for the slivers found at sharp edges of real meshes.
    [Fact]
    public void SmallRegions_AreLeftOutAndCounted_TheRibIsKept()
    {
        var mesh = TProfile().Concat(Box(10, 10, 20, 0.7, 0.7, 0.3)).ToArray();
        var result = MeshWallThicknessSampler.Sample(mesh);
        var nominal = WallThicknessRegionBuilder.NominalThickness(result.Measurements);

        var set = WallThicknessRegionBuilder.Build(mesh, result.Measurements, new WallThicknessBands(1.2, 3 * nominal, nominal, 0.5));

        Assert.Equal(1.44, set.MinimumRegionAreaMm2, 9);
        Assert.Equal(2, set.ExcludedRegionCount);
        Assert.Equal(0.98, set.ExcludedAreaMm2, 6);
        var thin = set.Regions.Where(r => r.Band == WallThicknessBand.Thin).ToArray();
        Assert.Equal(2, thin.Length);
        Assert.All(thin, r => Assert.InRange(r.ThicknessMm, 0.999, 1.001));
    }

    [Fact]
    public void Analysis_OfTheGeneratorTestBox_ReportsTwoFiveMillimetreWalls()
    {
        var result = MeshWallThicknessAnalysis.Analyze(GeneratorTestBox(), minimumThicknessMm: 1.2m, abruptChangeRatio: 0.50m);
        var summary = result.Analysis.Summary;

        Assert.Equal(2, summary.SampleCount);
        Assert.Equal(2, summary.NominalCount);
        Assert.Equal(5.0m, summary.MinimumObservedThicknessMm);
        Assert.Equal(5.0m, summary.MaximumObservedThicknessMm);
        Assert.Equal(0m, summary.RiskySurfaceAreaMm2);
        Assert.Equal(400m, summary.SampledSurfaceAreaMm2);
        Assert.Contains(result.Notes, n => n.Contains("nominal wall 5.000 mm", StringComparison.Ordinal));
    }

    [Fact]
    public void Analysis_FlagsTheRibAsThin_AndSaysWhere()
    {
        var result = MeshWallThicknessAnalysis.Analyze(TProfile(), minimumThicknessMm: 1.2m, abruptChangeRatio: 0.50m);
        var summary = result.Analysis.Summary;

        Assert.Equal(2, summary.ThinCount);
        Assert.Equal(1.0m, summary.MinimumObservedThicknessMm);
        Assert.Equal(1200m, summary.RiskySurfaceAreaMm2);
        Assert.Equal(new[] { "wall-thin-001", "wall-thin-002" }, result.ReportedRegions.Take(2).Select(r => r.RegionId));
        Assert.Contains(result.Notes, n => n.StartsWith("Thinnest wall region wall-thin-", StringComparison.Ordinal) && n.Contains("1.000 mm over 600.0 mm2", StringComparison.Ordinal));
    }

    [Fact]
    public void Analysis_NamesWallsThinnerThanATenthAsProbableSlivers()
    {
        // A 0.05 mm thick plate of 2 x 2 mm: 4 mm2 per face, above the 1.44 mm2 region limit.
        var mesh = TProfile().Concat(Box(10, 10, 20, 2, 2, 0.05)).ToArray();

        var result = MeshWallThicknessAnalysis.Analyze(mesh, minimumThicknessMm: 1.2m, abruptChangeRatio: 0.50m);

        Assert.Equal(4, result.Analysis.Summary.ThinCount);
        Assert.Contains(result.Notes, n => n.StartsWith("2 wall regions (8.0 mm2) are thinner than 0.1 mm: probably folded slivers", StringComparison.Ordinal));
    }

    [Fact]
    public void Analysis_WithoutWalls_ReportsNoSamplesAndSaysWhy()
    {
        var single = new[] { T(0, 0, 0, 10, 0, 0, 0, 10, 0) };

        var result = MeshWallThicknessAnalysis.Analyze(single, minimumThicknessMm: 1.2m, abruptChangeRatio: 0.50m);

        Assert.Equal(0, result.Analysis.Summary.SampleCount);
        Assert.Contains(result.Notes, n => n.StartsWith("Wall thickness could not be measured", StringComparison.Ordinal));
    }

    [Fact]
    public void Bands_FollowTheAnalyzerOrder()
    {
        var bands = new WallThicknessBands(MinimumMm: 1.2, MaximumMm: 12, NominalMm: 4, AbruptChangeRatio: 0.5);

        Assert.Equal(WallThicknessBand.Thin, bands.Classify(1.0));
        Assert.Equal(WallThicknessBand.AbruptLow, bands.Classify(1.5));
        Assert.Equal(WallThicknessBand.Nominal, bands.Classify(4.0));
        Assert.Equal(WallThicknessBand.AbruptHigh, bands.Classify(7.0));
        Assert.Equal(WallThicknessBand.Thick, bands.Classify(13.0));
    }

    /// <summary>A 100 mm wide, 4 mm plate with a 1 mm wide, 10 mm tall rib in the middle, extruded 60 mm along Y.</summary>
    private static IReadOnlyList<MeshTriangle> TProfile() => Extrude(
        new[] { (0.0, 0.0), (100.0, 0.0), (100.0, 4.0), (50.5, 4.0), (50.5, 14.0), (49.5, 14.0), (49.5, 4.0), (0.0, 4.0) },
        60,
        new[] { (0, 1, 2), (0, 2, 3), (0, 3, 6), (0, 6, 7), (6, 3, 4), (6, 4, 5) });

    /// <summary>
    /// Closed prism: a counterclockwise polygon in the (x, z) plane extruded from y = 0 to y = depth. The cap
    /// triangles index the polygon counterclockwise; side quads are split into two triangles.
    /// </summary>
    private static IReadOnlyList<MeshTriangle> Extrude(IReadOnlyList<(double X, double Z)> polygon, double depth, IReadOnlyList<(int, int, int)> cap)
    {
        MeshVertex V((double X, double Z) p, double y) => new(p.X, y, p.Z);
        var triangles = new List<MeshTriangle>();

        foreach (var (i, j, k) in cap)
        {
            triangles.Add(new MeshTriangle(V(polygon[i], 0), V(polygon[j], 0), V(polygon[k], 0)));
            triangles.Add(new MeshTriangle(V(polygon[i], depth), V(polygon[k], depth), V(polygon[j], depth)));
        }

        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            triangles.Add(new MeshTriangle(V(a, 0), V(b, depth), V(b, 0)));
            triangles.Add(new MeshTriangle(V(a, 0), V(a, depth), V(b, depth)));
        }

        return triangles;
    }

    /// <summary>Closed axis-aligned box with its minimum corner at (x, y, z), outward winding.</summary>
    private static IEnumerable<MeshTriangle> Box(double x, double y, double z, double sx, double sy, double sz)
    {
        MeshVertex Move(MeshVertex v) => new(v.X + x, v.Y + y, v.Z + z);
        return GeneratorTestBox(sx, sy, sz).Select(t => new MeshTriangle(Move(t.A), Move(t.B), Move(t.C)));
    }

    private static MeshTriangle T(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz) =>
        new(new MeshVertex(ax, ay, az), new MeshVertex(bx, by, bz), new MeshVertex(cx, cy, cz));

    private static (double X, double Y, double Z) Centroid(MeshTriangle t) =>
        ((t.A.X + t.B.X + t.C.X) / 3, (t.A.Y + t.B.Y + t.C.Y) / 3, (t.A.Z + t.B.Z + t.C.Z) / 3);

    private static (double X, double Y, double Z) Normal(MeshTriangle t)
    {
        var (ux, uy, uz) = (t.B.X - t.A.X, t.B.Y - t.A.Y, t.B.Z - t.A.Z);
        var (vx, vy, vz) = (t.C.X - t.A.X, t.C.Y - t.A.Y, t.C.Z - t.A.Z);
        var (nx, ny, nz) = ((uy * vz) - (uz * vy), (uz * vx) - (ux * vz), (ux * vy) - (uy * vx));
        var length = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
        return (nx / length, ny / length, nz / length);
    }
}
