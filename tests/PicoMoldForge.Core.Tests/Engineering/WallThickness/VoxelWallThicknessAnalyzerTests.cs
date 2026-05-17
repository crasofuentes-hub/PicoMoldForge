using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.WallThickness;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.WallThickness;

public sealed class VoxelWallThicknessAnalyzerTests
{
    [Fact]
    public void Analyze_WithNominalSample_ReturnsPass()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new VoxelWallThicknessSample("nominal", 2.5m, 2.5m, 10m)
        }));

        Assert.Equal(VoxelWallThicknessClassification.Nominal, Assert.Single(result.Samples).Classification);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithThinSample_ReturnsWarning()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new VoxelWallThicknessSample("thin", 0.8m, 2.0m, 12m)
        }));

        Assert.Equal(VoxelWallThicknessClassification.Thin, Assert.Single(result.Samples).Classification);
        Assert.Equal(EngineeringSeverity.Warning, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithCriticalThinSample_ReturnsFail()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new VoxelWallThicknessSample("critical-thin", 0.8m, 2.0m, 12m, IsCriticalToQuality: true)
        }));

        Assert.Equal(VoxelWallThicknessClassification.Thin, Assert.Single(result.Samples).Classification);
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithThickSample_ReturnsWarning()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new VoxelWallThicknessSample("thick", 4.0m, 2.0m, 20m)
        }));

        Assert.Equal(VoxelWallThicknessClassification.Thick, Assert.Single(result.Samples).Classification);
        Assert.Equal(EngineeringSeverity.Warning, Assert.Single(result.RuleResult.Issues).Severity);
    }

    [Fact]
    public void Analyze_WithAbruptChange_ReturnsWarning()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new VoxelWallThicknessSample("abrupt", 2.8m, 2.0m, 15m)
        }));

        var sample = Assert.Single(result.Samples);

        Assert.Equal(VoxelWallThicknessClassification.AbruptChange, sample.Classification);
        Assert.Equal(0.4m, sample.ThicknessDeltaRatio);
        Assert.Equal(EngineeringSeverity.Warning, Assert.Single(result.RuleResult.Issues).Severity);
    }

    [Fact]
    public void Analyze_WithInvalidSample_ReturnsFail()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new VoxelWallThicknessSample("", 0m, 2.0m, 10m)
        }));

        Assert.Equal(VoxelWallThicknessClassification.Invalid, Assert.Single(result.Samples).Classification);
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithEmptySamples_ReturnsFail()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(Array.Empty<VoxelWallThicknessSample>()));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Equal("wall-thickness.voxel.samples.missing", Assert.Single(result.RuleResult.Issues).RuleId);
    }

    [Fact]
    public void Analyze_WithMixedSamples_ComputesSummary()
    {
        var analyzer = new VoxelWallThicknessAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new VoxelWallThicknessSample("nominal", 2.0m, 2.0m, 10m),
            new VoxelWallThicknessSample("thin", 0.8m, 2.0m, 12m),
            new VoxelWallThicknessSample("thick", 4.0m, 2.0m, 20m),
            new VoxelWallThicknessSample("abrupt", 2.8m, 2.0m, 15m)
        }));

        Assert.Equal(4, result.Summary.SampleCount);
        Assert.Equal(1, result.Summary.NominalCount);
        Assert.Equal(1, result.Summary.ThinCount);
        Assert.Equal(1, result.Summary.ThickCount);
        Assert.Equal(1, result.Summary.AbruptChangeCount);
        Assert.Equal(47m, result.Summary.RiskySurfaceAreaMm2);
        Assert.Equal(0.8m, result.Summary.MinimumObservedThicknessMm);
        Assert.Equal(4.0m, result.Summary.MaximumObservedThicknessMm);
    }

    private static VoxelWallThicknessAnalysisInput CreateInput(
        IReadOnlyList<VoxelWallThicknessSample> samples)
    {
        return new VoxelWallThicknessAnalysisInput(
            MinimumThicknessMm: 1.0m,
            MaximumThicknessMm: 3.0m,
            AbruptChangeWarningRatio: 0.30m,
            Samples: samples);
    }
}