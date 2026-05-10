using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class GeneratorPartingOverrideConfigTests
{
    [Fact]
    public void Run_WithManualPartingOverride_GeneratesSplitArtifacts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-manual-parting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(stlPath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);
        WriteProjectConfig(configPath, "part-binary.stl", "output", """
          "parting": {
            "mode": "Manual",
            "axis": "X",
            "offsetMm": 10.0
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
            Assert.Contains("Generation pipeline: PASS", output.ToString());
            Assert.True(File.Exists(Path.Combine(outputDirectory, "BooleanCoreSide.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "BooleanCavitySide.stl")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithInvalidManualPartingAxis_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-invalid-parting-axis-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        WriteBinaryBoxStl(stlPath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);
        WriteProjectConfig(configPath, "part-binary.stl", "output", """
          "parting": {
            "mode": "Manual",
            "axis": "Q",
            "offsetMm": 10.0
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
            Assert.Contains("parting.axis must be one of: X, Y, Z", error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithManualPartingOffsetOutsideMoldBlock_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-invalid-parting-offset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        WriteBinaryBoxStl(stlPath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);
        WriteProjectConfig(configPath, "part-binary.stl", "output", """
          "parting": {
            "mode": "Manual",
            "axis": "X",
            "offsetMm": 125.0
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
            Assert.Contains("parting.offsetMm must be inside moldBlock bounds", error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithAutoPartingConfig_UsesAutomaticPartingAndGeneratesSplitArtifacts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-auto-parting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(stlPath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);
        WriteProjectConfig(configPath, "part-binary.stl", "output", """
          "parting": {
            "mode": "Auto"
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
            Assert.True(File.Exists(Path.Combine(outputDirectory, "BooleanCoreSide.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "BooleanCavitySide.stl")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteProjectConfig(
        string path,
        string inputPath,
        string outputDirectory,
        string partingJson)
    {
        File.WriteAllText(path, $$"""
        {
          "projectName": "Parting Override Test",
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
        {{partingJson}}
        }
        """);
    }

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge manual parting binary box STL test"u8.ToArray();
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