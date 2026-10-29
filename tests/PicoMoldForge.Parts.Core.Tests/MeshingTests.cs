using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Meshing;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Core.Tests;

public sealed class MarchingCubesTableTests
{
    [Fact]
    public void EveryCase_UsesEachCrossingEdgeExactlyOnce()
    {
        for (var cubeCase = 0; cubeCase < 256; cubeCase++)
        {
            var used = MarchingCubesTables.Loops(cubeCase).SelectMany(loop => loop).ToList();
            var crossing = Enumerable.Range(0, 12)
                .Where(e => (((cubeCase >> MarchingCubesTables.EdgeA[e]) & 1) == 1) != (((cubeCase >> MarchingCubesTables.EdgeB[e]) & 1) == 1))
                .ToList();

            Assert.Equal(crossing.OrderBy(e => e), used.OrderBy(e => e));
            Assert.All(MarchingCubesTables.Loops(cubeCase), loop => Assert.True(loop.Count >= 3));
        }
    }

    [Fact]
    public void RandomSignGrid_StitchesIntoAClosedOrientedSurface()
    {
        const int n = 9;
        var random = new Random(1234);
        var inside = new bool[n, n, n];

        for (var i = 1; i < n - 1; i++)
        {
            for (var j = 1; j < n - 1; j++)
            {
                for (var k = 1; k < n - 1; k++)
                {
                    inside[i, j, k] = random.NextDouble() < 0.45;
                }
            }
        }

        var directed = new Dictionary<(long, long), int>();
        var signedVolume = 0.0;

        for (var i = 0; i < n - 1; i++)
        {
            for (var j = 0; j < n - 1; j++)
            {
                for (var k = 0; k < n - 1; k++)
                {
                    var cubeCase = 0;

                    for (var c = 0; c < 8; c++)
                    {
                        if (inside[i + MarchingCubesTables.CornerX[c], j + MarchingCubesTables.CornerY[c], k + MarchingCubesTables.CornerZ[c]])
                        {
                            cubeCase |= 1 << c;
                        }
                    }

                    for (var t = MarchingCubesTables.CaseOffsets[cubeCase]; t < MarchingCubesTables.CaseOffsets[cubeCase + 1]; t += 3)
                    {
                        var keys = new long[3];
                        var points = new Vec3[3];

                        for (var v = 0; v < 3; v++)
                        {
                            var edge = MarchingCubesTables.TriangleEdges[t + v];
                            var a = MarchingCubesTables.EdgeA[edge];
                            var gx = i + MarchingCubesTables.CornerX[a];
                            var gy = j + MarchingCubesTables.CornerY[a];
                            var gz = k + MarchingCubesTables.CornerZ[a];
                            var direction = MarchingCubesTables.EdgeDirection[edge];
                            keys[v] = ((((long)gz * n) + gy) * n + gx) * 3 + direction;
                            points[v] = new Vec3(gx + (direction == 0 ? 0.5 : 0), gy + (direction == 1 ? 0.5 : 0), gz + (direction == 2 ? 0.5 : 0));
                        }

                        for (var v = 0; v < 3; v++)
                        {
                            var key = (keys[v], keys[(v + 1) % 3]);
                            directed[key] = directed.GetValueOrDefault(key) + 1;
                        }

                        signedVolume += Vec3.Dot(points[0], Vec3.Cross(points[1], points[2])) / 6;
                    }
                }
            }
        }

        Assert.NotEmpty(directed);
        Assert.All(directed, pair =>
        {
            Assert.Equal(1, pair.Value);
            Assert.Equal(1, directed.GetValueOrDefault((pair.Key.Item2, pair.Key.Item1)));
        });
        Assert.True(signedVolume > 0, "Normals must point outside (positive enclosed volume).");
    }
}

public sealed class SdfMesherTests
{
    [Fact]
    public void Sphere_IsWatertightGenusZero_WithTheRightVolume()
    {
        var sphere = new SphereSdf(new Vec3(1, 2, 3), 10);
        var mesh = SdfMesher.Mesh(sphere, new MeshingOptions(0.5));
        var report = MeshAnalysis.Analyze(mesh);
        var exact = 4.0 / 3.0 * Math.PI * 1000;

        Assert.True(report.Watertight);
        Assert.Equal(1, report.Components);
        Assert.Equal(0, report.Genus);
        Assert.InRange(report.VolumeMm3 / exact, 0.99, 1.01);
    }

    [Fact]
    public void Torus_FromARotarySweep_HasGenusOne()
    {
        var torus = new RotarySweepSdf(new CircleProfile(new Vec2(12, 0), 4), RotaryAxis.C, 0, 2 * Math.PI);
        var report = MeshAnalysis.Analyze(SdfMesher.Mesh(torus, new MeshingOptions(0.4)));
        var exact = 2 * Math.PI * Math.PI * 12 * 16;

        Assert.True(report.Watertight);
        Assert.Equal(1, report.Genus);
        Assert.InRange(report.VolumeMm3 / exact, 0.99, 1.01);
    }

    [Fact]
    public void BoxWithThroughHole_HasGenusOne_AndExactBounds()
    {
        var part = new CsgSdf(new (CsgOperation, ISdf)[]
        {
            (CsgOperation.Add, new LinearSweepSdf(new RectangleProfile(new Vec2(-10, -6), new Vec2(10, 6)), LinearAxis.Z, 0, 5, 0)),
            (CsgOperation.Subtract, new LinearSweepSdf(new CircleProfile(Vec2.Zero, 3), LinearAxis.Z, 6, -1, 0))
        });
        var report = MeshAnalysis.Analyze(SdfMesher.Mesh(part, new MeshingOptions(0.25)));
        var exact = (20 * 12 * 5) - (Math.PI * 9 * 5);

        Assert.True(report.Watertight);
        Assert.Equal(1, report.Genus);
        Assert.InRange(report.VolumeMm3 / exact, 0.99, 1.01);
        Assert.InRange(report.BoundsMin.X, -10.25, -9.75);
        Assert.InRange(report.BoundsMax.Z, 4.75, 5.25);
    }

    [Fact]
    public void Output_IsDeterministic_RegardlessOfThreads()
    {
        var torus = new RotarySweepSdf(new CircleProfile(new Vec2(12, 0), 4), RotaryAxis.A, 0, 2 * Math.PI);
        var serial = SdfMesher.Mesh(torus, new MeshingOptions(0.6, MaxDegreeOfParallelism: 1));
        var parallel = SdfMesher.Mesh(torus, new MeshingOptions(0.6, MaxDegreeOfParallelism: 4));

        Assert.Equal(serial.Vertices, parallel.Vertices);
        Assert.Equal(serial.Indices, parallel.Indices);
    }

    [Fact]
    public void VoxelVolume_AgreesWithTheAnalyticVolume()
    {
        var volume = VoxelVolumeEstimator.EstimateVolume(new SphereSdf(Vec3.Zero, 10), 0.25);

        Assert.InRange(volume / (4.0 / 3.0 * Math.PI * 1000), 0.995, 1.005);
    }
}

public sealed class SurfaceDistanceProbeTests
{
    [Theory]
    [InlineData(0.3)]
    [InlineData(-0.7)]
    [InlineData(0.0)]
    public void UpperBound_OnASphere_IsTheExactDistance(double offset)
    {
        var sphere = new SphereSdf(Vec3.Zero, 10);
        var direction = new Vec3(1, 2, 2).Normalized();
        var point = direction * (10 + offset);

        var distance = SurfaceDistanceProbe.UpperBound(sphere, point, direction, 2.0);

        Assert.InRange(distance, Math.Abs(offset) - 1e-9, Math.Abs(offset) + 1e-6);
    }

    [Fact]
    public void UpperBound_IsInfinite_WhenNoSurfaceIsWithinTheLimit()
    {
        var sphere = new SphereSdf(Vec3.Zero, 10);

        Assert.True(double.IsPositiveInfinity(SurfaceDistanceProbe.UpperBound(sphere, new Vec3(0, 0, 15), Vec3.UnitZ, 2.0)));
    }

    [Fact]
    public void UpperBound_DoesNotTrustAnUnderestimatingField()
    {
        // The same sphere with values scaled down 3x still has the exact sign: the probe measures geometry.
        var scaled = new ScaledSdf(new SphereSdf(Vec3.Zero, 10), 1.0 / 3.0);
        var point = new Vec3(0, 0, 10.6);

        Assert.InRange(scaled.Distance(point), 0.19, 0.21);
        Assert.InRange(SurfaceDistanceProbe.UpperBound(scaled, point, Vec3.UnitZ, 2.0), 0.6 - 1e-9, 0.6 + 1e-6);
    }

    [Fact]
    public void Measure_SphereMesh_IsWithinTheChordError()
    {
        var sphere = new SphereSdf(Vec3.Zero, 10);
        var mesh = SdfMesher.Mesh(sphere, new MeshingOptions(0.5));

        var report = SurfaceDistanceProbe.Measure(mesh, sphere, 1.0, maxSamples: 2000);

        Assert.Equal(0, report.Unresolved);
        Assert.InRange(report.VertexSamples, 1000, 2000);
        Assert.InRange(report.TriangleSamples, 1000, 2000);
        Assert.InRange(report.VertexMaxMm, 0, 0.01);
        Assert.InRange(report.TriangleMaxMm, 0.0001, 0.05);
    }

    private sealed class ScaledSdf : ISdf
    {
        private readonly ISdf _inner;
        private readonly double _scale;

        public ScaledSdf(ISdf inner, double scale)
        {
            _inner = inner;
            _scale = scale;
        }

        public Box3 Bounds => _inner.Bounds;

        public double Lipschitz => 1.0;

        public double Distance(Vec3 p) => _inner.Distance(p) * _scale;
    }
}

public sealed class QemDecimatorTests
{
    [Fact]
    public void Box_SimplifiesHeavily_AndStaysOnTheSurface()
    {
        var box = new LinearSweepSdf(new RectangleProfile(new Vec2(-10, -6), new Vec2(10, 6)), LinearAxis.Z, 0, 5, 0);
        var raw = SdfMesher.Mesh(box, new MeshingOptions(0.5));
        var simplified = QemDecimator.Decimate(raw, box, new DecimationOptions(0.01), out var stats);
        var before = MeshAnalysis.Analyze(raw);
        var after = MeshAnalysis.Analyze(simplified);

        Assert.True(after.Watertight);
        Assert.Equal(before.Genus, after.Genus);
        // Faces collapse; the chamfer strips marching cubes leaves along the 12 sharp edges stay (their
        // centroids are off the true surface), which bounds the reduction for a small box.
        Assert.True(simplified.TriangleCount * 4 < raw.TriangleCount, $"{raw.TriangleCount} -> {simplified.TriangleCount}");
        Assert.Equal(simplified.TriangleCount, stats.TrianglesAfter);
        Assert.InRange(Math.Abs(after.VolumeMm3 - before.VolumeMm3) / before.VolumeMm3, 0, 0.005);
        Assert.All(simplified.Vertices, v => Assert.True(Math.Abs(box.Distance(v)) <= 0.01 + 1e-9, $"vertex {v} is off the surface"));
    }

    [Fact]
    public void Torus_KeepsItsTopology()
    {
        var torus = new RotarySweepSdf(new CircleProfile(new Vec2(12, 0), 4), RotaryAxis.C, 0, 2 * Math.PI);
        var raw = SdfMesher.Mesh(torus, new MeshingOptions(0.5));
        var simplified = QemDecimator.Decimate(raw, torus, new DecimationOptions(0.02), out _);
        var report = MeshAnalysis.Analyze(simplified);

        Assert.True(report.Watertight);
        Assert.Equal(1, report.Genus);
        Assert.True(simplified.TriangleCount < raw.TriangleCount);
        Assert.All(simplified.Vertices, v => Assert.True(Math.Abs(torus.Distance(v)) <= 0.02 + 1e-9));
    }
}
