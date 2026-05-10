using PicoMoldForge.Core.MoldSystems;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class PreliminaryMoldSystemPlannerTests
{
    [Fact]
    public void MoldSystemRequest_WithValidValues_ReturnsNoValidationErrors()
    {
        var request = CreateValidRequest();

        var errors = request.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void MoldSystemRequest_WithInvalidPartSize_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            PartSizeXmm = 0
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("PartSizeXmm", StringComparison.Ordinal));
    }

    [Fact]
    public void MoldSystemRequest_WithTooManyEjectorPins_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            EjectorPinCount = 65
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("EjectorPinCount", StringComparison.Ordinal));
    }

    [Fact]
    public void MoldSystemRequest_WithInsertClearanceGreaterThanMargin_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            InsertClearanceMm = 20,
            MoldMarginMm = 20
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("InsertClearanceMm", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_WithValidRequest_ReturnsMoldBaseEnvelope()
    {
        var planner = new PreliminaryMoldSystemPlanner();

        var plan = planner.Plan(CreateValidRequest());

        Assert.True(plan.IsSuccessful);
        Assert.Equal(140, plan.MoldBase.WidthMm);
        Assert.Equal(100, plan.MoldBase.DepthMm);
        Assert.Equal(70, plan.MoldBase.HeightMm);
        Assert.Equal(20, plan.MoldBase.MarginMm);
    }

    [Fact]
    public void Plan_WithValidRequest_ReturnsDeterministicEjectorPins()
    {
        var planner = new PreliminaryMoldSystemPlanner();

        var plan = planner.Plan(CreateValidRequest());

        Assert.True(plan.Ejectors.IsSuccessful);
        Assert.Equal(4, plan.Ejectors.Pins.Count);

        Assert.Equal("ejector-pin-001", plan.Ejectors.Pins[0].Id);
        Assert.Equal(20, plan.Ejectors.Pins[0].CenterXmm);
        Assert.Equal(50, plan.Ejectors.Pins[0].CenterYmm);
        Assert.Equal(4, plan.Ejectors.Pins[0].DiameterMm);

        Assert.Equal("ejector-pin-004", plan.Ejectors.Pins[3].Id);
        Assert.Equal(120, plan.Ejectors.Pins[3].CenterXmm);
    }

    [Fact]
    public void Plan_WithValidRequest_ReturnsVentChannels()
    {
        var planner = new PreliminaryMoldSystemPlanner();

        var plan = planner.Plan(CreateValidRequest());

        Assert.True(plan.Vents.IsSuccessful);
        Assert.Equal(2, plan.Vents.Channels.Count);
        Assert.Equal("vent-channel-001", plan.Vents.Channels[0].Id);
        Assert.Equal(20, plan.Vents.Channels[0].StartXmm);
        Assert.Equal(120, plan.Vents.Channels[0].EndXmm);
        Assert.Equal(0.5m, plan.Vents.Channels[0].WidthMm);
        Assert.Equal(0.1m, plan.Vents.Channels[0].DepthMm);
    }

    [Fact]
    public void Plan_WithValidRequest_ReturnsInsertPocket()
    {
        var planner = new PreliminaryMoldSystemPlanner();

        var plan = planner.Plan(CreateValidRequest());

        var pocket = Assert.Single(plan.Inserts.Pockets);

        Assert.True(plan.Inserts.IsSuccessful);
        Assert.Equal("insert-pocket-001", pocket.Id);
        Assert.Equal(18, pocket.MinXmm);
        Assert.Equal(18, pocket.MinYmm);
        Assert.Equal(18, pocket.MinZmm);
        Assert.Equal(122, pocket.MaxXmm);
        Assert.Equal(82, pocket.MaxYmm);
        Assert.Equal(52, pocket.MaxZmm);
        Assert.Equal(2, pocket.ClearanceMm);
    }

    [Fact]
    public void Plan_WithInvalidRequest_Throws()
    {
        var planner = new PreliminaryMoldSystemPlanner();
        var request = CreateValidRequest() with
        {
            OutputDirectory = string.Empty
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            planner.Plan(request));

        Assert.Contains("Invalid mold system request", exception.Message);
    }

    private static MoldSystemRequest CreateValidRequest()
    {
        return new MoldSystemRequest(
            OutputDirectory: "output",
            PartSizeXmm: 100,
            PartSizeYmm: 60,
            PartSizeZmm: 30,
            MoldMarginMm: 20,
            EjectorPinDiameterMm: 4,
            EjectorPinCount: 4,
            VentWidthMm: 0.5m,
            VentDepthMm: 0.1m,
            InsertClearanceMm: 2);
    }
}