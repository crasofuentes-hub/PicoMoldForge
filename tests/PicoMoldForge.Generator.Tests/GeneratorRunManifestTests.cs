using System.Text.Json;
using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class GeneratorRunManifestTests
{
    [Fact]
    public void Run_WithGenerateAll_WritesRunManifest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-run-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(stlPath, sizeX: 20, sizeY: 10, sizeZ: 5);
        WriteConfig(configPath, "part-binary.stl", "output");

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = GeneratorCommandLineApplication.Run(
                new[] { "--config", configPath, "--generate-all", "--clean-output" },
                output,
                error);

            var manifestPath = Path.Combine(outputDirectory, "RunManifest.json");

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("Run manifest:", output.ToString());
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            Assert.Equal("picomoldforge.run-manifest.v1", root.GetProperty("SchemaVersion").GetString());
            Assert.Equal("Run Manifest Test", root.GetProperty("ProjectName").GetString());
            Assert.True(root.GetProperty("CleanOutput").GetBoolean());
            Assert.False(root.GetProperty("UsedOutputOverride").GetBoolean());
            Assert.True(root.GetProperty("Artifacts").GetArrayLength() >= 10);
            Assert.True(root.GetProperty("Warnings").GetArrayLength() > 0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithOutputOverride_RecordsOutputOverrideInRunManifest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-run-manifest-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");
        var overrideOutputDirectory = Path.Combine(tempDir, "override-output");

        WriteBinaryBoxStl(stlPath, sizeX: 20, sizeY: 10, sizeZ: 5);
        WriteConfig(configPath, "part-binary.stl", "configured-output");

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = GeneratorCommandLineApplication.Run(
                new[] { "--config", configPath, "--generate-all", "--clean-output", "--output", overrideOutputDirectory },
                output,
                error);

            var manifestPath = Path.Combine(overrideOutputDirectory, "RunManifest.json");

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            Assert.True(root.GetProperty("CleanOutput").GetBoolean());
            Assert.True(root.GetProperty("UsedOutputOverride").GetBoolean());
            Assert.Equal(Path.GetFullPath(overrideOutputDirectory), root.GetProperty("OutputOverridePath").GetString());
            Assert.Equal(Path.GetFullPath(overrideOutputDirectory), root.GetProperty("OutputDirectory").GetString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithHelp_MentionsRunManifest()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = GeneratorCommandLineApplication.Run(new[] { "--help" }, output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("RunManifest.json", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    private static void WriteConfig(
        string path,
        string inputPath,
        string outputDirectory)
    {
        File.WriteAllText(path, $$"""
        {
          "projectName": "Run Manifest Test",
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

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge run manifest binary box STL test"u8.ToArray();
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