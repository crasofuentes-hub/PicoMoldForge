using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Separation;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Separation;

public sealed class ShutoffStrategyEvaluatorTests
{
    [Fact]
    public void Evaluate_WithVerifiedRegions_ReturnsPass()
    {
        var evaluator = new ShutoffStrategyEvaluator();

        var result = evaluator.Evaluate(new ShutoffStrategyInput(
            Regions: new[]
            {
                new ShutoffRegion(
                    RegionId: "parting-main",
                    RegionType: ShutoffRegionType.PartingLine,
                    ClosureState: ShutoffClosureState.Verified,
                    ContactAreaMm2: 120m,
                    GapMm: 0m,
                    OverlapMm: 0m,
                    IsCriticalToQuality: true)
            }));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Pass, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithNoRegions_ReturnsNeedsEngineerReview()
    {
        var evaluator = new ShutoffStrategyEvaluator();

        var result = evaluator.Evaluate(new ShutoffStrategyInput(
            Regions: Array.Empty<ShutoffRegion>()));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
        Assert.Equal("shutoff.strategy.missing", issue.RuleId);
    }

    [Fact]
    public void Evaluate_WithUndefinedRegion_ReturnsWarningAndReview()
    {
        var evaluator = new ShutoffStrategyEvaluator();

        var result = evaluator.Evaluate(new ShutoffStrategyInput(
            Regions: new[]
            {
                new ShutoffRegion(
                    RegionId: "hole-1",
                    RegionType: ShutoffRegionType.ThroughHole,
                    ClosureState: ShutoffClosureState.NotDefined,
                    ContactAreaMm2: 10m,
                    GapMm: 0m,
                    OverlapMm: 0m)
            }));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.True(result.RequiresEngineerReview);
    }

    [Fact]
    public void Evaluate_WithCriticalGapAboveFailThreshold_ReturnsFail()
    {
        var evaluator = new ShutoffStrategyEvaluator();

        var result = evaluator.Evaluate(new ShutoffStrategyInput(
            Regions: new[]
            {
                new ShutoffRegion(
                    RegionId: "parting-gap",
                    RegionType: ShutoffRegionType.PartingLine,
                    ClosureState: ShutoffClosureState.Preliminary,
                    ContactAreaMm2: 100m,
                    GapMm: 0.08m,
                    OverlapMm: 0m,
                    IsCriticalToQuality: true)
            }));

        Assert.Contains(result.Issues, issue => issue.RuleId == "shutoff.region.parting-gap.gap-fail");
        Assert.True(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithOverlapAboveRecommended_ReturnsWarning()
    {
        var evaluator = new ShutoffStrategyEvaluator();

        var result = evaluator.Evaluate(new ShutoffStrategyInput(
            Regions: new[]
            {
                new ShutoffRegion(
                    RegionId: "boss-boundary",
                    RegionType: ShutoffRegionType.BossBoundary,
                    ClosureState: ShutoffClosureState.Preliminary,
                    ContactAreaMm2: 20m,
                    GapMm: 0m,
                    OverlapMm: 0.03m)
            }));

        var issue = Assert.Single(result.Issues);

        Assert.Equal(EngineeringSeverity.Warning, issue.Severity);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Evaluate_WithEngineerOverride_ReturnsNeedsEngineerReview()
    {
        var evaluator = new ShutoffStrategyEvaluator();

        var result = evaluator.Evaluate(new ShutoffStrategyInput(
            Regions: new[]
            {
                new ShutoffRegion(
                    RegionId: "manual-shutoff",
                    RegionType: ShutoffRegionType.Unknown,
                    ClosureState: ShutoffClosureState.Preliminary,
                    ContactAreaMm2: 10m,
                    GapMm: 0m,
                    OverlapMm: 0m,
                    HasEngineerOverride: true)
            }));

        Assert.Contains(result.Issues, issue => issue.Severity == EngineeringSeverity.NeedsEngineerReview);
        Assert.True(result.RequiresEngineerReview);
        Assert.False(result.HasFailures);
    }

    [Fact]
    public void Summarize_ComputesQualityScore()
    {
        var evaluator = new ShutoffStrategyEvaluator();

        var summary = evaluator.Summarize(new ShutoffStrategyInput(
            Regions: new[]
            {
                new ShutoffRegion(
                    RegionId: "region-a",
                    RegionType: ShutoffRegionType.PartingLine,
                    ClosureState: ShutoffClosureState.Verified,
                    ContactAreaMm2: 100m,
                    GapMm: 0.01m,
                    OverlapMm: 0.01m,
                    IsCriticalToQuality: true)
            }));

        Assert.Equal(1, summary.RegionCount);
        Assert.Equal(0, summary.UndefinedRegionCount);
        Assert.Equal(1, summary.CriticalRegionCount);
        Assert.Equal(0.01m, summary.MaximumGapMm);
        Assert.Equal(0.01m, summary.MaximumOverlapMm);
        Assert.True(summary.QualityScore > 0.80m);
    }
}