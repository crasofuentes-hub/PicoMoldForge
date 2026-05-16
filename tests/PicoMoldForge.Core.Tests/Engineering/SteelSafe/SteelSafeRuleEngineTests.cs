using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.SteelSafe;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.SteelSafe;

public sealed class SteelSafeRuleEngineTests
{
    [Fact]
    public void Evaluate_WithGeneralAllowanceInRange_ReturnsPass()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.GeneralAllowanceMm,
            ActualValue: 0.25m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(0.10m, issue.RequiredValue);
        Assert.Equal(0.50m, issue.RecommendedValue);
        Assert.Equal("mm", issue.Unit);
    }

    [Fact]
    public void Evaluate_WithNonCriticalAllowanceBelowMinimum_ReturnsWarning()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.GeneralAllowanceMm,
            ActualValue: 0.05m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalDimensionMissingAllowance_ReturnsFail()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.CriticalDimensionAllowanceMm,
            ActualValue: 0.0m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithShutoffMissingAllowance_ReturnsFail()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.ShutoffAllowanceMm,
            ActualValue: 0.0m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithPartingLineAllowanceBelowMinimum_ReturnsWarning()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.PartingLineAllowanceMm,
            ActualValue: 0.05m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCosmeticAllowanceTooHigh_ReturnsFail()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.GeneralAllowanceMm,
            ActualValue: 1.20m,
            IsCosmeticSurface: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCosmeticReworkRiskInRange_ReturnsPass()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.CosmeticReworkRiskScore,
            ActualValue: 0.20m,
            IsCosmeticSurface: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal("score", issue.Unit);
    }

    [Fact]
    public void Evaluate_WithCosmeticReworkRiskAboveRecommended_ReturnsWarning()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.CosmeticReworkRiskScore,
            ActualValue: 0.45m,
            IsCosmeticSurface: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
        Assert.True(result.RequiresEngineerReview);
    }

    [Fact]
    public void Evaluate_WithCriticalCosmeticReworkRiskAboveFailThreshold_ReturnsFail()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.CosmeticReworkRiskScore,
            ActualValue: 0.80m,
            IsCriticalToQuality: true,
            IsCosmeticSurface: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithEngineerOverride_ReturnsNeedsEngineerReview()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.CriticalDimensionAllowanceMm,
            ActualValue: 0.0m,
            IsCriticalToQuality: true,
            HasEngineerOverride: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithNegativeAllowance_ReturnsFail()
    {
        var engine = new SteelSafeRuleEngine();

        var result = engine.Evaluate(new SteelSafeRuleInput(
            CheckType: SteelSafeCheckType.GeneralAllowanceMm,
            ActualValue: -0.10m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }
}