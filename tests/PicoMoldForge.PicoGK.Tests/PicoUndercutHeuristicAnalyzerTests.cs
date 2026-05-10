using PicoMoldForge.Core.Analysis;
using PicoMoldForge.PicoGK.Analysis;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoUndercutHeuristicAnalyzerTests
{
    [Fact]
    public void AnalyzeBinaryStl_WithCubeAndPositiveZOpening_ReturnsReportableRisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-undercut-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "cube-binary.stl");

        WriteBinaryCubeStl(stlPath);

        try
        {
            var analyzer = new PicoUndercutHeuristicAnalyzer();

            var result = analyzer.AnalyzeBinaryStl(
                stlPath,
                OpeningDirection3.PositiveZ,
                voxelSizeMm: 1.0f);

            Assert.Equal(12, result.TotalTriangleCount);
            Assert.True(result.OpposingNormalTriangleCount > 0);
            Assert.True(result.OpposingNormalRatio > 0);
            Assert.True(result.HasPotentialUndercutRisk);
            Assert.Contains("preliminary", result.Limitation, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PicoPartAnalyzer_WithCube_AddsUndercutHeuristicWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-analysis-undercut-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "cube-binary.stl");

        WriteBinaryCubeStl(stlPath);

        try
        {
            var analyzer = new PicoPartAnalyzer();

            var report = analyzer.AnalyzeBinaryStl(
                stlPath,
                OpeningDirection3.PositiveZ,
                voxelSizeMm: 1.0f);

            Assert.Contains(report.Warnings, warning => warning.Code == "UNDERCUT_HEURISTIC_RISK");
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
        var headerText = "PicoMoldForge binary cube STL undercut test"u8.ToArray();
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