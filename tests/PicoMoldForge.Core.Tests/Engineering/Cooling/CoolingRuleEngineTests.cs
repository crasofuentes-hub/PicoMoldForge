using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Cooling;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Cooling;

public sealed class CoolingRuleEngineTests
{
    [Fact]
    public void Evaluate_WithChannelDistanceAtOnePointTwoDiameters_ReturnsPass()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.ChannelDistanceToCavityDiameterRatio,
            ActualValue: 1.2m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(1.0m, issue.RequiredValue);
        Assert.Equal(1.5m, issue.RecommendedValue);
        Assert.Equal("ratio", issue.Unit);
    }

    [Fact]
    public void Evaluate_WithChannelDistanceBelowOneDiameter_ReturnsWarning()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.ChannelDistanceToCavityDiameterRatio,
            ActualValue: 0.8m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(1.0m, issue.RequiredValue);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalChannelDistanceFarAboveLimit_ReturnsFail()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.ChannelDistanceToCavityDiameterRatio,
            ActualValue: 2.8m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithLocalThicknessJumpAtTwentyPercent_ReturnsPass()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.LocalThicknessJumpRatio,
            ActualValue: 0.20m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(0.30m, issue.RecommendedValue);
    }

    [Fact]
    public void Evaluate_WithLocalThicknessJumpAboveThirtyPercent_ReturnsWarning()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.LocalThicknessJumpRatio,
            ActualValue: 0.35m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCosmeticLocalThicknessJumpAboveFiftyPercent_ReturnsFail()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.LocalThicknessJumpRatio,
            ActualValue: 0.55m,
            IsCosmeticCritical: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCoolingBalanceDeltaAboveRecommended_ReturnsWarning()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.CoolingBalanceDeltaRatio,
            ActualValue: 0.20m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.15m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithCriticalCoolingBalanceDeltaAboveFailThreshold_ReturnsFail()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.CoolingBalanceDeltaRatio,
            ActualValue: 0.35m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void EstimateRelativeCoolingTimeRatio_UsesWallThicknessSquared()
    {
        var engine = new CoolingRuleEngine();

        var ratio = engine.EstimateRelativeCoolingTimeRatio(
            localWallThicknessMm: 4.0m,
            nominalWallThicknessMm: 2.0m);

        Assert.Equal(4.0m, ratio);
    }

    [Fact]
    public void Evaluate_WithRelativeCoolingTimeRatioFourOnCriticalFeature_ReturnsWarningAtThreshold()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.RelativeCoolingTimeRatio,
            ActualValue: 4.0m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithEngineerOverride_ReturnsNeedsEngineerReview()
    {
        var engine = new CoolingRuleEngine();

        var result = engine.Evaluate(new CoolingRuleInput(
            CheckType: CoolingCheckType.CoolingBalanceDeltaRatio,
            ActualValue: 0.40m,
            HasEngineerOverride: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }
}