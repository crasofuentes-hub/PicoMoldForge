using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.CavityCore;
using PicoMoldForge.Core.Parting;
using PicoMoldForge.PicoGK.CavityCore;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoCavityCoreGeneratorTests
{
    [Fact]
    public void GeneratePreliminary_WithBinaryStl_WritesCavityAndCoreStl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-cavity-core-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "source-box-binary.stl");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(sourcePath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);

        try
        {
            var generator = new PicoCavityCoreGenerator();
            var request = new CavityCoreGenerationRequest(
                SourcePath: sourcePath,
                OutputDirectory: outputDirectory,
                ShrinkageRate: 0.011m,
                PartingPlane: CreatePartingPlane());

            var result = generator.GeneratePreliminary(request);

            var cavity = Assert.Single(result.Artifacts, artifact => artifact.Kind == CavityCoreArtifactKind.Cavity);
            var core = Assert.Single(result.Artifacts, artifact => artifact.Kind == CavityCoreArtifactKind.Core);

            Assert.True(result.IsSuccessful);
            Assert.Equal(1.011m, result.ShrinkageScaleFactor);
            Assert.True(File.Exists(cavity.Path));
            Assert.True(File.Exists(core.Path));
            Assert.True(new FileInfo(cavity.Path).Length > 84);
            Assert.True(new FileInfo(core.Path).Length > 84);
            Assert.Contains(result.Warnings, warning => warning.Contains("preliminary", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Warnings, warning => warning.Contains("not production mold geometry", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GeneratePreliminary_WithMissingSource_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-cavity-core-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var generator = new PicoCavityCoreGenerator();
            var request = new CavityCoreGenerationRequest(
                SourcePath: Path.Combine(tempDir, "missing.stl"),
                OutputDirectory: Path.Combine(tempDir, "output"),
                ShrinkageRate: 0.011m,
                PartingPlane: CreatePartingPlane());

            var exception = Assert.Throws<FileNotFoundException>(() =>
                generator.GeneratePreliminary(request));

            Assert.Contains("Source STL file was not found", exception.Message);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static PartingPlaneResult CreatePartingPlane()
    {
        return new PartingPlaneResult(
            PartingPlaneMode.Automatic,
            PartingAxis.X,
            new OpeningDirection3(1.0f, 0.0f, 0.0f),
            PlaneOffsetMm: 10.0f,
            Method: "Dominant bounding-box axis with center-plane placement.",
            Warnings: new[] { "Preliminary parting plane." });
    }

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge binary box STL cavity core test"u8.ToArray();
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