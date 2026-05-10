using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Parting;
using PicoMoldForge.PicoGK.BooleanGeometry;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoBooleanCavityCoreSplitterTests
{
    [Fact]
    public void Generate_WithBinaryBoxStl_WritesCoreAndCavitySideStls()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-boolean-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "part-box-binary.stl");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(sourcePath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);

        try
        {
            var splitter = new PicoBooleanCavityCoreSplitter();

            var request = new BooleanCavityCoreSplitRequest(
                SourcePath: sourcePath,
                OutputDirectory: outputDirectory,
                BlockBounds: new MoldBlockBounds(
                    MinXmm: -5,
                    MinYmm: -5,
                    MinZmm: -5,
                    MaxXmm: 25,
                    MaxYmm: 15,
                    MaxZmm: 10),
                SplitAxis: PartingAxis.X,
                PartingPlaneOffsetMm: 10,
                VoxelSizeMm: 1.0f);

            var result = splitter.Generate(request);

            Assert.True(result.IsSuccessful);
            Assert.Equal(PartingAxis.X, result.SplitAxis);
            Assert.Equal(10, result.PartingPlaneOffsetMm);

            Assert.Equal(MoldHalfKind.CoreSide, result.CoreSide.Kind);
            Assert.Equal(MoldHalfKind.CavitySide, result.CavitySide.Kind);

            Assert.True(File.Exists(result.CoreSide.OutputPath));
            Assert.True(File.Exists(result.CavitySide.OutputPath));

            Assert.EndsWith("BooleanCoreSide.stl", result.CoreSide.OutputPath, StringComparison.Ordinal);
            Assert.EndsWith("BooleanCavitySide.stl", result.CavitySide.OutputPath, StringComparison.Ordinal);

            Assert.True(result.CoreSide.OutputFileSizeBytes > 84);
            Assert.True(result.CavitySide.OutputFileSizeBytes > 84);

            Assert.True(result.CoreSide.RemainingVolumeCubicMm > 0);
            Assert.True(result.CavitySide.RemainingVolumeCubicMm > 0);

            Assert.True(result.CoreSide.DiagnosticTriangleCount > 0);
            Assert.True(result.CavitySide.DiagnosticTriangleCount > 0);

            Assert.Contains(result.Warnings, warning => warning.Contains("voxel boolean subtraction", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Generate_WithInvalidPartingOffset_Throws()
    {
        var splitter = new PicoBooleanCavityCoreSplitter();

        var request = new BooleanCavityCoreSplitRequest(
            SourcePath: "part.stl",
            OutputDirectory: "output",
            BlockBounds: new MoldBlockBounds(-5, -5, -5, 25, 15, 10),
            SplitAxis: PartingAxis.X,
            PartingPlaneOffsetMm: 25,
            VoxelSizeMm: 1.0f);

        var exception = Assert.Throws<ArgumentException>(() =>
            splitter.Generate(request));

        Assert.Contains("Invalid boolean cavity/core split request", exception.Message);
    }

    [Fact]
    public void Generate_WithMissingSource_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-boolean-split-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var splitter = new PicoBooleanCavityCoreSplitter();

            var request = new BooleanCavityCoreSplitRequest(
                SourcePath: Path.Combine(tempDir, "missing.stl"),
                OutputDirectory: Path.Combine(tempDir, "output"),
                BlockBounds: new MoldBlockBounds(-5, -5, -5, 25, 15, 10),
                SplitAxis: PartingAxis.X,
                PartingPlaneOffsetMm: 10,
                VoxelSizeMm: 1.0f);

            var exception = Assert.Throws<FileNotFoundException>(() =>
                splitter.Generate(request));

            Assert.Contains("Source STL file was not found", exception.Message);
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
        var headerText = "PicoMoldForge boolean split binary box STL test"u8.ToArray();
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