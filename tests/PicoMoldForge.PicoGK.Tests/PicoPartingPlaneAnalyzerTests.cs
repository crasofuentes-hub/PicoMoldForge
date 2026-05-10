using PicoMoldForge.Core.Parting;
using PicoMoldForge.PicoGK.Parting;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoPartingPlaneAnalyzerTests
{
    [Fact]
    public void AnalyzeBinaryStl_WithXDominantBox_ReturnsXPartingPlane()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-parting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "box-x-dominant-binary.stl");

        WriteBinaryBoxStl(stlPath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);

        try
        {
            var analyzer = new PicoPartingPlaneAnalyzer();

            var result = analyzer.AnalyzeBinaryStl(stlPath, voxelSizeMm: 1.0f);

            Assert.Equal(PartingPlaneMode.Automatic, result.Mode);
            Assert.Equal(PartingAxis.X, result.Axis);
            Assert.Equal(10.0f, result.PlaneOffsetMm);
            Assert.Equal(1.0f, result.OpeningDirection.X);
            Assert.Equal(0.0f, result.OpeningDirection.Y);
            Assert.Equal(0.0f, result.OpeningDirection.Z);
            Assert.Contains("Dominant bounding-box axis", result.Method);
            Assert.NotEmpty(result.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AnalyzeBinaryStl_WithYDominantBox_ReturnsYPartingPlane()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-parting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "box-y-dominant-binary.stl");

        WriteBinaryBoxStl(stlPath, sizeX: 8.0f, sizeY: 30.0f, sizeZ: 6.0f);

        try
        {
            var analyzer = new PicoPartingPlaneAnalyzer();

            var result = analyzer.AnalyzeBinaryStl(stlPath, voxelSizeMm: 1.0f);

            Assert.Equal(PartingAxis.Y, result.Axis);
            Assert.Equal(15.0f, result.PlaneOffsetMm);
            Assert.Equal(0.0f, result.OpeningDirection.X);
            Assert.Equal(1.0f, result.OpeningDirection.Y);
            Assert.Equal(0.0f, result.OpeningDirection.Z);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge binary box STL parting test"u8.ToArray();
        Array.Copy(headerText, header, headerText.Length);
        writer.Write(header);

        writer.Write((uint)12);

        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, sizeX, sizeY, 0, sizeX, 0, 0);
        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, 0, sizeY, 0, sizeX, sizeY, 0);

        WriteTriangle(writer, 0, 0, 1, 0, 0, sizeZ, sizeX, 0, sizeZ, sizeX, sizeY, sizeZ);
        WriteTriangle(writer, 0, 0, 1, 0, 0, sizeZ, sizeX, sizeY, sizeZ, 0, sizeY, sizeZ);

        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, sizeX, 0, 0, sizeX, 0, sizeZ);
        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, sizeX, 0, sizeZ, 0, 0, sizeZ);

        WriteTriangle(writer, 0, 1, 0, 0, sizeY, 0, sizeX, sizeY, sizeZ, sizeX, sizeY, 0);
        WriteTriangle(writer, 0, 1, 0, 0, sizeY, 0, 0, sizeY, sizeZ, sizeX, sizeY, sizeZ);

        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, 0, sizeZ, 0, sizeY, sizeZ);
        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, sizeY, sizeZ, 0, sizeY, 0);

        WriteTriangle(writer, 1, 0, 0, sizeX, 0, 0, sizeX, sizeY, 0, sizeX, sizeY, sizeZ);
        WriteTriangle(writer, 1, 0, 0, sizeX, 0, 0, sizeX, sizeY, sizeZ, sizeX, 0, sizeZ);
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