using System;
using System.IO;
using PicoMoldForge.Cli;
using Xunit;

namespace PicoMoldForge.Cli.Tests;

public sealed class ConfigValidationCliTests
{
    [Fact]
    public void Run_WithValidConfigAndExistingStl_ReturnsZeroAndPrintsPass()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-cli-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "sample.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        File.WriteAllText(stlPath, "solid sample" + Environment.NewLine + "endsolid sample");

        File.WriteAllText(configPath, $$"""
        {
          "projectName": "CLI Config Test",
          "inputPath": "{{stlPath.Replace("\\", "\\\\")}}",
          "outputDirectory": "output",
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

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = CommandLineApplication.Run(new[] { "--config", configPath, "--validate-config" }, output, error);

            Assert.Equal(0, exitCode);
            Assert.Contains("Config validation: PASS", output.ToString());
            Assert.Contains("Input validation: PASS", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}