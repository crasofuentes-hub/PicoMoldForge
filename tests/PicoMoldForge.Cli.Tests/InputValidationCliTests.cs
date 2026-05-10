using System;
using System.IO;
using PicoMoldForge.Cli;
using Xunit;

namespace PicoMoldForge.Cli.Tests;

public sealed class InputValidationCliTests
{
    [Fact]
    public void Run_WithValidConfigAndExistingStl_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-cli-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        File.WriteAllText(stlPath, "solid test" + Environment.NewLine + "endsolid test");

        File.WriteAllText(configPath, $$"""
        {
          "projectName": "CLI Input Test",
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

    [Fact]
    public void Run_WithMissingStl_ReturnsFailure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-cli-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var missingStlPath = Path.Combine(tempDir, "missing.stl");
        var configPath = Path.Combine(tempDir, "project.json");

        File.WriteAllText(configPath, $$"""
        {
          "projectName": "CLI Missing Input Test",
          "inputPath": "{{missingStlPath.Replace("\\", "\\\\")}}",
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

            Assert.Equal(1, exitCode);
            Assert.Contains("Input validation: FAIL", output.ToString());
            Assert.Contains("STL input file was not found", output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}