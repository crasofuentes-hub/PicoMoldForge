using PicoMoldForge.PicoGK.Analysis;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoPartAnalyzerTests
{
    [Fact]
    public void AnalyzeBinaryStl_WithBinaryCubeStl_ReturnsPreliminaryReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-part-analysis-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "cube-binary.stl");

        WriteBinaryCubeStl(stlPath);

        try
        {
            var analyzer = new PicoPartAnalyzer();

            var report = analyzer.AnalyzeBinaryStl(stlPath, voxelSizeMm: 1.0f);

            Assert.Equal(Path.GetFullPath(stlPath), report.SourcePath);
            Assert.True(report.TriangleCount > 0);
            Assert.True(report.VertexCount > 0);
            Assert.False(string.IsNullOrWhiteSpace(report.MeshBoundingBox));
            Assert.Equal(1.0f, report.VoxelSizeMm);
            Assert.True(report.VoxelizedVolumeCubicMm > 0);
            Assert.True(report.VoxelSliceCount > 0);
            Assert.True(report.VoxelMemoryUsageBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(report.VoxelBoundingBox));
            Assert.Contains(report.Warnings, warning => warning.Code == "PRELIMINARY_ANALYSIS");
            Assert.Contains(report.Warnings, warning => warning.Code == "BINARY_STL_REQUIRED");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteBinaryCubeStl(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge binary cube STL analysis test"u8.ToArray();
        Array.Copy(headerText, header, headerText.Length);
        writer.Write(header);

        writer.Write((uint)12);

        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, 10, 10, 0, 10, 0, 0);
        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, 0, 10, 0, 10, 10, 0);

        WriteTriangle(writer, 0, 0, 1, 0, 0, 10, 10, 0, 10, 10, 10, 10);
        WriteTriangle(writer, 0, 0, 1, 0, 0, 10, 10, 10, 10, 0, 10, 10);

        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, 10, 0, 0, 10, 0, 10);
        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, 10, 0, 10, 0, 0, 10);

        WriteTriangle(writer, 0, 1, 0, 0, 10, 0, 10, 10, 10, 10, 10, 0);
        WriteTriangle(writer, 0, 1, 0, 0, 10, 0, 0, 10, 10, 10, 10, 10);

        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, 0, 10, 0, 10, 10);
        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, 10, 10, 0, 10, 0);

        WriteTriangle(writer, 1, 0, 0, 10, 0, 0, 10, 10, 0, 10, 10, 10);
        WriteTriangle(writer, 1, 0, 0, 10, 0, 0, 10, 10, 10, 10, 0, 10);
    }

    private static void WriteTriangle(
        BinaryWriter writer,
        float nx,
        float ny,
        float nz,
        float ax,
        float ay,
        float az,
        float bx,
        float by,
        float bz,
        float cx,
        float cy,
        float cz)
    {
        writer.Write(nx);
        writer.Write(ny);
        writer.Write(nz);

        writer.Write(ax);
        writer.Write(ay);
        writer.Write(az);

        writer.Write(bx);
        writer.Write(by);
        writer.Write(bz);

        writer.Write(cx);
        writer.Write(cy);
        writer.Write(cz);

        writer.Write((ushort)0);
    }
}