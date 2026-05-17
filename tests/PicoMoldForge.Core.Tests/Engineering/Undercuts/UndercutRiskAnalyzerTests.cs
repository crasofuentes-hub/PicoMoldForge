using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Undercuts;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Undercuts;

public sealed class UndercutRiskAnalyzerTests
{
    [Fact]
    public void Analyze_WithClearPullFace_ReturnsPass()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new UndercutFaceSample("clear", 0m, 0m, 1m, 10m)
        }));

        Assert.Equal(UndercutFaceClassification.ClearPull, Assert.Single(result.Faces).Classification);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithLowPullClearance_ReturnsWarning()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new UndercutFaceSample("low", 1m, 0m, 0.05m, 12m)
        }));

        Assert.Equal(UndercutFaceClassification.LowPullClearance, Assert.Single(result.Faces).Classification);
        Assert.Equal(EngineeringSeverity.Warning, Assert.Single(result.RuleResult.Issues).Severity);
    }

    [Fact]
    public void Analyze_WithSideActionCandidate_ReturnsWarning()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new UndercutFaceSample("side", 1m, 0m, -0.05m, 15m)
        }));

        Assert.Equal(UndercutFaceClassification.SideActionCandidate, Assert.Single(result.Faces).Classification);
        Assert.Equal(EngineeringSeverity.Warning, Assert.Single(result.RuleResult.Issues).Severity);
    }

    [Fact]
    public void Analyze_WithCriticalSideActionCandidate_ReturnsFail()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new UndercutFaceSample("critical-side", 1m, 0m, -0.05m, 15m, TrapDepthMm: 3m)
        }));

        Assert.Equal(UndercutFaceClassification.SideActionCandidate, Assert.Single(result.Faces).Classification);
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithUndercut_ReturnsFail()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new UndercutFaceSample("undercut", 0m, 0m, -1m, 25m)
        }));

        Assert.Equal(UndercutFaceClassification.Undercut, Assert.Single(result.Faces).Classification);
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithInvalidNormal_ReturnsFail()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new UndercutFaceSample("invalid", 0m, 0m, 0m, 10m)
        }));

        Assert.Equal(UndercutFaceClassification.InvalidNormal, Assert.Single(result.Faces).Classification);
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithEmptyFaces_ReturnsFail()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(Array.Empty<UndercutFaceSample>()));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Equal("undercut.faces.missing", Assert.Single(result.RuleResult.Issues).RuleId);
    }

    [Fact]
    public void Analyze_WithMixedFaces_ComputesSummary()
    {
        var analyzer = new UndercutRiskAnalyzer();

        var result = analyzer.Analyze(CreateInput(new[]
        {
            new UndercutFaceSample("clear", 0m, 0m, 1m, 10m),
            new UndercutFaceSample("low", 1m, 0m, 0.05m, 12m),
            new UndercutFaceSample("side", 1m, 0m, -0.05m, 15m, TrapDepthMm: 1m),
            new UndercutFaceSample("undercut", 0m, 0m, -1m, 25m, TrapDepthMm: 2m)
        }));

        Assert.Equal(4, result.Summary.FaceCount);
        Assert.Equal(1, result.Summary.ClearPullCount);
        Assert.Equal(1, result.Summary.LowPullClearanceCount);
        Assert.Equal(1, result.Summary.SideActionCandidateCount);
        Assert.Equal(1, result.Summary.UndercutCount);
        Assert.Equal(52m, result.Summary.RiskySurfaceAreaMm2);
        Assert.Equal(2m, result.Summary.MaximumTrapDepthMm);
    }

    private static UndercutRiskAnalysisInput CreateInput(IReadOnlyList<UndercutFaceSample> faces)
    {
        return new UndercutRiskAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            LowPullDotThreshold: 0.10m,
            SideActionDotThreshold: -0.25m,
            CriticalTrapDepthMm: 2.0m,
            Faces: faces);
    }
}