using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class GeneratorMoldBlockConfigTests
{
    [Fact]
    public void Run_WithGenerateAllAndMissingMoldBlock_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-missing-moldblock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        WriteBinaryTriangleStl(stlPath);
        WriteProjectConfigWithoutMoldBlock(configPath, "part-binary.stl", "output");

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = GeneratorCommandLineApplication.Run(
                new[] { "--config", configPath, "--generate-all" },
                output,
                error);

            Assert.Equal(1, exitCode);
            Assert.Contains("Generation pipeline: FAIL", error.ToString());
            Assert.Contains("moldBlock is required", error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithGenerateAllAndInvalidMoldBlock_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-invalid-moldblock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        WriteBinaryTriangleStl(stlPath);
        WriteProjectConfigWithInvalidMoldBlock(configPath, "part-binary.stl", "output");

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = GeneratorCommandLineApplication.Run(
                new[] { "--config", configPath, "--generate-all" },
                output,
                error);

            Assert.Equal(1, exitCode);
            Assert.Contains("Generation pipeline: FAIL", error.ToString());
            Assert.Contains("moldBlock validation failed", error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteProjectConfigWithoutMoldBlock(string path, string inputPath, string outputDirectory)
    {
        File.WriteAllText(path, $$"""
        {
          "projectName": "Missing MoldBlock Test",
          "inputPath": "{{inputPath}}",
          "outputDirectory": "{{outputDirectory}}",
          "mode": "Prototype",
          "standard": "Custom",
          "voxelResolutionMm": 1.0,
          "material": {
            "name": "H13 Tool Steel",
            "shrinkageRate": 0.011
          },
          "machine": {
            "name": "Generic LPBF Machine",
            "buildVolumeXmm": 250,
            "buildVolumeYmm": 250,
            "buildVolumeZmm": 300
          }
        }
        """);
    }

    private static void WriteProjectConfigWithInvalidMoldBlock(string path, string inputPath, string outputDirectory)
    {
        File.WriteAllText(path, $$"""
        {
          "projectName": "Invalid MoldBlock Test",
          "inputPath": "{{inputPath}}",
          "outputDirectory": "{{outputDirectory}}",
          "mode": "Prototype",
          "standard": "Custom",
          "voxelResolutionMm": 1.0,
          "material": {
            "name": "H13 Tool Steel",
            "shrinkageRate": 0.011
          },
          "machine": {
            "name": "Generic LPBF Machine",
            "buildVolumeXmm": 250,
            "buildVolumeYmm": 250,
            "buildVolumeZmm": 300
          },
          "moldBlock": {
            "minXmm": 10,
            "minYmm": -25,
            "minZmm": -25,
            "maxXmm": 10,
            "maxYmm": 85,
            "maxZmm": 55
          },
          "cooling": {
            "partSizeXmm": 100,
            "partSizeYmm": 60,
            "partSizeZmm": 30,
            "channelDiameterMm": 6,
            "channelSpacingMm": 15,
            "minimumClearanceMm": 10,
            "channelCount": 3
          },
          "lattice": {
            "regionName": "default-lattice-region",
            "minXmm": 0,
            "minYmm": 0,
            "minZmm": 0,
            "maxXmm": 20,
            "maxYmm": 10,
            "maxZmm": 10,
            "cellSizeMm": 10,
            "beamRadiusMm": 1,
            "targetRelativeDensity": 0.2
          },
          "moldSystem": {
            "partSizeXmm": 100,
            "partSizeYmm": 60,
            "partSizeZmm": 30,
            "moldMarginMm": 20,
            "ejectorPinDiameterMm": 4,
            "ejectorPinCount": 4,
            "ventWidthMm": 0.5,
            "ventDepthMm": 0.1,
            "insertClearanceMm": 2
          },
          "dfam": {
            "minimumWallThicknessMm": 1.5,
            "recommendedMinimumWallThicknessMm": 1.2,
            "usesPreliminaryGeometry": true
          }
        }
        """);
    }

    private static void WriteBinaryTriangleStl(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge mold block config binary STL test"u8.ToArray();
        Array.Copy(headerText, header, headerText.Length);
        writer.Write(header);

        writer.Write((uint)1);

        WriteTriangle(
            writer,
            nx: 0,
            ny: 0,
            nz: 1,
            ax: 0,
            ay: 0,
            az: 0,
            bx: 10,
            by: 0,
            bz: 0,
            cx: 0,
            cy: 10,
            cz: 0);
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