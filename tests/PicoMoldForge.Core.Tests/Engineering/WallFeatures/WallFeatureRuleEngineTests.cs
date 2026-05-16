using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.WallFeatures;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.WallFeatures;

public sealed class WallFeatureRuleEngineTests
{
    [Fact]
    public void Evaluate_WithAbsNominalWallInRange_ReturnsPass()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.Abs,
            CheckType: WallFeatureCheckType.NominalWallThickness,
            ActualValue: 2.5m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(2.0m, issue.RequiredValue);
        Assert.Equal(3.0m, issue.RecommendedValue);
        Assert.Equal("mm", issue.Unit);
    }

    [Fact]
    public void Evaluate_WithPpThinWallBelowMinimum_ReturnsWarning()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.Pp,
            CheckType: WallFeatureCheckType.NominalWallThickness,
            ActualValue: 0.8m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(1.0m, issue.RequiredValue);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCosmeticAbsVeryThickWall_ReturnsFail()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.Abs,
            CheckType: WallFeatureCheckType.NominalWallThickness,
            ActualValue: 5.5m,
            IsCosmeticCritical: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithRibThicknessAtHalfWall_ReturnsPass()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.General,
            CheckType: WallFeatureCheckType.RibThicknessRatio,
            ActualValue: 0.50m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal("ratio", issue.Unit);
    }

    [Fact]
    public void Evaluate_WithCosmeticRibThicknessAboveSeventyPercent_ReturnsFail()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.General,
            CheckType: WallFeatureCheckType.RibThicknessRatio,
            ActualValue: 0.72m,
            IsCosmeticCritical: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(issue.RequiresEngineerReview);
    }

    [Fact]
    public void Evaluate_WithTallRibAboveRecommended_ReturnsWarning()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.General,
            CheckType: WallFeatureCheckType.RibHeightRatio,
            ActualValue: 3.5m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithBossWallThicknessAboveRecommended_ReturnsWarning()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.General,
            CheckType: WallFeatureCheckType.BossWallThicknessRatio,
            ActualValue: 0.65m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.60m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithAbruptThicknessJumpAboveFiftyPercentOnCtq_ReturnsFail()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.General,
            CheckType: WallFeatureCheckType.AbruptThicknessJumpRatio,
            ActualValue: 0.55m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithInternalRadiusBelowHalfWall_ReturnsWarning()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.General,
            CheckType: WallFeatureCheckType.InternalRadiusRatio,
            ActualValue: 0.30m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.50m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithCriticalSharpInternalRadius_ReturnsFail()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.General,
            CheckType: WallFeatureCheckType.InternalRadiusRatio,
            ActualValue: 0.10m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithEngineerOverride_ReturnsNeedsEngineerReview()
    {
        var engine = new WallFeatureRuleEngine();

        var result = engine.Evaluate(new WallFeatureRuleInput(
            Material: WallFeatureMaterial.Abs,
            CheckType: WallFeatureCheckType.NominalWallThickness,
            ActualValue: 5.5m,
            HasEngineerOverride: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }
}