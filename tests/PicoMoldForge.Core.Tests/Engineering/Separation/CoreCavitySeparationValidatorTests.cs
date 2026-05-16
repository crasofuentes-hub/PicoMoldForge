using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Separation;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Separation;

public sealed class CoreCavitySeparationValidatorTests
{
    [Fact]
    public void Validate_WithHealthySeparation_ReturnsPass()
    {
        var validator = new CoreCavitySeparationValidator();

        var result = validator.Validate(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.Z,
            PartingOffsetMm: 0m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 980,
            OverlapVoxelCount: 0,
            GapVoxelCount: 5,
            HasCoreSideArtifact: true,
            HasCavitySideArtifact: true,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.False(result.HasFailures);
        Assert.Equal("CoreCavitySeparation", result.Category);
    }

    [Fact]
    public void Validate_WithMissingCoreArtifact_ReturnsFail()
    {
        var validator = new CoreCavitySeparationValidator();

        var result = validator.Validate(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.Z,
            PartingOffsetMm: 0m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 980,
            OverlapVoxelCount: 0,
            GapVoxelCount: 0,
            HasCoreSideArtifact: false,
            HasCavitySideArtifact: true,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.Contains(result.Issues, issue => issue.RuleId == "separation.core-artifact.missing");
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Validate_WithMissingShutoffStrategy_ReturnsWarningAndReview()
    {
        var validator = new CoreCavitySeparationValidator();

        var result = validator.Validate(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.X,
            PartingOffsetMm: 2m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 1000,
            OverlapVoxelCount: 0,
            GapVoxelCount: 0,
            HasCoreSideArtifact: true,
            HasCavitySideArtifact: true,
            HasPartingMetadata: true,
            HasShutoffStrategy: false));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.Equal("separation.shutoff-strategy.missing", issue.RuleId);
    }

    [Fact]
    public void Validate_WithOverlapAboveFailThreshold_ReturnsFail()
    {
        var validator = new CoreCavitySeparationValidator();

        var result = validator.Validate(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.Y,
            PartingOffsetMm: 0m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 1000,
            OverlapVoxelCount: 120,
            GapVoxelCount: 0,
            HasCoreSideArtifact: true,
            HasCavitySideArtifact: true,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.Contains(result.Issues, issue => issue.RuleId == "separation.overlap.fail");
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Validate_WithGapAboveFailThreshold_ReturnsFail()
    {
        var validator = new CoreCavitySeparationValidator();

        var result = validator.Validate(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.Y,
            PartingOffsetMm: 0m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 1000,
            OverlapVoxelCount: 0,
            GapVoxelCount: 120,
            HasCoreSideArtifact: true,
            HasCavitySideArtifact: true,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.Contains(result.Issues, issue => issue.RuleId == "separation.gap.fail");
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Validate_WithMissingPartingMetadata_ReturnsWarning()
    {
        var validator = new CoreCavitySeparationValidator();

        var result = validator.Validate(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.X,
            PartingOffsetMm: 1.5m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 1000,
            OverlapVoxelCount: 0,
            GapVoxelCount: 0,
            HasCoreSideArtifact: true,
            HasCavitySideArtifact: true,
            HasPartingMetadata: false,
            HasShutoffStrategy: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.True(issue.RequiresEngineerReview);
        Assert.Equal("mm", issue.Unit);
    }

    [Fact]
    public void Validate_WithHighlyImbalancedHalves_ReturnsWarning()
    {
        var validator = new CoreCavitySeparationValidator();

        var result = validator.Validate(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.Z,
            PartingOffsetMm: 0m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 100,
            OverlapVoxelCount: 0,
            GapVoxelCount: 0,
            HasCoreSideArtifact: true,
            HasCavitySideArtifact: true,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.Contains(result.Issues, issue => issue.RuleId == "separation.balance.warning");
        Assert.True(result.RequiresEngineerReview);
    }

    [Fact]
    public void Summarize_ComputesRatiosAndQualityScore()
    {
        var validator = new CoreCavitySeparationValidator();

        var summary = validator.Summarize(new CoreCavitySeparationInput(
            PartingAxis: PartingAxis.Z,
            PartingOffsetMm: 0m,
            CoreVoxelCount: 1000,
            CavityVoxelCount: 1000,
            OverlapVoxelCount: 20,
            GapVoxelCount: 10,
            HasCoreSideArtifact: true,
            HasCavitySideArtifact: true,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.Equal(2000, summary.TotalHalfVoxelCount);
        Assert.Equal(0.01m, summary.OverlapRatio);
        Assert.Equal(0.005m, summary.GapRatio);
        Assert.Equal(1.0m, summary.BalanceRatio);
        Assert.True(summary.QualityScore > 0.90m);
    }
}