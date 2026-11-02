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
        var regions = WallThicknessRegionBuilder.Build(mesh, result.Measurements, new WallThicknessBands(1.2, 3 * nominal, nominal, 0.5));

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
