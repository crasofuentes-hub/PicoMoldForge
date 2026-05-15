using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Draft;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Draft;

public sealed class DraftRuleEngineTests
{
    [Fact]
    public void Evaluate_WithAbsSmoothWallAtTwoDegrees_ReturnsPass()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.Abs,
            SurfaceType: DraftSurfaceType.Smooth,
            FeatureType: DraftFeatureType.Wall,
            ActualDraftDeg: 2.0m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(1.0m, issue.RequiredValue);
        Assert.Equal("deg", issue.Unit);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithAbsSmoothWallBelowRequired_ReturnsWarning()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.Abs,
            SurfaceType: DraftSurfaceType.Smooth,
            FeatureType: DraftFeatureType.Wall,
            ActualDraftDeg: 0.75m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.75m, issue.ActualValue);
        Assert.Equal(1.0m, issue.RequiredValue);
        Assert.Equal("expert-injection-mold-rules.v1", issue.SourceRulePackVersion);
    }

    [Fact]
    public void Evaluate_WithZeroDraftWall_ReturnsFail()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.Abs,
            SurfaceType: DraftSurfaceType.Smooth,
            FeatureType: DraftFeatureType.Wall,
            ActualDraftDeg: 0.0m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(issue.RequiresEngineerReview);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithShutoffBelowMinimum_ReturnsFail()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.Any,
            SurfaceType: DraftSurfaceType.Smooth,
            FeatureType: DraftFeatureType.Shutoff,
            ActualDraftDeg: 1.0m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.Equal(3.0m, issue.RequiredValue);
        Assert.Equal("Shutoff", issue.FeatureType);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithNylonSmoothWallBelowMinimumButAboveFail_ReturnsWarning()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.NylonPa,
            SurfaceType: DraftSurfaceType.Smooth,
            FeatureType: DraftFeatureType.Wall,
            ActualDraftDeg: 0.4m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.5m, issue.RequiredValue);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithBossInnerHoleBelowMinimum_ReturnsWarning()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.Any,
            SurfaceType: DraftSurfaceType.Smooth,
            FeatureType: DraftFeatureType.BossInnerHoleCorePin,
            ActualDraftDeg: 0.2m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.25m, issue.RequiredValue);
        Assert.Equal(0.5m, issue.RecommendedValue);
    }

    [Fact]
    public void Evaluate_WithHeavyTextureAndLowDraft_ReturnsFail()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.Pc,
            SurfaceType: DraftSurfaceType.TexturedHeavy,
            FeatureType: DraftFeatureType.Wall,
            ActualDraftDeg: 1.0m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithTextureDepthAboveReviewThreshold_ReturnsNeedsEngineerReview()
    {
        var engine = new DraftRuleEngine();

        var result = engine.Evaluate(new DraftRuleInput(
            Material: DraftMaterial.Pc,
            SurfaceType: DraftSurfaceType.TexturedHeavy,
            FeatureType: DraftFeatureType.Wall,
            ActualDraftDeg: 8.0m,
            TextureDepthMm: 0.12m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
        Assert.Equal("mm", issue.Unit);
    }

    [Fact]
    public void ResolveRequiredDraftDeg_WithCosmeticCriticalPart_AddsConservativeMargin()
    {
        var engine = new DraftRuleEngine();

        var required = engine.ResolveRequiredDraftDeg(new DraftRuleInput(
            Material: DraftMaterial.Abs,
            SurfaceType: DraftSurfaceType.Smooth,
            FeatureType: DraftFeatureType.Wall,
            ActualDraftDeg: 2.0m,
            IsCosmeticCritical: true));

        Assert.Equal(1.5m, required);
    }
}