using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Shrinkage;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Shrinkage;

public sealed class ShrinkageRuleEngineTests
{
    [Fact]
    public void Evaluate_WithAbsDatasheetShrinkageInRange_ReturnsPass()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.Abs,
            ActualShrinkageRate: 0.005m,
            IsCriticalDimension: true,
            UsesDatasheetValue: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.Equal(0.005m, issue.ActualValue);
        Assert.Equal(0.004m, issue.RequiredValue);
        Assert.Equal("ratio", issue.Unit);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalDimensionMissingShrinkage_ReturnsFail()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.Pp,
            ActualShrinkageRate: null,
            IsCriticalDimension: true,
            UsesDatasheetValue: false));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(issue.RequiresEngineerReview);
        Assert.True(result.HasFailures);
        Assert.Equal("CriticalDimension", issue.FeatureType);
    }

    [Fact]
    public void Evaluate_WithNonCriticalMissingShrinkage_ReturnsWarning()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.Abs,
            ActualShrinkageRate: null,
            IsCriticalDimension: false,
            UsesDatasheetValue: false));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.True(issue.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithCriticalZeroShrinkage_ReturnsFail()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.Pom,
            ActualShrinkageRate: 0m,
            IsCriticalDimension: true,
            UsesDatasheetValue: false));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Fail, issue.Severity);
        Assert.True(result.HasFailures);
        Assert.Equal(0.015m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithPpShrinkageTooLow_ReturnsWarning()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.Pp,
            ActualShrinkageRate: 0.005m,
            IsCriticalDimension: false,
            UsesDatasheetValue: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.Equal(0.010m, issue.RequiredValue);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithPeShrinkageTooHigh_ReturnsWarningAndEngineerReview()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.Pe,
            ActualShrinkageRate: 0.040m,
            IsCriticalDimension: false,
            UsesDatasheetValue: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.True(issue.RequiresEngineerReview);
        Assert.Equal(0.030m, issue.RequiredValue);
    }

    [Fact]
    public void Evaluate_WithNonDatasheetCriticalShrinkage_ReturnsWarningAndReview()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.Pc,
            ActualShrinkageRate: 0.006m,
            IsCriticalDimension: true,
            UsesDatasheetValue: false));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.True(issue.RequiresEngineerReview);
        Assert.Equal("shrinkage.pc.not-datasheet", issue.RuleId);
    }

    [Fact]
    public void Evaluate_WithEngineerOverride_ReturnsNeedsEngineerReview()
    {
        var engine = new ShrinkageRuleEngine();

        var result = engine.Evaluate(new ShrinkageRuleInput(
            Material: ShrinkageMaterial.NylonPa,
            ActualShrinkageRate: 0.008m,
            IsCriticalDimension: true,
            UsesDatasheetValue: false,
            HasEngineerOverride: true));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void CalculateCavityDimension_UsesNominalTimesOnePlusShrinkage()
    {
        var engine = new ShrinkageRuleEngine();

        var cavityDimension = engine.CalculateCavityDimension(
            nominalDimensionMm: 100m,
            shrinkageRate: 0.005m);

        Assert.Equal(100.5m, cavityDimension);
    }

    [Fact]
    public void CalculateCavityDimension_WithNegativeShrinkage_Throws()
    {
        var engine = new ShrinkageRuleEngine();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            engine.CalculateCavityDimension(
                nominalDimensionMm: 100m,
                shrinkageRate: -0.001m));
    }
}