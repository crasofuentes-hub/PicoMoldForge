using PicoMoldForge.Core.Parting;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class PartingPlaneEngineTests
{
    [Fact]
    public void CalculateAutomatic_WithDominantX_SelectsXAxisAndCenterOffset()
    {
        var engine = new PartingPlaneEngine();
        var boundingBox = new PartingBoundingBox(
            MinX: 0,
            MinY: 0,
            MinZ: 0,
            MaxX: 100,
            MaxY: 50,
            MaxZ: 20);

        var result = engine.CalculateAutomatic(boundingBox);

        Assert.Equal(PartingPlaneMode.Automatic, result.Mode);
        Assert.Equal(PartingAxis.X, result.Axis);
        Assert.Equal(50.0f, result.PlaneOffsetMm);
        Assert.Equal(1.0f, result.OpeningDirection.X);
        Assert.Equal(0.0f, result.OpeningDirection.Y);
        Assert.Equal(0.0f, result.OpeningDirection.Z);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void CalculateAutomatic_WithDominantY_SelectsYAxisAndCenterOffset()
    {
        var engine = new PartingPlaneEngine();
        var boundingBox = new PartingBoundingBox(
            MinX: -10,
            MinY: 5,
            MinZ: 0,
            MaxX: 10,
            MaxY: 85,
            MaxZ: 15);

        var result = engine.CalculateAutomatic(boundingBox);

        Assert.Equal(PartingAxis.Y, result.Axis);
        Assert.Equal(45.0f, result.PlaneOffsetMm);
        Assert.Equal(0.0f, result.OpeningDirection.X);
        Assert.Equal(1.0f, result.OpeningDirection.Y);
        Assert.Equal(0.0f, result.OpeningDirection.Z);
    }

    [Fact]
    public void CalculateAutomatic_WithDominantZ_SelectsZAxisAndCenterOffset()
    {
        var engine = new PartingPlaneEngine();
        var boundingBox = new PartingBoundingBox(
            MinX: 0,
            MinY: 0,
            MinZ: -20,
            MaxX: 10,
            MaxY: 20,
            MaxZ: 80);

        var result = engine.CalculateAutomatic(boundingBox);

        Assert.Equal(PartingAxis.Z, result.Axis);
        Assert.Equal(30.0f, result.PlaneOffsetMm);
        Assert.Equal(0.0f, result.OpeningDirection.X);
        Assert.Equal(0.0f, result.OpeningDirection.Y);
        Assert.Equal(1.0f, result.OpeningDirection.Z);
    }

    [Fact]
    public void CalculateAutomatic_WithEqualDimensions_UsesDeterministicTieBreakX()
    {
        var engine = new PartingPlaneEngine();
        var boundingBox = new PartingBoundingBox(
            MinX: 0,
            MinY: 0,
            MinZ: 0,
            MaxX: 10,
            MaxY: 10,
            MaxZ: 10);

        var result = engine.CalculateAutomatic(boundingBox);

        Assert.Equal(PartingAxis.X, result.Axis);
        Assert.Contains(result.Warnings, warning => warning.Contains("tie-break", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CalculateAutomatic_WithInvalidBoundingBox_Throws()
    {
        var engine = new PartingPlaneEngine();
        var boundingBox = new PartingBoundingBox(
            MinX: 0,
            MinY: 0,
            MinZ: 0,
            MaxX: 0,
            MaxY: 10,
            MaxZ: 10);

        var exception = Assert.Throws<ArgumentException>(() => engine.CalculateAutomatic(boundingBox));

        Assert.Contains("Invalid bounding box", exception.Message);
    }
}