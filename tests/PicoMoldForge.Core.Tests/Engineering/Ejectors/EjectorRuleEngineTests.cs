using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Ejectors;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Ejectors;

public sealed class EjectorRuleEngineTests
{
    [Fact]
    public void Evaluate_WithPinLandClearanceInRange_ReturnsPass()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.PinLandClearanceMm,
            ActualValue: 0.03m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(0.02m, issue.RequiredValue);
        Assert.Equal(0.05m, issue.RecommendedValue);
        Assert.Equal("mm", issue.Unit);
    }

    [Fact]
    public void Evaluate_WithPinLandClearanceTooTight_ReturnsWarning()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.PinLandClearanceMm,
            ActualValue: 0.015m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalPinLandClearanceBelowFailThreshold_ReturnsFail()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.PinLandClearanceMm,
            ActualValue: 0.005m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithPinLandClearanceTooLoose_ReturnsWarning()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.PinLandClearanceMm,
            ActualValue: 0.07m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.05m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithCosmeticHighSurfaceRisk_ReturnsFail()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.SurfacePlacementRiskScore,
            ActualValue: 0.90m,
            IsCosmeticSurface: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithSurfacePlacementRiskAboveRecommended_ReturnsWarning()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.SurfacePlacementRiskScore,
            ActualValue: 0.45m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithLowDraftAtEjectorLocation_ReturnsWarning()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.DraftAtEjectorLocationDeg,
            ActualValue: 0.35m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.50m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithThinWallVeryLowDraftAtEjectorLocation_ReturnsFail()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.DraftAtEjectorLocationDeg,
            ActualValue: 0.10m,
            IsThinWall: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithEjectorConcentrationAboveRecommended_ReturnsWarning()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.EjectorConcentrationRatio,
            ActualValue: 0.60m));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalEjectorConcentrationAboveFailThreshold_ReturnsFail()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.EjectorConcentrationRatio,
            ActualValue: 0.80m,
            IsCriticalToQuality: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithEngineerOverride_ReturnsNeedsEngineerReview()
    {
        var engine = new EjectorRuleEngine();

        var result = engine.Evaluate(new EjectorRuleInput(
            CheckType: EjectorCheckType.SurfacePlacementRiskScore,
            ActualValue: 0.90m,
            HasEngineerOverride: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }
}