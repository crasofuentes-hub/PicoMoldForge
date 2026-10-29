using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Core.Tests;

public sealed class AxisConventionTests
{
    private const double Tolerance = 1e-12;

    [Theory]
    [InlineData(RotaryAxis.A, 0, 1, 0, 0, 0, 1)]
    [InlineData(RotaryAxis.B, 0, 0, 1, 1, 0, 0)]
    [InlineData(RotaryAxis.C, 1, 0, 0, 0, 1, 0)]
    public void PositiveQuarterTurn_FollowsTheRightHandRule(RotaryAxis axis, double x, double y, double z, double ex, double ey, double ez)
    {
        var rotated = AxisConvention.Rotation(axis, Math.PI / 2) * new Vec3(x, y, z);

        AssertClose(new Vec3(ex, ey, ez), rotated);
    }

    [Fact]
    public void RotaryAxes_RotateAboutTheirIsoLinearAxes()
    {
        Assert.Equal(LinearAxis.X, AxisConvention.AxisOf(RotaryAxis.A));
        Assert.Equal(LinearAxis.Y, AxisConvention.AxisOf(RotaryAxis.B));
        Assert.Equal(LinearAxis.Z, AxisConvention.AxisOf(RotaryAxis.C));
        Assert.Equal(RotaryAxis.C, AxisConvention.RotaryAbout(LinearAxis.Z));
    }

    [Theory]
    [InlineData(LinearAxis.X)]
    [InlineData(LinearAxis.Y)]
    [InlineData(LinearAxis.Z)]
    public void ProfileFrames_AreRightHanded(LinearAxis axis)
    {
        var frame = AxisConvention.Frame(axis);

        AssertClose(frame.W, Vec3.Cross(frame.U, frame.V));
        AssertClose(AxisConvention.Unit(axis), frame.W);
    }

    [Fact]
    public void Orientation_AppliesAThenBThenC_AndOrderMatters()
    {
        // Documented order: R = Rz(C) * Ry(B) * Rx(A). With A = 90 and B = 90, +Y goes to +Z (A) and then to +X (B).
        var documented = new Orientation(90, 90, 0).ToMatrix() * Vec3.UnitY;
        AssertClose(Vec3.UnitX, documented);

        // Applying B first and A second sends +Y to +Z instead: the order is part of the contract.
        var reversed = AxisConvention.Rotation(RotaryAxis.A, Math.PI / 2) * (AxisConvention.Rotation(RotaryAxis.B, Math.PI / 2) * Vec3.UnitY);
        AssertClose(Vec3.UnitZ, reversed);
    }

    [Theory]
    [InlineData("x", true)]
    [InlineData(" Z ", true)]
    [InlineData("C", false)]
    [InlineData("", false)]
    public void TryParseLinear_AcceptsOnlyXyz(string text, bool expected)
    {
        Assert.Equal(expected, AxisConvention.TryParseLinear(text, out _));
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("C", true)]
    [InlineData("Z", false)]
    public void TryParseRotary_AcceptsOnlyAbc(string text, bool expected)
    {
        Assert.Equal(expected, AxisConvention.TryParseRotary(text, out _));
    }

    private static void AssertClose(Vec3 expected, Vec3 actual)
    {
        Assert.True((expected - actual).Length < Tolerance, $"expected {expected}, actual {actual}");
    }
}
