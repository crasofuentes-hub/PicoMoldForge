using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.EjectionGeometry;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.EjectionGeometry;

public sealed class EjectorCandidateGeneratorTests
{
    [Fact]
    public void Plan_WithValidCandidate_ReturnsPass()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Candidate("ejector-a", 50m, 50m, 20m)
        }));

        var candidate = Assert.Single(result.Candidates);

        Assert.True(candidate.IsAccepted);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Plan_WithNoCandidates_ReturnsNeedsEngineerReview()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(Array.Empty<EjectorCandidate>()));

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.True(result.RuleResult.RequiresEngineerReview);
    }

    [Fact]
    public void Plan_WithOutsideCandidate_ReturnsFail()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Candidate("outside", 120m, 50m, 20m)
        }));

        Assert.False(Assert.Single(result.Candidates).IsAccepted);
        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "ejector-candidates.outside.outside-mold-bounds");
    }

    [Fact]
    public void Plan_WithCoolingClearanceConflict_ReturnsFail()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Candidate("near-cooling", 50m, 50m, 20m, coolingClearance: 1m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "ejector-candidates.near-cooling.cooling-clearance");
    }

    [Fact]
    public void Plan_WithGateSystemClearanceConflict_ReturnsFail()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Candidate("near-gate", 50m, 50m, 20m, gateClearance: 1m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "ejector-candidates.near-gate.gate-system-clearance");
    }

    [Fact]
    public void Plan_WithInsufficientSurfaceSupport_ReturnsWarning()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Candidate("low-support", 50m, 50m, 20m, supportedArea: 5m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "ejector-candidates.low-support.surface-support");
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "ejector-candidates.none-accepted");
        Assert.True(result.RuleResult.RequiresEngineerReview);
    }

    [Fact]
    public void Plan_WithCosmeticSurface_ReturnsWarningAndBlocksAcceptance()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Candidate("cosmetic", 50m, 50m, 20m, isCosmetic: true)
        }));

        var candidate = Assert.Single(result.Candidates);

        Assert.False(candidate.IsAccepted);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "ejector-candidates.cosmetic.cosmetic-surface");
        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Plan_WithMixedCandidates_ComputesSummary()
    {
        var generator = new EjectorCandidateGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Candidate("accepted-a", 30m, 30m, 20m),
            Candidate("accepted-b", 70m, 70m, 20m),
            Candidate("blocked", 120m, 50m, 20m)
        }));

        Assert.Equal(3, result.Summary.CandidateCount);
        Assert.Equal(2, result.Summary.AcceptedCandidateCount);
        Assert.Equal(1, result.Summary.BlockedCandidateCount);
        Assert.True(result.Summary.TotalAcceptedPinAreaMm2 > 0m);
    }

    private static EjectorCandidateGenerationInput CreateInput(
        IReadOnlyList<EjectorCandidate> candidates)
    {
        return new EjectorCandidateGenerationInput(
            MoldBounds: new EjectorLayoutBounds(0m, 0m, 0m, 100m, 100m, 100m),
            Candidates: candidates,
            RequiredCoolingClearanceMm: 5m,
            RequiredGateSystemClearanceMm: 5m,
            RequiredMoldEdgeClearanceMm: 5m,
            MinimumSupportedSurfaceAreaMm2: 20m);
    }

    private static EjectorCandidate Candidate(
        string id,
        decimal x,
        decimal y,
        decimal z,
        decimal diameter = 4m,
        decimal stroke = 20m,
        decimal supportedArea = 40m,
        decimal coolingClearance = 8m,
        decimal gateClearance = 8m,
        decimal edgeClearance = 8m,
        bool isCosmetic = false,
        bool isCritical = false)
    {
        return new EjectorCandidate(
            CandidateId: id,
            Location: new EjectorCandidatePoint(x, y, z),
            PinDiameterMm: diameter,
            StrokeMm: stroke,
            SupportedSurfaceAreaMm2: supportedArea,
            MinimumCoolingClearanceMm: coolingClearance,
            MinimumGateSystemClearanceMm: gateClearance,
            MinimumMoldEdgeClearanceMm: edgeClearance,
            IsCosmeticSurface: isCosmetic,
            IsCriticalToQuality: isCritical);
    }
}
