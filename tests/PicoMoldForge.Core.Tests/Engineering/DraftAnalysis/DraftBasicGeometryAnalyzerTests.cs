using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.DraftAnalysis;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.DraftAnalysis;

public sealed class DraftBasicGeometryAnalyzerTests
{
    [Fact]
    public void Analyze_WithPositiveDraftFace_ReturnsPass()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        var result = analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            MinimumRequiredDraftDeg: 1m,
            Faces: new[]
            {
                new DraftFaceSample("face-positive", 0.996m, 0m, 0.087m, 25m)
            }));

        Assert.Equal(DraftFaceClassification.PositiveDraft, Assert.Single(result.Faces).Classification);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithZeroDraftFace_ReturnsWarning()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        var result = analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            MinimumRequiredDraftDeg: 1m,
            Faces: new[]
            {
                new DraftFaceSample("face-zero", 1m, 0m, 0m, 20m)
            }));

        var issue = Assert.Single(result.RuleResult.Issues);

        Assert.Equal(DraftFaceClassification.ZeroDraft, Assert.Single(result.Faces).Classification);
        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(20m, result.Summary.RiskySurfaceAreaMm2);
    }

    [Fact]
    public void Analyze_WithLowDraftFace_ReturnsWarning()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        var result = analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            MinimumRequiredDraftDeg: 3m,
            Faces: new[]
            {
                new DraftFaceSample("face-low", 0.999m, 0m, 0.034m, 15m)
            }));

        Assert.Equal(DraftFaceClassification.LowDraft, Assert.Single(result.Faces).Classification);
        Assert.Equal(EngineeringSeverity.Warning, Assert.Single(result.RuleResult.Issues).Severity);
    }

    [Fact]
    public void Analyze_WithNegativeDraftFace_ReturnsFail()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        var result = analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            MinimumRequiredDraftDeg: 1m,
            Faces: new[]
            {
                new DraftFaceSample("face-negative", 0.996m, 0m, -0.087m, 30m)
            }));

        Assert.Equal(DraftFaceClassification.NegativeDraft, Assert.Single(result.Faces).Classification);
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithInvalidNormal_ReturnsFail()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        var result = analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            MinimumRequiredDraftDeg: 1m,
            Faces: new[]
            {
                new DraftFaceSample("face-invalid", 0m, 0m, 0m, 10m)
            }));

        Assert.Equal(DraftFaceClassification.InvalidNormal, Assert.Single(result.Faces).Classification);
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Analyze_WithEmptyFaces_ReturnsFail()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        var result = analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            MinimumRequiredDraftDeg: 1m,
            Faces: Array.Empty<DraftFaceSample>()));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Equal("draft.geometry.faces.missing", Assert.Single(result.RuleResult.Issues).RuleId);
    }

    [Fact]
    public void Analyze_WithInvalidPullDirection_Throws()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
                PullDirectionX: 0m,
                PullDirectionY: 0m,
                PullDirectionZ: 0m,
                MinimumRequiredDraftDeg: 1m,
                Faces: new[]
                {
                    new DraftFaceSample("face", 1m, 0m, 0m, 10m)
                })));
    }

    [Fact]
    public void Analyze_WithMixedFaces_ComputesSummaryCounts()
    {
        var analyzer = new DraftBasicGeometryAnalyzer();

        var result = analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
            PullDirectionX: 0m,
            PullDirectionY: 0m,
            PullDirectionZ: 1m,
            MinimumRequiredDraftDeg: 1m,
            Faces: new[]
            {
                new DraftFaceSample("positive", 0.996m, 0m, 0.087m, 10m),
                new DraftFaceSample("zero", 1m, 0m, 0m, 20m),
                new DraftFaceSample("negative", 0.996m, 0m, -0.087m, 30m)
            }));

        Assert.Equal(3, result.Summary.FaceCount);
        Assert.Equal(1, result.Summary.PositiveDraftCount);
        Assert.Equal(1, result.Summary.ZeroDraftCount);
        Assert.Equal(1, result.Summary.NegativeDraftCount);
        Assert.Equal(50m, result.Summary.RiskySurfaceAreaMm2);
    }
}