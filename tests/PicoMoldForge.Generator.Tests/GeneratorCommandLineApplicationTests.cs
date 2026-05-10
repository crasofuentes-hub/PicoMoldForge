using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class GeneratorCommandLineApplicationTests
{
    [Fact]
    public void Run_WithSelfTest_ReturnsZeroAndPrintsPass()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = GeneratorCommandLineApplication.Run(new[] { "--self-test" }, output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("PicoMoldForge Generator v5", output.ToString());
        Assert.Contains("Status: PASS", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_WithHelp_ReturnsZeroAndPrintsUsage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = GeneratorCommandLineApplication.Run(new[] { "--help" }, output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output.ToString());
        Assert.Contains("--config <path> --generate-all", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_WithGenerateAllAndValidBinaryStlConfig_ReturnsZeroAndGeneratesArtifacts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-intake-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(stlPath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);
        WriteProjectConfig(configPath, "part-binary.stl", "output");

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
            Assert.Contains("Generation input validation: PASS", output.ToString());
            Assert.Contains("Generation pipeline: PASS", output.ToString());
            Assert.Contains("Artifacts generated: 7", output.ToString());

            Assert.True(File.Exists(Path.Combine(outputDirectory, "DiagnosticMesh.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Cavity.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Core.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "CoolingDiagnostic.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "LatticeDiagnostic.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "MoldSystemDiagnostic.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "FinalProjectReport.json")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithGenerateAllAndAsciiStlConfig_ReturnsOne()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-ascii-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-ascii.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        File.WriteAllText(stlPath, """
        solid triangle
          facet normal 0 0 1
            outer loop
              vertex 0 0 0
              vertex 10 0 0
              vertex 0 10 0
            endloop
          endfacet
        endsolid triangle
        """);

        WriteProjectConfig(configPath, "part-ascii.stl", "output");

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
            Assert.Contains("requires binary STL input", error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Run_WithGenerateAllAndMissingConfig_ReturnsOne()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = GeneratorCommandLineApplication.Run(
            new[] { "--config", "missing-project.json", "--generate-all" },
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Contains("Generation pipeline: FAIL", error.ToString());
        Assert.Contains("Config file was not found", error.ToString());
    }

    [Fact]
    public void Run_WithGenerateAllWithoutConfig_ReturnsTwo()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = GeneratorCommandLineApplication.Run(
            new[] { "--generate-all" },
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Missing required option: --config <path>", error.ToString());
    }

    [Fact]
    public void Run_WithUnknownCommand_ReturnsTwo()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = GeneratorCommandLineApplication.Run(new[] { "--unknown" }, output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Unknown command", error.ToString());
    }

    private static void WriteProjectConfig(string path, string inputPath, string outputDirectory)
    {
        File.WriteAllText(path, $$"""
        {
          "projectName": "Generator Intake Test",
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

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge generator command binary box STL test"u8.ToArray();
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