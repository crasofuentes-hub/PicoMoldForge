using PicoMoldForge.Parts.Export;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Meshing;

namespace PicoMoldForge.Parts.Core.Tests;

public sealed class StlReaderTests
{
    [Fact]
    public void RoundTrip_WeldsVerticesAndKeepsTheTopology()
    {
        var mesh = SdfMesher.Mesh(new SphereSdf(Vec3.Zero, 5), new MeshingOptions(0.5));
        using var stream = new MemoryStream();
        StlWriter.Write(mesh, stream, "round trip");
        stream.Position = 0;

        var read = StlReader.ReadBinary(stream);
        var report = MeshAnalysis.Analyze(read.Mesh);

        Assert.Equal(0, read.DroppedDegenerateTriangles);
        Assert.Equal(mesh.TriangleCount, read.Mesh.TriangleCount);
        Assert.True(report.Watertight);
        Assert.Equal(0, report.Genus);
        Assert.InRange(report.VolumeMm3, MeshAnalysis.Analyze(mesh).VolumeMm3 * 0.9999, MeshAnalysis.Analyze(mesh).VolumeMm3 * 1.0001);
    }

    [Fact]
    public void ZeroAreaTriangles_AreDropped()
    {
        // A tetrahedron (closed, outward) plus one triangle that repeats a vertex.
        var a = new Vec3(0, 0, 0);
        var b = new Vec3(10, 0, 0);
        var c = new Vec3(0, 10, 0);
        var d = new Vec3(0, 0, 10);
        var mesh = new TriangleMesh(new[] { a, b, c, d }, new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 0, 3, 2, 1, 1, 3 });
        using var stream = new MemoryStream();
        StlWriter.Write(mesh, stream, "tetra");
        stream.Position = 0;

        var read = StlReader.ReadBinary(stream);
        var report = MeshAnalysis.Analyze(read.Mesh);

        Assert.Equal(1, read.DroppedDegenerateTriangles);
        Assert.Equal(4, read.Mesh.TriangleCount);
        Assert.True(report.Watertight);
        Assert.Equal(0, report.Genus);
    }

    [Fact]
    public void TruncatedFile_IsRejected()
    {
        var mesh = SdfMesher.Mesh(new SphereSdf(Vec3.Zero, 5), new MeshingOptions(1.0));
        using var stream = new MemoryStream();
        StlWriter.Write(mesh, stream, "truncated");
        using var truncated = new MemoryStream(stream.ToArray()[..^10]);

        Assert.Throws<InvalidDataException>(() => StlReader.ReadBinary(truncated));
    }
}
