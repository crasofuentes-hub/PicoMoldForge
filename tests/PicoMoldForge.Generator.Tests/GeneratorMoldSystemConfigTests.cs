using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class GeneratorMoldSystemConfigTests
{
    [Fact]
    public void Run_WithMissingMoldSystem_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-missing-moldsystem-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        WriteBinaryTriangleStl(stlPath);
        WriteConfig(configPath, "part-binary.stl", "output", moldSystemJson: null);

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
            Assert.Contains("moldSystem is required", error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithInvalidMoldSystem_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-invalid-moldsystem-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        WriteBinaryTriangleStl(stlPath);
        WriteConfig(configPath, "part-binary.stl", "output", """
          "moldSystem": {
            "partSizeXmm": 100,
            "partSizeYmm": 60,
            "partSizeZmm": 30,
            "moldMarginMm": 20,
            "ejectorPinDiameterMm": 0,
            "ejectorPinCount": 4,
            "ventWidthMm": 0.5,
            "ventDepthMm": 0.1,
            "insertClearanceMm": 2
          }
        """);

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
            Assert.Contains("moldSystem validation failed", error.ToString());
            Assert.Contains("moldSystem.ejectorPinDiameterMm must be greater than zero", error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithValidMoldSystem_GeneratesMoldSystemDiagnostic()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-valid-moldsystem-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(stlPath, sizeX: 20, sizeY: 10, sizeZ: 5);
        WriteConfig(configPath, "part-binary.stl", "output", """
          "moldSystem": {
            "partSizeXmm": 80,
            "partSizeYmm": 40,
            "partSizeZmm": 20,
            "moldMarginMm": 15,
            "ejectorPinDiameterMm": 3,
            "ejectorPinCount": 2,
            "ventWidthMm": 0.4,
            "ventDepthMm": 0.08,
            "insertClearanceMm": 1.5
          }
        """);

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = GeneratorCommandLineApplication.Run(
                new[] { "--config", configPath, "--generate-all" },
                output,
                error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.True(File.Exists(Path.Combine(outputDirectory, "MoldSystemDiagnostic.stl")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteConfig(
        string path,
        string inputPath,
        string outputDirectory,
        string? moldSystemJson)
    {
        var moldSystemSegment = moldSystemJson is null
            ? string.Empty
            : "," + Environment.NewLine + moldSystemJson;

        File.WriteAllText(path, $$"""
        {
          "projectName": "MoldSystem Config Test",
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
            "minXmm": -25,
            "minYmm": -25,
            "minZmm": -25,
            "maxXmm": 125,
            "maxYmm": 85,
            "maxZmm": 55
          },
          "parting": {
            "mode": "Manual",
            "axis": "X",
            "offsetMm": 10.0
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
          }{{moldSystemSegment}}
        }
        """);
    }

    private static void WriteBinaryTriangleStl(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge mold system config binary STL test"u8.ToArray();
        Array.Copy(headerText, header, headerText.Length);
        writer.Write(header);

        writer.Write((uint)1);

        WriteTriangle(writer, 0, 0, 1, 0, 0, 0, 10, 0, 0, 0, 10, 0);
    }

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge mold system config binary box STL test"u8.ToArray();
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