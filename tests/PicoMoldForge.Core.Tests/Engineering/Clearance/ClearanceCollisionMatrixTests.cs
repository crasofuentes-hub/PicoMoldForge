using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Clearance;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Clearance;

public sealed class ClearanceCollisionMatrixTests
{
    [Fact]
    public void Evaluate_WithSafeFeatures_ReturnsPass()
    {
        var matrix = new ClearanceCollisionMatrix();

        var result = matrix.Evaluate(CreateInput(new[]
        {
            Feature("cooling-a", ClearanceFeatureKind.CoolingChannel, 0m, 0m, 0m, 100m, 0m, 0m, radius: 4m),
            Feature("ejector-a", ClearanceFeatureKind.EjectorPin, 0m, 20m, 0m, 0m, 20m, 40m, radius: 2m)
        }));

        Assert.Equal(1, result.Summary.PairCount);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Evaluate_WithSingleFeature_ReturnsNeedsEngineerReview()
    {
        var matrix = new ClearanceCollisionMatrix();

        var result = matrix.Evaluate(CreateInput(new[]
        {
            Feature("cooling-a", ClearanceFeatureKind.CoolingChannel, 0m, 0m, 0m, 100m, 0m, 0m, radius: 4m)
        }));

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.True(result.RuleResult.RequiresEngineerReview);
    }

    [Fact]
    public void Evaluate_WithCriticalCollision_ReturnsFail()
    {
        var matrix = new ClearanceCollisionMatrix();

        var result = matrix.Evaluate(CreateInput(new[]
        {
            Feature("gate-a", ClearanceFeatureKind.GateSystem, 0m, 0m, 0m, 100m, 0m, 0m, radius: 4m, critical: true),
            Feature("ejector-a", ClearanceFeatureKind.EjectorPin, 0m, 5m, 0m, 0m, 5m, 40m, radius: 2m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Equal(1, result.Summary.CollisionRiskPairCount);
        Assert.Equal(1, result.Summary.CriticalRiskPairCount);
    }

    [Fact]
    public void Evaluate_WithNonCriticalClearanceShortfall_ReturnsWarning()
    {
        var matrix = new ClearanceCollisionMatrix();

        var result = matrix.Evaluate(CreateInput(new[]
        {
            Feature("cooling-a", ClearanceFeatureKind.CoolingChannel, 0m, 0m, 0m, 100m, 0m, 0m, radius: 2m),
            Feature("runner-a", ClearanceFeatureKind.GateSystem, 0m, 8m, 0m, 100m, 8m, 0m, radius: 2m)
        }, globalClearance: 5m));

        Assert.False(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.Severity == EngineeringSeverity.Warning);
        Assert.Equal(1, result.Summary.CollisionRiskPairCount);
    }

    [Fact]
    public void Evaluate_WithNegativeRadius_ReturnsFail()
    {
        var matrix = new ClearanceCollisionMatrix();

        var result = matrix.Evaluate(CreateInput(new[]
        {
            Feature("bad", ClearanceFeatureKind.Unknown, 0m, 0m, 0m, 10m, 0m, 0m, radius: -1m),
            Feature("good", ClearanceFeatureKind.CoolingChannel, 0m, 20m, 0m, 10m, 20m, 0m, radius: 1m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "clearance-matrix.bad.radius-negative");
    }

    [Fact]
    public void Evaluate_WithMissingFeatureId_ReturnsFail()
    {
        var matrix = new ClearanceCollisionMatrix();

        var result = matrix.Evaluate(CreateInput(new[]
        {
            Feature("", ClearanceFeatureKind.Unknown, 0m, 0m, 0m, 10m, 0m, 0m, radius: 1m),
            Feature("good", ClearanceFeatureKind.CoolingChannel, 0m, 20m, 0m, 10m, 20m, 0m, radius: 1m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "clearance-matrix.missing.id-missing");
    }

    [Fact]
    public void Evaluate_WithMultipleFeatures_ComputesPairCount()
    {
        var matrix = new ClearanceCollisionMatrix();

        var result = matrix.Evaluate(CreateInput(new[]
        {
            Feature("cooling-a", ClearanceFeatureKind.CoolingChannel, 0m, 0m, 0m, 100m, 0m, 0m, radius: 2m),
            Feature("runner-a", ClearanceFeatureKind.GateSystem, 0m, 20m, 0m, 100m, 20m, 0m, radius: 2m),
            Feature("ejector-a", ClearanceFeatureKind.EjectorPin, 50m, 50m, 0m, 50m, 50m, 40m, radius: 2m)
        }));

        Assert.Equal(3, result.Summary.FeatureCount);
        Assert.Equal(3, result.Summary.PairCount);
        Assert.Equal(0, result.Summary.CollisionRiskPairCount);
    }

    private static ClearanceCollisionMatrixInput CreateInput(
        IReadOnlyList<ClearanceFeature> features,
        decimal globalClearance = 3m)
    {
        return new ClearanceCollisionMatrixInput(
            Features: features,
            GlobalMinimumClearanceMm: globalClearance);
    }

    private static ClearanceFeature Feature(
        string id,
        ClearanceFeatureKind kind,
        decimal x1,
        decimal y1,
        decimal z1,
        decimal x2,
        decimal y2,
        decimal z2,
        decimal radius,
        decimal requiredClearance = 0m,
        bool critical = false,
        bool cosmetic = false)
    {
        return new ClearanceFeature(
            FeatureId: id,
            Kind: kind,
            Start: new ClearancePoint(x1, y1, z1),
            End: new ClearancePoint(x2, y2, z2),
            RadiusMm: radius,
            RequiredClearanceMm: requiredClearance,
            IsCriticalToQuality: critical,
            IsCosmeticCritical: cosmetic);
    }
}