using PicoMoldForge.PicoGK.Exports;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoDiagnosticMeshExporterTests
{
    [Fact]
    public void ExportVoxelizedDiagnosticMesh_WithBinaryCubeStl_WritesDiagnosticStl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-picogk-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "cube-binary.stl");
        var outputPath = Path.Combine(tempDir, "DiagnosticMesh.stl");

        WriteBinaryCubeStl(sourcePath);

        try
        {
            var exporter = new PicoDiagnosticMeshExporter();

            var result = exporter.ExportVoxelizedDiagnosticMesh(
                sourcePath,
                outputPath,
                voxelSizeMm: 1.0f);

            Assert.Equal(Path.GetFullPath(sourcePath), result.SourcePath);
            Assert.Equal(Path.GetFullPath(outputPath), result.OutputPath);
            Assert.True(File.Exists(outputPath));
            Assert.True(result.OutputFileSizeBytes > 84);
            Assert.True(result.DiagnosticTriangleCount > 0);
            Assert.True(result.DiagnosticVertexCount > 0);
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
        var headerText = "PicoMoldForge binary cube STL export test"u8.ToArray();
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