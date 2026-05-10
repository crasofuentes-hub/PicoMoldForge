using PicoMoldForge.Core.Cooling;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class CoolingPlannerTests
{
    [Fact]
    public void CoolingChannelRequest_WithValidValues_ReturnsNoValidationErrors()
    {
        var request = CreateValidRequest();

        var errors = request.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void CoolingChannelRequest_WithInvalidDiameter_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            ChannelDiameterMm = 0
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("ChannelDiameterMm", StringComparison.Ordinal));
    }

    [Fact]
    public void CoolingChannelRequest_WithSpacingNotGreaterThanDiameter_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            ChannelDiameterMm = 8,
            ChannelSpacingMm = 8
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("ChannelSpacingMm", StringComparison.Ordinal));
    }

    [Fact]
    public void CoolingChannelRequest_WithInsufficientClearance_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            ChannelDiameterMm = 8,
            MinimumClearanceMm = 4
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("MinimumClearanceMm", StringComparison.Ordinal));
    }

    [Fact]
    public void PlanStraightChannels_WithValidRequest_ReturnsDeterministicSegments()
    {
        var planner = new CoolingPlanner();
        var request = CreateValidRequest() with
        {
            PartSizeXmm = 100,
            PartSizeYmm = 60,
            PartSizeZmm = 30,
            ChannelDiameterMm = 6,
            ChannelSpacingMm = 15,
            MinimumClearanceMm = 10,
            ChannelCount = 3
        };

        var plan = planner.PlanStraightChannels(request);

        Assert.True(plan.IsSuccessful);
        Assert.Equal(3, plan.Segments.Count);
        Assert.Equal("cooling-channel-001", plan.Segments[0].Id);
        Assert.Equal("cooling-channel-002", plan.Segments[1].Id);
        Assert.Equal("cooling-channel-003", plan.Segments[2].Id);

        Assert.Equal(10, plan.Segments[0].StartXmm);
        Assert.Equal(90, plan.Segments[0].EndXmm);
        Assert.Equal(15, plan.Segments[0].StartZmm);
        Assert.Equal(15, plan.Segments[0].EndZmm);

        Assert.Equal(15, plan.Segments[0].StartYmm);
        Assert.Equal(30, plan.Segments[1].StartYmm);
        Assert.Equal(45, plan.Segments[2].StartYmm);

        Assert.Contains(plan.Warnings, warning => warning.Contains("contracts only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanStraightChannels_WithSingleChannel_PlacesChannelAtCenterY()
    {
        var planner = new CoolingPlanner();
        var request = CreateValidRequest() with
        {
            PartSizeYmm = 60,
            MinimumClearanceMm = 10,
            ChannelCount = 1
        };

        var plan = planner.PlanStraightChannels(request);

        Assert.Single(plan.Segments);
        Assert.Equal(30, plan.Segments[0].StartYmm);
    }

    [Fact]
    public void PlanStraightChannels_WithInvalidRequest_Throws()
    {
        var planner = new CoolingPlanner();
        var request = CreateValidRequest() with
        {
            OutputDirectory = string.Empty
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            planner.PlanStraightChannels(request));

        Assert.Contains("Invalid cooling channel request", exception.Message);
    }

    private static CoolingChannelRequest CreateValidRequest()
    {
        return new CoolingChannelRequest(
            OutputDirectory: "output",
            PartSizeXmm: 100,
            PartSizeYmm: 60,
            PartSizeZmm: 30,
            ChannelDiameterMm: 6,
            ChannelSpacingMm: 15,
            MinimumClearanceMm: 10,
            ChannelCount: 3);
    }
}