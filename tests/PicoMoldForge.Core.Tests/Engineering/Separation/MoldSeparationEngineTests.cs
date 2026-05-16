using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Separation;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Separation;

public sealed class MoldSeparationEngineTests
{
    [Fact]
    public void Split_WithCenteredZPartingAndShutoffStrategy_ReturnsPassingSeparation()
    {
        var engine = new MoldSeparationEngine();

        var result = engine.Split(new MoldSeparationEngineInput(
            MoldBlockBounds: new MoldSeparationBounds(-50m, -50m, 0m, 50m, 50m, 100m),
            PartBounds: new MoldSeparationBounds(-20m, -20m, 20m, 20m, 20m, 80m),
            PartingAxis: PartingAxis.Z,
            PartingOffsetMm: 50m,
            VoxelResolutionMm: 1m,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.True(result.CoreVoxelCount > 0);
        Assert.True(result.CavityVoxelCount > 0);
        Assert.Equal(result.CoreVoxelCount, result.CavityVoxelCount);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.ValidationResult.Issues).Severity);
        Assert.False(result.ValidationResult.HasFailures);
    }

    [Fact]
    public void Split_WithMissingShutoffStrategy_ReturnsWarning()
    {
        var engine = new MoldSeparationEngine();

        var result = engine.Split(new MoldSeparationEngineInput(
            MoldBlockBounds: new MoldSeparationBounds(-50m, -50m, 0m, 50m, 50m, 100m),
            PartBounds: new MoldSeparationBounds(-20m, -20m, 20m, 20m, 20m, 80m),
            PartingAxis: PartingAxis.Z,
            PartingOffsetMm: 50m,
            VoxelResolutionMm: 1m,
            HasPartingMetadata: true,
            HasShutoffStrategy: false));

        var issue = Assert.Single(result.ValidationResult.Issues);

        Assert.Equal("separation.shutoff-strategy.missing", issue.RuleId);
        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.True(result.ValidationResult.RequiresEngineerReview);
    }

    [Fact]
    public void Split_WithPartingAtMoldMinimum_ReturnsInvalidCoreSide()
    {
        var engine = new MoldSeparationEngine();

        var result = engine.Split(new MoldSeparationEngineInput(
            MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
            PartingAxis: PartingAxis.Z,
            PartingOffsetMm: 0m,
            VoxelResolutionMm: 1m,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.Equal(0, result.CoreVoxelCount);
        Assert.Contains(result.ValidationResult.Issues, issue => issue.RuleId == "separation.core-artifact.missing");
        Assert.True(result.ValidationResult.HasFailures);
    }

    [Fact]
    public void Split_WithOffsetQuarterAlongX_ComputesExpectedVolumeRatio()
    {
        var engine = new MoldSeparationEngine();

        var result = engine.Split(new MoldSeparationEngineInput(
            MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
            PartingAxis: PartingAxis.X,
            PartingOffsetMm: 25m,
            VoxelResolutionMm: 1m,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        Assert.Equal(250000m, result.CoreApproxVolumeMm3);
        Assert.Equal(750000m, result.CavityApproxVolumeMm3);
        Assert.Equal(250000, result.CoreVoxelCount);
        Assert.Equal(750000, result.CavityVoxelCount);
    }

    [Fact]
    public void Split_WithInvalidVoxelResolution_Throws()
    {
        var engine = new MoldSeparationEngine();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.Split(new MoldSeparationEngineInput(
                MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
                PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
                PartingAxis: PartingAxis.Z,
                PartingOffsetMm: 50m,
                VoxelResolutionMm: 0m,
                HasPartingMetadata: true,
                HasShutoffStrategy: true)));
    }

    [Fact]
    public void Split_WithPartOutsideMoldBlock_Throws()
    {
        var engine = new MoldSeparationEngine();

        Assert.Throws<InvalidOperationException>(() =>
            engine.Split(new MoldSeparationEngineInput(
                MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
                PartBounds: new MoldSeparationBounds(-10m, 20m, 20m, 80m, 80m, 80m),
                PartingAxis: PartingAxis.Z,
                PartingOffsetMm: 50m,
                VoxelResolutionMm: 1m,
                HasPartingMetadata: true,
                HasShutoffStrategy: true)));
    }

    [Fact]
    public void Split_WithMissingPartingMetadata_ReturnsWarning()
    {
        var engine = new MoldSeparationEngine();

        var result = engine.Split(new MoldSeparationEngineInput(
            MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
            PartingAxis: PartingAxis.Y,
            PartingOffsetMm: 50m,
            VoxelResolutionMm: 1m,
            HasPartingMetadata: false,
            HasShutoffStrategy: true));

        Assert.Contains(result.ValidationResult.Issues, issue => issue.RuleId == "separation.parting-metadata.missing");
        Assert.True(result.ValidationResult.RequiresEngineerReview);
    }
}