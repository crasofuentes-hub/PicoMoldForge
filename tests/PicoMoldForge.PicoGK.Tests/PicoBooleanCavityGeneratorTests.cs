using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.PicoGK.BooleanGeometry;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoBooleanCavityGeneratorTests
{
    [Fact]
    public void Generate_WithBinaryBoxStl_SubtractsPartFromMoldBlockAndWritesCavity()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-boolean-cavity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "part-box-binary.stl");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(sourcePath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);

        try
        {
            var generator = new PicoBooleanCavityGenerator();

            var request = new BooleanCavityGenerationRequest(
                SourcePath: sourcePath,
                OutputDirectory: outputDirectory,
                BlockBounds: new MoldBlockBounds(
                    MinXmm: -5,
                    MinYmm: -5,
                    MinZmm: -5,
                    MaxXmm: 25,
                    MaxYmm: 15,
                    MaxZmm: 10),
                VoxelSizeMm: 1.0f);

            var result = generator.Generate(request);

            Assert.True(result.IsSuccessful);
            Assert.True(File.Exists(result.OutputPath));
            Assert.EndsWith("BooleanCavity.stl", result.OutputPath, StringComparison.Ordinal);
            Assert.True(result.OutputFileSizeBytes > 84);
            Assert.True(result.BlockVolumeCubicMm > 0);
            Assert.True(result.CavityVolumeCubicMm > 0);
            Assert.True(result.DiagnosticTriangleCount > 0);
            Assert.True(result.DiagnosticVertexCount > 0);
            Assert.Contains(result.Warnings, warning => warning.Contains("boolean subtraction", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Generate_WithMissingSource_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-boolean-cavity-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var generator = new PicoBooleanCavityGenerator();

            var request = new BooleanCavityGenerationRequest(
                SourcePath: Path.Combine(tempDir, "missing.stl"),
                OutputDirectory: Path.Combine(tempDir, "output"),
                BlockBounds: new MoldBlockBounds(-5, -5, -5, 25, 15, 10),
                VoxelSizeMm: 1.0f);

            var exception = Assert.Throws<FileNotFoundException>(() =>
                generator.Generate(request));

            Assert.Contains("Source STL file was not found", exception.Message);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Generate_WithInvalidBlock_Throws()
    {
        var generator = new PicoBooleanCavityGenerator();

        var request = new BooleanCavityGenerationRequest(
            SourcePath: "part.stl",
            OutputDirectory: "output",
            BlockBounds: new MoldBlockBounds(0, 0, 0, 0, 10, 10),
            VoxelSizeMm: 1.0f);

        var exception = Assert.Throws<ArgumentException>(() =>
            generator.Generate(request));

        Assert.Contains("Invalid boolean cavity generation request", exception.Message);
    }

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge boolean cavity binary box STL test"u8.ToArray();
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