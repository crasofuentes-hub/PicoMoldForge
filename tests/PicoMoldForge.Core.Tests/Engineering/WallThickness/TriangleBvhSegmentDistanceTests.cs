using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.WallThickness;

public sealed class TriangleBvhSegmentDistanceTests
{
    private static readonly MeshTriangle Flat = new(new MeshVertex(0, 0, 0), new MeshVertex(10, 0, 0), new MeshVertex(0, 10, 0));

    [Fact]
    public void SegmentThroughATriangle_IsAtDistanceZero()
    {
        var bvh = new TriangleBvh(new[] { Flat });

        Assert.Equal(0, bvh.SegmentDistance(new Vec3d(2, 2, -1), new Vec3d(2, 2, 1)));
    }

    [Fact]
    public void SegmentParallelOverATriangle_IsAtItsHeight()
    {
        var bvh = new TriangleBvh(new[] { Flat });

        Assert.Equal(2, bvh.SegmentDistance(new Vec3d(1, 1, 2), new Vec3d(3, 3, 2)), 12);
    }

    [Fact]
    public void SegmentLyingInThePlaneOfATriangle_AcrossIt_IsAtDistanceZero()
    {
        var bvh = new TriangleBvh(new[] { Flat });

        Assert.Equal(0, bvh.SegmentDistance(new Vec3d(-5, 2, 0), new Vec3d(20, 2, 0)), 12);
    }

    [Fact]
    public void SkewSegmentPastAnEdge_MatchesTheEdgeToEdgeDistance()
    {
        // The segment runs along Y at x = 6, z = 7 over the hypotenuse x + y = 10 of the triangle: the closest
        // points are (6, 4, 0) on the edge and (6, 4, 7) on the segment.
        var bvh = new TriangleBvh(new[] { Flat });

        Assert.Equal(7, bvh.SegmentDistance(new Vec3d(6, -20, 7), new Vec3d(6, 20, 7)), 9);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RandomSegments_AgreeWithDenseSamplingOfTheBox(int seed)
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        var bvh = new TriangleBvh(box);
        var random = new Random(seed);

        for (var n = 0; n < 40; n++)
        {
            var p = new Vec3d((random.NextDouble() * 60) - 20, (random.NextDouble() * 50) - 20, (random.NextDouble() * 45) - 20);
            var q = new Vec3d((random.NextDouble() * 60) - 20, (random.NextDouble() * 50) - 20, (random.NextDouble() * 45) - 20);
            var exact = bvh.SegmentDistance(p, q);
            var sampled = double.PositiveInfinity;

            for (var k = 0; k <= 4000; k++)
            {
                var s = p + ((q - p) * (k / 4000.0));

                foreach (var t in box)
                {
                    sampled = Math.Min(sampled, Math.Sqrt(TriangleBvh.PointTriangleDistanceSquared(s, Vec3d.From(t.A), Vec3d.From(t.B), Vec3d.From(t.C))));
                }
            }

            // Sampling can only miss the closest point, by at most half a sample step.
            var step = (q - p).Length / 4000;
            Assert.True(exact <= sampled + 1e-9, $"exact {exact} above sampled {sampled}");
            Assert.True(sampled - exact <= (step / 2) + 1e-9, $"exact {exact} too far below sampled {sampled}");
        }
    }

    [Fact]
    public void LongSegmentOverTheBox_IsQueriedInPiecesAndStaysExact()
    {
        var bvh = new TriangleBvh(MeshWallThicknessSamplerTests.GeneratorTestBox());

        // Along X at y = 5, 13 mm above the top face, far past both ends of the box.
        Assert.Equal(13, bvh.SegmentDistance(new Vec3d(-500, 5, 18), new Vec3d(500, 5, 18)), 9);

        // Along X at y = 20, z = 18: from the top edge (y = 10, z = 5).
        Assert.Equal(Math.Sqrt((10 * 10) + (13 * 13)), bvh.SegmentDistance(new Vec3d(-500, 20, 18), new Vec3d(500, 20, 18)), 9);
    }
}
