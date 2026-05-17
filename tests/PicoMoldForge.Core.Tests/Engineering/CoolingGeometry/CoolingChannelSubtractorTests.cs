using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.CoolingGeometry;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.CoolingGeometry;

public sealed class CoolingChannelSubtractorTests
{
    [Fact]
    public void PlanSubtraction_WithValidChannel_ReturnsPass()
    {
        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(CreateInput(new[]
        {
            new CoolingChannelSegment(
                "cooling-a",
                new CoolingChannelPoint(10m, 10m, 10m),
                new CoolingChannelPoint(90m, 10m, 10m),
                DiameterMm: 8m,
                MinimumCavityClearanceMm: 12m,
                MinimumMoldEdgeClearanceMm: 10m)
        }));

        var channel = Assert.Single(result.Channels);

        Assert.True(channel.IsSubtractable);
        Assert.Equal(80m, channel.LengthMm);
        Assert.Equal(EngineeringSeverity.Pass, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void PlanSubtraction_WithNoChannels_ReturnsNeedsEngineerReview()
    {
        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(CreateInput(Array.Empty<CoolingChannelSegment>()));

        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.True(result.RuleResult.RequiresEngineerReview);
    }

    [Fact]
    public void PlanSubtraction_WithOutsideChannel_ReturnsFail()
    {
        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(CreateInput(new[]
        {
            new CoolingChannelSegment(
                "outside",
                new CoolingChannelPoint(10m, 10m, 10m),
                new CoolingChannelPoint(120m, 10m, 10m),
                DiameterMm: 8m,
                MinimumCavityClearanceMm: 12m,
                MinimumMoldEdgeClearanceMm: 10m)
        }));

        Assert.False(Assert.Single(result.Channels).IsSubtractable);
        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "cooling-subtraction.outside.outside-mold-bounds");
    }

    [Fact]
    public void PlanSubtraction_WithInsufficientCavityClearance_ReturnsFail()
    {
        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(CreateInput(new[]
        {
            new CoolingChannelSegment(
                "near-cavity",
                new CoolingChannelPoint(10m, 10m, 10m),
                new CoolingChannelPoint(90m, 10m, 10m),
                DiameterMm: 8m,
                MinimumCavityClearanceMm: 4m,
                MinimumMoldEdgeClearanceMm: 10m)
        }));

        Assert.True(result.RuleResult.HasFailures);
        Assert.Contains(result.RuleResult.Issues, issue => issue.RuleId == "cooling-subtraction.near-cavity.cavity-clearance");
    }

    [Fact]
    public void PlanSubtraction_WithInsufficientEdgeClearance_ReturnsWarning()
    {
        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(CreateInput(new[]
        {
            new CoolingChannelSegment(
                "near-edge",
                new CoolingChannelPoint(10m, 10m, 10m),
                new CoolingChannelPoint(90m, 10m, 10m),
                DiameterMm: 8m,
                MinimumCavityClearanceMm: 12m,
                MinimumMoldEdgeClearanceMm: 2m)
        }));

        Assert.Equal(EngineeringSeverity.Warning, Assert.Single(result.RuleResult.Issues).Severity);
        Assert.True(result.RuleResult.RequiresEngineerReview);
        Assert.False(result.RuleResult.HasFailures);
    }

    [Fact]
    public void PlanSubtraction_WithInvalidDiameter_ReturnsFail()
    {
        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(CreateInput(new[]
        {
            new CoolingChannelSegment(
                "invalid",
                new CoolingChannelPoint(10m, 10m, 10m),
                new CoolingChannelPoint(90m, 10m, 10m),
                DiameterMm: 0m,
                MinimumCavityClearanceMm: 12m,
                MinimumMoldEdgeClearanceMm: 10m)
        }));

        Assert.True(result.RuleResult.HasFailures);
    }

    [Fact]
    public void PlanSubtraction_WithMultipleChannels_ComputesSummary()
    {
        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(CreateInput(new[]
        {
            new CoolingChannelSegment(
                "a",
                new CoolingChannelPoint(10m, 10m, 10m),
                new CoolingChannelPoint(90m, 10m, 10m),
                DiameterMm: 8m,
                MinimumCavityClearanceMm: 12m,
                MinimumMoldEdgeClearanceMm: 10m),
            new CoolingChannelSegment(
                "b",
                new CoolingChannelPoint(10m, 20m, 10m),
                new CoolingChannelPoint(90m, 20m, 10m),
                DiameterMm: 8m,
                MinimumCavityClearanceMm: 4m,
                MinimumMoldEdgeClearanceMm: 10m)
        }));

        Assert.Equal(2, result.Summary.ChannelCount);
        Assert.Equal(1, result.Summary.SubtractableChannelCount);
        Assert.Equal(1, result.Summary.BlockedChannelCount);
        Assert.True(result.Summary.TotalEstimatedRemovedVolumeMm3 > 0m);
    }

    private static CoolingChannelSubtractionInput CreateInput(
        IReadOnlyList<CoolingChannelSegment> channels)
    {
        return new CoolingChannelSubtractionInput(
            MoldBounds: new CoolingMoldBounds(0m, 0m, 0m, 100m, 100m, 100m),
            Channels: channels,
            RequiredCavityClearanceMm: 8m,
            RequiredMoldEdgeClearanceMm: 5m);
    }
}