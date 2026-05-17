using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.GateSystem;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.GateSystem;

public sealed class GateRunnerSprueGeneratorTests
{
    [Fact]
    public void Plan_WithCompleteFeedSystem_ReturnsPass()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Segment("sprue", GateSystemFeatureType.Sprue, 50m, 50m, 100m, 50m, 50m, 60m),
            Segment("runner", GateSystemFeatureType.Runner, 50m, 50m, 60m, 70m, 50m, 60m),
            Segment("gate", GateSystemFeatureType.Gate, 70m, 50m, 60m, 80m, 50m, 60m)
        }));

        Assert.Equal(3, result.Summary.SegmentCount);
        Assert.Equal(1, result.Summary.SprueCount);
        Assert.Equal(1, result.Summary.RunnerCount);
        Assert.Equal(1, result.Summary.GateCount);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Plan_WithNoSegments_ReturnsNeedsEngineerReview()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(Array.Empty<GateRunnerSprueSegment>()));

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.True(result.RuleResult.RequiresEngineerReview);
    }

    [Fact]
    public void Plan_WithMissingGate_ReturnsFail()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Segment("sprue", GateSystemFeatureType.Sprue, 50m, 50m, 100m, 50m, 50m, 60m),
            Segment("runner", GateSystemFeatureType.Runner, 50m, 50m, 60m, 70m, 50m, 60m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "gate-system.gate.missing");
    }

    [Fact]
    public void Plan_WithOutsideSegment_ReturnsFail()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Segment("sprue", GateSystemFeatureType.Sprue, 50m, 50m, 100m, 50m, 50m, 60m),
            Segment("runner", GateSystemFeatureType.Runner, 50m, 50m, 60m, 120m, 50m, 60m),
            Segment("gate", GateSystemFeatureType.Gate, 70m, 50m, 60m, 80m, 50m, 60m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "gate-system.runner.outside-mold-bounds");
    }

    [Fact]
    public void Plan_WithInsufficientCavityClearance_ReturnsFail()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Segment("sprue", GateSystemFeatureType.Sprue, 50m, 50m, 100m, 50m, 50m, 60m),
            Segment("runner", GateSystemFeatureType.Runner, 50m, 50m, 60m, 70m, 50m, 60m),
            Segment("gate", GateSystemFeatureType.Gate, 70m, 50m, 60m, 80m, 50m, 60m, cavityClearance: 2m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "gate-system.gate.cavity-clearance");
    }

    [Fact]
    public void Plan_WithInsufficientMoldEdgeClearance_ReturnsWarning()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Segment("sprue", GateSystemFeatureType.Sprue, 50m, 50m, 100m, 50m, 50m, 60m),
            Segment("runner", GateSystemFeatureType.Runner, 50m, 50m, 60m, 70m, 50m, 60m),
            Segment("gate", GateSystemFeatureType.Gate, 70m, 50m, 60m, 80m, 50m, 60m, edgeClearance: 2m)
        }));

        Assert.False(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.Severity == EngineeringSeverity.Warning);
    }

    [Fact]
    public void Plan_WithInvalidFlowArea_ReturnsFail()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Segment("sprue", GateSystemFeatureType.Sprue, 50m, 50m, 100m, 50m, 50m, 60m),
            Segment("runner", GateSystemFeatureType.Runner, 50m, 50m, 60m, 70m, 50m, 60m),
            Segment("gate", GateSystemFeatureType.Gate, 70m, 50m, 60m, 80m, 50m, 60m, flowArea: 0m)
        }));

        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void Plan_ComputesSummaryVolumes()
    {
        var generator = new GateRunnerSprueGenerator();

        var result = generator.Plan(CreateInput(new[]
        {
            Segment("sprue", GateSystemFeatureType.Sprue, 50m, 50m, 100m, 50m, 50m, 60m),
            Segment("runner", GateSystemFeatureType.Runner, 50m, 50m, 60m, 70m, 50m, 60m),
            Segment("gate", GateSystemFeatureType.Gate, 70m, 50m, 60m, 80m, 50m, 60m)
        }));

        Assert.Equal(3, result.Summary.GeneratableSegmentCount);
        Assert.Equal(0, result.Summary.BlockedSegmentCount);
        Assert.True(result.Summary.TotalFlowLengthMm > 0m);
        Assert.True(result.Summary.TotalEstimatedVolumeMm3 > 0m);
    }

    private static GateRunnerSprueGenerationInput CreateInput(
        IReadOnlyList<GateRunnerSprueSegment> segments)
    {
        return new GateRunnerSprueGenerationInput(
            MoldBounds: new GateSystemBounds(0m, 0m, 0m, 100m, 100m, 120m),
            Segments: segments,
            RequiredCavityClearanceMm: 5m,
            RequiredMoldEdgeClearanceMm: 5m);
    }

    private static GateRunnerSprueSegment Segment(
        string id,
        GateSystemFeatureType type,
        decimal x1,
        decimal y1,
        decimal z1,
        decimal x2,
        decimal y2,
        decimal z2,
        decimal diameter = 4m,
        decimal flowArea = 12m,
        decimal cavityClearance = 8m,
        decimal edgeClearance = 8m)
    {
        return new GateRunnerSprueSegment(
            FeatureId: id,
            FeatureType: type,
            Start: new GateSystemPoint(x1, y1, z1),
            End: new GateSystemPoint(x2, y2, z2),
            HydraulicDiameterMm: diameter,
            FlowAreaMm2: flowArea,
            MinimumCavityClearanceMm: cavityClearance,
            MinimumMoldEdgeClearanceMm: edgeClearance);
    }
}