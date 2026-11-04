using PicoMoldForge.Core.Engineering.Verification;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.Verification;

public sealed class MeshIntegrityTests
{
    [Fact]
    public void Box_IsClosedManifoldAndOnePiece()
    {
        var result = MeshIntegrity.Measure(MoldVerifierTests.BoxMesh(0, 0, 0, 10, 20, 30));

        Assert.Equal(12, result.Triangles);
        Assert.Equal(0, result.OpenEdges);
        Assert.Equal(0, result.NonManifoldEdges);
        Assert.Equal(1, result.Pieces);
    }

    [Fact]
    public void BoxWithATunnel_IsClosedManifoldAndOnePiece()
    {
        var result = MeshIntegrity.Measure(MoldVerifierTests.BoxWithTunnelAlongX(0, 0, 0, 30, 20, 20, 5, 5, 15, 15));

        Assert.Equal(0, result.OpenEdges);
        Assert.Equal(0, result.NonManifoldEdges);
        Assert.Equal(1, result.Pieces);
    }

    [Fact]
    public void MissingTriangle_LeavesThreeOpenEdges()
    {
        var result = MeshIntegrity.Measure(MoldVerifierTests.BoxMesh(0, 0, 0, 10, 20, 30).Skip(1).ToArray());

        Assert.Equal(3, result.OpenEdges);
        Assert.Equal(1, result.Pieces);
    }

    [Fact]
    public void BoxesTouchingAlongAnEdge_AreOnePieceWithANonManifoldEdge()
    {
        // The edge x = 10, y = 5 belongs to two faces of each box: four triangles.
        var triangles = MoldVerifierTests.BoxMesh(0, 0, 0, 10, 5, 8).Concat(MoldVerifierTests.BoxMesh(10, 5, 0, 20, 10, 8)).ToArray();

        var result = MeshIntegrity.Measure(triangles);

        Assert.Equal(1, result.NonManifoldEdges);
        Assert.Equal(0, result.OpenEdges);
        Assert.Equal(1, result.Pieces);
        var near = Assert.Single(result.NonManifoldNear);
        Assert.Equal(10, near.X, 9);
        Assert.Equal(5, near.Y, 9);
    }

    [Fact]
    public void SeparateBoxes_AreTwoPieces()
    {
        var triangles = MoldVerifierTests.BoxMesh(0, 0, 0, 10, 5, 8).Concat(MoldVerifierTests.BoxMesh(20, 0, 0, 30, 5, 8)).ToArray();

        Assert.Equal(2, MeshIntegrity.Measure(triangles).Pieces);
    }

    [Fact]
    public void Pieces_ComeLargestFirst_WithTheirVolumeAndBox()
    {
        var triangles = MoldVerifierTests.BoxMesh(20, 0, 0, 22, 1, 1).Concat(MoldVerifierTests.BoxMesh(0, 0, 0, 10, 5, 8)).ToArray();

        var pieces = MeshIntegrity.Measure(triangles).PieceList;

        Assert.Equal(2, pieces.Count);
        Assert.Equal(400, pieces[0].VolumeMm3, 9);
        Assert.Equal(2, pieces[1].VolumeMm3, 9);
        Assert.Equal(20, pieces[1].Min.X, 9);
        Assert.Equal(22, pieces[1].Max.X, 9);
    }

    [Theory]
    [InlineData(0.2, 1, 0.008)]
    [InlineData(1.5, 0, 0.0)]
    public void Islands_SmallerThanTheLimitAreLeftOut(double side, int removed, double removedVolume)
    {
        // A 0.2 mm cube (0.008 mm3) next to the half is voxel debris under a 1 mm3 limit; a 1.5 mm cube (3.375 mm3)
        // is a real piece and stays, for the verification to see.
        var half = MoldVerifierTests.BoxMesh(0, 0, 0, 10, 5, 8);
        var triangles = half.Concat(MoldVerifierTests.BoxMesh(20, 0, 0, 20 + side, side, side)).ToArray();

        var (kept, count, volume) = MeshIslands.Remove(triangles, 1.0);

        Assert.Equal(removed, count);
        Assert.Equal(removedVolume, volume, 9);
        Assert.Equal(triangles.Length - (removed * 12), kept.Count);
        Assert.Equal(1 + (1 - removed), MeshIntegrity.Measure(kept).Pieces);
    }

    [Fact]
    public void Islands_NeverTheLargestPiece()
    {
        // Only one piece, smaller than the limit: it is the solid, not an island.
        var triangles = MoldVerifierTests.BoxMesh(0, 0, 0, 0.5, 0.5, 0.5);

        var (kept, count, _) = MeshIslands.Remove(triangles, 1.0);

        Assert.Equal(0, count);
        Assert.Equal(12, kept.Count);
    }

    [Fact]
    public void NoTriangles_IsNoPiece()
    {
        var result = MeshIntegrity.Measure(Array.Empty<MeshTriangle>());

        Assert.Equal(0, result.Pieces);
        Assert.Equal(0, result.Triangles);
    }
}
