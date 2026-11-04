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
    public void NoTriangles_IsNoPiece()
    {
        var result = MeshIntegrity.Measure(Array.Empty<MeshTriangle>());

        Assert.Equal(0, result.Pieces);
        Assert.Equal(0, result.Triangles);
    }
}
