using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.CavityCore;
using PicoMoldForge.Core.Parting;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class CavityCoreContractTests
{
    [Fact]
    public void ShrinkageCompensator_CalculateUniformScale_ReturnsOnePlusShrinkageRate()
    {
        var compensator = new ShrinkageCompensator();

        var result = compensator.CalculateUniformScale(0.012m);

        Assert.Equal(0.012m, result.ShrinkageRate);
        Assert.Equal(1.012m, result.ScaleFactor);
    }

    [Fact]
    public void ShrinkageCompensator_ApplyToDimension_ReturnsScaledDimension()
    {
        var compensator = new ShrinkageCompensator();

        var result = compensator.ApplyToDimension(100.0m, 0.012m);

        Assert.Equal(101.2m, result);
    }

    [Fact]
    public void ShrinkageCompensator_WithNegativeShrinkage_Throws()
    {
        var compensator = new ShrinkageCompensator();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            compensator.CalculateUniformScale(-0.001m));

        Assert.Contains("Shrinkage rate cannot be negative", exception.Message);
    }

    [Fact]
    public void CavityCoreGenerationRequest_WithValidValues_ReturnsNoValidationErrors()
    {
        var request = CreateValidRequest();

        var errors = request.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void CavityCoreGenerationRequest_WithoutPartingPlane_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            PartingPlane = null
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("PartingPlane is required.", StringComparison.Ordinal));
    }

    [Fact]
    public void CavityCorePreliminaryPlanner_WithValidRequest_ReturnsPlannedArtifacts()
    {
        var planner = new CavityCorePreliminaryPlanner();
        var request = CreateValidRequest();

        var result = planner.Plan(request);

        Assert.True(result.IsSuccessful);
        Assert.Equal(1.011m, result.ShrinkageScaleFactor);
        Assert.Equal(2, result.Artifacts.Count);
        Assert.Contains(result.Artifacts, artifact => artifact.Kind == CavityCoreArtifactKind.Cavity && artifact.Path.EndsWith("Cavity.stl", StringComparison.Ordinal));
        Assert.Contains(result.Artifacts, artifact => artifact.Kind == CavityCoreArtifactKind.Core && artifact.Path.EndsWith("Core.stl", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("contracts only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CavityCorePreliminaryPlanner_WithInvalidRequest_Throws()
    {
        var planner = new CavityCorePreliminaryPlanner();
        var request = CreateValidRequest() with
        {
            SourcePath = string.Empty
        };

        var exception = Assert.Throws<ArgumentException>(() => planner.Plan(request));

        Assert.Contains("Invalid cavity/core generation request", exception.Message);
    }

    private static CavityCoreGenerationRequest CreateValidRequest()
    {
        var partingPlane = new PartingPlaneResult(
            PartingPlaneMode.Automatic,
            PartingAxis.X,
            new OpeningDirection3(1.0f, 0.0f, 0.0f),
            PlaneOffsetMm: 50.0f,
            Method: "Dominant bounding-box axis with center-plane placement.",
            Warnings: new[] { "Preliminary parting plane." });

        return new CavityCoreGenerationRequest(
            SourcePath: "part.stl",
            OutputDirectory: "output",
            ShrinkageRate: 0.011m,
            PartingPlane: partingPlane);
    }
}