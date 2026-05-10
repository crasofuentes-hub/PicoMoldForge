using System;
using System.IO;
using PicoMoldForge.Core.Domain;
using PicoMoldForge.Core.Input;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class PartInputLoaderTests
{
    [Fact]
    public void Load_WithExistingStl_ReturnsSuccess()
    {
        var path = Path.Combine(Path.GetTempPath(), $"picomoldforge-part-{Guid.NewGuid():N}.stl");
        File.WriteAllText(path, "solid sample" + Environment.NewLine + "endsolid sample");

        try
        {
            var config = CreateValidConfig(path);
            var loader = new PartInputLoader();

            var result = loader.Load(config);

            Assert.True(result.IsSuccessful);
            Assert.Equal(PartInputFormat.Stl, result.Format);
            Assert.Equal(Path.GetFullPath(path), result.ResolvedPath);
            Assert.Null(result.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithStepInputAndNoConverter_ReturnsClearFailure()
    {
        var config = CreateValidConfig("part.step") with
        {
            StepConverterPath = null
        };

        var loader = new PartInputLoader();

        var result = loader.Load(config);

        Assert.False(result.IsSuccessful);
        Assert.Equal(PartInputFormat.Step, result.Format);
        Assert.Contains("STEP input requires a configured external converter", result.Error);
    }

    [Fact]
    public void Load_WithUnsupportedExtension_ReturnsFailure()
    {
        var config = CreateValidConfig("part.obj");
        var loader = new PartInputLoader();

        var result = loader.Load(config);

        Assert.False(result.IsSuccessful);
        Assert.Equal(PartInputFormat.Unknown, result.Format);
        Assert.Contains("Unsupported part input format", result.Error);
    }

    [Fact]
    public void DetectFormat_WithStpExtension_ReturnsStep()
    {
        var format = PartInputLoader.DetectFormat("part.stp");

        Assert.Equal(PartInputFormat.Step, format);
    }

    private static MoldProjectConfig CreateValidConfig(string inputPath)
    {
        return new MoldProjectConfig
        {
            ProjectName = "Input Loader Test",
            InputPath = inputPath,
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