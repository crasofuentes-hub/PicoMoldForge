using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Venting;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Venting;

public sealed class VentingRuleEngineTests
{
    [Fact]
    public void Evaluate_WithVentDepthInRange_ReturnsPass()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.VentDepthMm,
            ActualValue: 0.03m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(0.02m, issue.RequiredValue);
        Assert.Equal(0.05m, issue.RecommendedValue);
        Assert.Equal("mm", issue.Unit);
    }

    [Fact]
    public void Evaluate_WithVentDepthTooShallow_ReturnsWarning()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.VentDepthMm,
            ActualValue: 0.015m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithLongFlowVentDepthBelowFailThreshold_ReturnsFail()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.VentDepthMm,
            ActualValue: 0.005m,
            IsLongFlowPath: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithFlashSensitiveVentDepthTooDeep_ReturnsFail()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.VentDepthMm,
            ActualValue: 0.09m,
            IsFlashSensitive: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithVentWidthTooNarrow_ReturnsWarning()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.VentWidthMm,
            ActualValue: 0.15m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.20m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithVentLandLengthInRange_ReturnsPass()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.VentLandLengthMm,
            ActualValue: 1.20m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(2.00m, issue.RecommendedValue);
    }

    [Fact]
    public void Evaluate_WithEndOfFillRiskAboveRecommended_ReturnsWarning()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.EndOfFillVentRiskScore,
            ActualValue: 0.45m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalEndOfFillRiskAboveFailThreshold_ReturnsFail()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.EndOfFillVentRiskScore,
            ActualValue: 0.80m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithGasTrapRiskAboveRecommended_ReturnsWarning()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.GasTrapRiskScore,
            ActualValue: 0.40m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalGasTrapRiskAboveFailThreshold_ReturnsFail()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.GasTrapRiskScore,
            ActualValue: 0.75m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithEngineerOverride_ReturnsNeedsEngineerReview()
    {
        var engine = new VentingRuleEngine();

        var result = engine.Evaluate(new VentingRuleInput(
            CheckType: VentingCheckType.GasTrapRiskScore,
            ActualValue: 0.80m,
            HasEngineerOverride: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }
}