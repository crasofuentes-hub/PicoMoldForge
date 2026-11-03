using PicoMoldForge.Core.Engineering.Shrinkage;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Tests.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.Shrinkage;

public sealed class MoldShrinkageResolverTests
{
    [Fact]
    public void WithoutResin_KeepsTheMaterialRate()
    {
        var shrinkage = MoldShrinkageResolver.Resolve(null, null, 0.011m);

        Assert.Equal(0.011m, shrinkage.Rate);
        Assert.Equal("material.shrinkageRate", shrinkage.Source);
        Assert.StartsWith("Shrinkage 1.1 % taken from material.shrinkageRate", shrinkage.Note);
    }

    [Theory]
    [InlineData("ABS", 0.005)]
    [InlineData("polypropylene", 0.018)]
    [InlineData("PA66", 0.015)]
    [InlineData("Acetal", 0.020)]
    public void KnownResinWithoutRate_UsesItsRecommendedRate(string name, double expected)
    {
        var shrinkage = MoldShrinkageResolver.Resolve(name, null, 0.011m);

        Assert.Equal((decimal)expected, shrinkage.Rate);
        Assert.Equal("resin (recommended rate)", shrinkage.Source);
    }

    [Fact]
    public void RateOutsideTheResinRange_IsFlagged()
    {
        var inside = MoldShrinkageResolver.Resolve("ABS", 0.006m, 0.011m);
        var outside = MoldShrinkageResolver.Resolve("ABS", 0.011m, 0.011m);

        Assert.Equal("Shrinkage 0.6 % from resin ABS (typical 0.4 % to 0.7 %).", inside.Note);
        Assert.Equal("Shrinkage 1.1 % from resin ABS is outside its typical 0.4 % to 0.7 %: check the resin datasheet.", outside.Note);
    }

    [Fact]
    public void UnknownResin_NeedsARate()
    {
        Assert.Throws<ArgumentException>(() => MoldShrinkageResolver.Resolve("Mystery", null, 0.011m));
        Assert.Equal(0.004m, MoldShrinkageResolver.Resolve("Mystery", 0.004m, 0.011m).Rate);
    }

    [Fact]
    public void BinaryStl_WrittenAndReadBack_KeepsTheTriangles()
    {
        var box = MeshWallThicknessSamplerTests.GeneratorTestBox();
        using var stream = new MemoryStream();

        BinaryStlTriangleWriter.Write(stream, box);
        stream.Position = 0;
        var read = BinaryStlTriangleReader.Read(stream);

        Assert.Equal(84 + (50 * box.Count), stream.Length);
        Assert.Equal(box, read);
    }
}
