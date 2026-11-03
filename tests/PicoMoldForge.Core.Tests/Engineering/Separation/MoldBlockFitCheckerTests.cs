using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Tests.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.Separation;

public sealed class MoldBlockFitCheckerTests
{
    private static readonly MoldSeparationBounds Block = new(-25m, -25m, -25m, 125m, 85m, 55m);

    [Fact]
    public void PartWellInside_MeetsTheMargin_AndNamesTheClosestFace()
    {
        var fit = MoldBlockFitChecker.Check(Block, new MoldSeparationBounds(0m, 0m, 0m, 20m, 10m, 5m), 20m);

        Assert.True(fit.IsPartInsideBlock);
        Assert.True(fit.MeetsRequiredMargin);
        Assert.Equal(25m, fit.MinimumMarginMm);
        Assert.Equal("-X", fit.MinimumMarginSide);
    }

    [Fact]
    public void PartCloseToAFace_IsInsideButBelowTheMargin()
    {
        var fit = MoldBlockFitChecker.Check(Block, new MoldSeparationBounds(0m, 0m, 0m, 20m, 10m, 50m), 20m);

        Assert.True(fit.IsPartInsideBlock);
        Assert.False(fit.MeetsRequiredMargin);
        Assert.Equal(5m, fit.MinimumMarginMm);
        Assert.Equal("+Z", fit.MinimumMarginSide);
    }

    [Theory]
    [InlineData(130, -5)]
    [InlineData(125, 0)]
    public void PartReachingOrCrossingAFace_IsNotInside(int maxX, int margin)
    {
        var fit = MoldBlockFitChecker.Check(Block, new MoldSeparationBounds(0m, 0m, 0m, maxX, 10m, 5m), 0m);

        Assert.False(fit.IsPartInsideBlock);
        Assert.False(fit.MeetsRequiredMargin);
        Assert.Equal(margin, fit.MinimumMarginMm);
        Assert.Equal("+X", fit.MinimumMarginSide);
    }

    [Fact]
    public void MeshBounds_OfTheGeneratorTestBox()
    {
        var bounds = MeshBounds.Of(MeshWallThicknessSamplerTests.GeneratorTestBox());

        Assert.Equal(new MeshVertex(0, 0, 0), bounds.Min);
        Assert.Equal(new MeshVertex(20, 10, 5), bounds.Max);
        Assert.Equal(new MeshVertex(10, 5, 2.5), bounds.Center);
        Assert.Throws<ArgumentException>(() => MeshBounds.Of(Array.Empty<MeshTriangle>()));
    }
}
