using PicoMoldForge.Core.Lattice;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class LatticePlannerTests
{
    [Fact]
    public void LatticeRegionRequest_WithValidValues_ReturnsNoValidationErrors()
    {
        var request = CreateValidRequest();

        var errors = request.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void LatticeRegionRequest_WithInvalidCellSize_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            CellSizeMm = 0
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("CellSizeMm", StringComparison.Ordinal));
    }

    [Fact]
    public void LatticeRegionRequest_WithInvalidBeamRadius_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            BeamRadiusMm = 0
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("BeamRadiusMm", StringComparison.Ordinal));
    }

    [Fact]
    public void LatticeRegionRequest_WithBeamRadiusTooLarge_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            CellSizeMm = 10,
            BeamRadiusMm = 5
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("less than half", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LatticeRegionRequest_WithInvalidDensity_ReturnsValidationError()
    {
        var request = CreateValidRequest() with
        {
            TargetRelativeDensity = 1.2m
        };

        var errors = request.Validate();

        Assert.Contains(errors, error => error.Contains("TargetRelativeDensity", StringComparison.Ordinal));
    }

    [Fact]
    public void PlanSimpleGrid_WithValidRequest_ReturnsDeterministicBeams()
    {
        var planner = new LatticePlanner();
        var request = CreateValidRequest() with
        {
            MinXmm = 0,
            MinYmm = 0,
            MinZmm = 0,
            MaxXmm = 20,
            MaxYmm = 10,
            MaxZmm = 10,
            CellSizeMm = 10,
            BeamRadiusMm = 1
        };

        var plan = planner.PlanSimpleGrid(request);

        Assert.True(plan.IsSuccessful);
        Assert.Equal("test-region", plan.RegionName);
        Assert.Equal(3, plan.XNodeCount);
        Assert.Equal(2, plan.YNodeCount);
        Assert.Equal(2, plan.ZNodeCount);
        Assert.Equal(20, plan.Beams.Count);

        var first = plan.Beams[0];

        Assert.Equal("lattice-beam-000001", first.Id);
        Assert.Equal(LatticeBeamAxis.X, first.Axis);
        Assert.Equal(0, first.StartXmm);
        Assert.Equal(0, first.StartYmm);
        Assert.Equal(0, first.StartZmm);
        Assert.Equal(10, first.EndXmm);
        Assert.Equal(0, first.EndYmm);
        Assert.Equal(0, first.EndZmm);
        Assert.Equal(1, first.BeamRadiusMm);

        Assert.Contains(plan.Warnings, warning => warning.Contains("contracts only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlanSimpleGrid_WithNonDivisibleRegion_IncludesMaxBoundary()
    {
        var planner = new LatticePlanner();
        var request = CreateValidRequest() with
        {
            MinXmm = 0,
            MinYmm = 0,
            MinZmm = 0,
            MaxXmm = 25,
            MaxYmm = 10,
            MaxZmm = 10,
            CellSizeMm = 10,
            BeamRadiusMm = 1
        };

        var plan = planner.PlanSimpleGrid(request);

        Assert.Equal(4, plan.XNodeCount);
        Assert.Contains(plan.Beams, beam => beam.EndXmm == 25);
    }

    [Fact]
    public void PlanSimpleGrid_WithInvalidRequest_Throws()
    {
        var planner = new LatticePlanner();
        var request = CreateValidRequest() with
        {
            RegionName = string.Empty
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            planner.PlanSimpleGrid(request));

        Assert.Contains("Invalid lattice region request", exception.Message);
    }

    private static LatticeRegionRequest CreateValidRequest()
    {
        return new LatticeRegionRequest(
            RegionName: "test-region",
            OutputDirectory: "output",
            MinXmm: 0,
            MinYmm: 0,
            MinZmm: 0,
            MaxXmm: 20,
            MaxYmm: 10,
            MaxZmm: 10,
            CellSizeMm: 10,
            BeamRadiusMm: 1,
            TargetRelativeDensity: 0.2m);
    }
}