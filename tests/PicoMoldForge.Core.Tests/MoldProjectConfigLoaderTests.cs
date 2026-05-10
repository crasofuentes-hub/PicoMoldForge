using System;
using System.IO;
using PicoMoldForge.Core.Configuration;
using PicoMoldForge.Core.Domain;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class MoldProjectConfigLoaderTests
{
    [Fact]
    public void Validate_WithValidStlConfig_ReturnsNoErrors()
    {
        var config = CreateValidConfig();

        var errors = config.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithStepInputAndNoConverter_ReturnsClearError()
    {
        var config = CreateValidConfig() with
        {
            InputPath = "part.step",
            StepConverterPath = null
        };

        var errors = config.Validate();

        Assert.Contains(errors, error => error.Contains("STEP input requires StepConverterPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithInvalidVoxelResolution_ReturnsError()
    {
        var config = CreateValidConfig() with
        {
            VoxelResolutionMm = 0
        };

        var errors = config.Validate();

        Assert.Contains(errors, error => error.Contains("VoxelResolutionMm", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadFromFile_WithValidJson_LoadsConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"picomoldforge-config-{Guid.NewGuid():N}.json");

        File.WriteAllText(path, """
        {
          "projectName": "Json Config Test",
          "inputPath": "sample.stl",
          "outputDirectory": "output",
          "mode": "Prototype",
          "standard": "Custom",
          "voxelResolutionMm": 0.75,
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
            var config = MoldProjectConfigLoader.LoadFromFile(path);

            Assert.Equal("Json Config Test", config.ProjectName);
            Assert.Equal("sample.stl", config.InputPath);
            Assert.Empty(config.Validate());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MoldProjectConfig CreateValidConfig()
    {
        return new MoldProjectConfig
        {
            ProjectName = "Valid Mold Project",
            InputPath = "part.stl",
            OutputDirectory = "output",
            Mode = MoldMode.Prototype,
            Standard = MoldStandard.Custom,
            VoxelResolutionMm = 1.0m,
            Material = new MaterialProfile
            {
                Name = "H13 Tool Steel",
                ShrinkageRate = 0.011m
            },
            Machine = new MachineProfile
            {
                Name = "Generic LPBF Machine",
                BuildVolumeXmm = 250m,
                BuildVolumeYmm = 250m,
                BuildVolumeZmm = 300m
            }
        };
    }
}