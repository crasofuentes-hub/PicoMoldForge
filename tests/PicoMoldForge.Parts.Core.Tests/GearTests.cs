using PicoMoldForge.Parts.Gears;
using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Core.Tests;

public sealed class GearSpecTests
{
    [Fact]
    public void SpurGear_StandardDimensions()
    {
        var spec = GearSpec.Spur(2, 24);

        Assert.Equal(24.0, spec.PitchRadius, 12);
        Assert.Equal(24.0 * Math.Cos(20 * Math.PI / 180), spec.BaseRadius, 12);
        Assert.Equal(26.0, spec.TipRadius, 12);
        Assert.Equal(21.5, spec.RootRadius, 12);
        Assert.Equal(Math.PI, spec.PitchToothThickness, 12);
        Assert.Empty(spec.Validate());
    }

    [Fact]
    public void HelicalGear_UsesTransverseModuleAndPressureAngle()
    {
        var spec = GearSpec.Helical(2, 24, 20);
        var beta = 20 * Math.PI / 180;

        Assert.Equal(2 / Math.Cos(beta), spec.TransverseModule, 12);
        Assert.Equal(Math.Atan(Math.Tan(20 * Math.PI / 180) / Math.Cos(beta)), spec.TransversePressureAngleRad, 12);
        Assert.Equal(Math.Tan(beta) / spec.PitchRadius, spec.TwistRadPerMm, 12);
    }

    [Fact]
    public void ProfileShift_ThickensTheToothByTwoXmTanAlpha()
    {
        var plain = GearSpec.Spur(2, 24);
        var shifted = GearSpec.Spur(2, 24, 20, 0.3);

        Assert.Equal(2 * 0.3 * 2 * Math.Tan(20 * Math.PI / 180), shifted.PitchToothThickness - plain.PitchToothThickness, 12);
        Assert.Equal(plain.TipRadius + 0.6, shifted.TipRadius, 12);
    }

    [Theory]
    [InlineData(0, 24, 20)]
    [InlineData(2, 3, 20)]
    [InlineData(2, 24, 5)]
    public void Validate_RejectsInvalidGears(double module, int teeth, double pressureAngle)
    {
        Assert.NotEmpty(new GearSpec(module, teeth, pressureAngle, 0, 0, 0, GearToothSystem.Iso53ProfileA).Validate());
    }
}

public sealed class GearToothProfileTests
{
    private static readonly Lazy<GearToothProfile> Standard = new(() => new GearToothProfile(GearSpec.Spur(2, 24)));

    [Fact]
    public void PitchToothThickness_MatchesTheStandardFormula()
    {
        var spec = GearSpec.Spur(2, 24);
        var measured = 2 * spec.PitchRadius * HalfAngleAt(Standard.Value, spec.Teeth, spec.PitchRadius);

        Assert.InRange(measured - spec.PitchToothThickness, -0.002, 0.002);
    }

    [Theory]
    [InlineData(0.15)]
    [InlineData(0.5)]
    [InlineData(0.85)]
    public void Flank_IsAnInvolute(double fraction)
    {
        var spec = GearSpec.Spur(2, 24);
        var start = Math.Max(spec.BaseRadius, spec.RootRadius);
        var rho = start + (fraction * (spec.TipRadius - start));
        var arcError = rho * (HalfAngleAt(Standard.Value, spec.Teeth, rho) - spec.HalfToothAngleAt(rho));

        Assert.InRange(arcError, -0.002, 0.002);
    }

    [Fact]
    public void TipAndRootCircles_AreOnTheSurface()
    {
        var spec = GearSpec.Spur(2, 24);
        var gap = Math.PI / spec.Teeth;

        Assert.InRange(Standard.Value.Distance(new Vec2(spec.TipRadius, 0)), -0.002, 0.002);
        Assert.InRange(Standard.Value.Distance(new Vec2(spec.RootRadius * Math.Cos(gap), spec.RootRadius * Math.Sin(gap))), -0.002, 0.002);
    }

    [Fact]
    public void Raster_MatchesTheExactGeneratedDistance()
    {
        var spec = GearSpec.Spur(2, 24);
        var random = new Random(7);
        var worst = 0.0;

        for (var i = 0; i < 400; i++)
        {
            var rho = spec.RootRadius - 0.8 + (random.NextDouble() * (spec.TipRadius - spec.RootRadius + 1.6));
            var angle = random.NextDouble() * 2 * Math.PI;
            var q = new Vec2(rho * Math.Cos(angle), rho * Math.Sin(angle));
            worst = Math.Max(worst, Math.Abs(Standard.Value.Distance(q) - Standard.Value.ExactDistance(q)));
        }

        Assert.True(worst < 0.01, $"worst raster error {worst} mm");
    }

    [Fact]
    public void ProfileShift_IsGeneratedByTheShiftedRack()
    {
        var spec = GearSpec.Spur(2, 24, 20, 0.3);
        var profile = new GearToothProfile(spec, 2.0 / 40);
        var measured = 2 * spec.PitchRadius * HalfAngleAt(profile, spec.Teeth, spec.PitchRadius);

        Assert.InRange(measured - spec.PitchToothThickness, -0.003, 0.003);
    }

    [Fact]
    public void ThicknessAllowance_ThinsTheTooth()
    {
        var spec = GearSpec.Spur(2, 24, 20, 0, 0.1);
        var profile = new GearToothProfile(spec, 2.0 / 40);
        var measured = 2 * spec.PitchRadius * HalfAngleAt(profile, spec.Teeth, spec.PitchRadius);

        Assert.InRange(measured - (Math.PI - 0.1), -0.003, 0.003);
    }

    [Fact]
    public void SmallGear_WithUndercut_KeepsItsTeethAndPitchThickness()
    {
        var spec = GearSpec.Spur(1, 10);
        var profile = new GearToothProfile(spec, 1.0 / 40);
        var crossings = 0;
        var previous = profile.Distance(new Vec2(spec.PitchRadius, 0)) < 0;

        for (var i = 1; i <= 7200; i++)
        {
            var angle = 2 * Math.PI * i / 7200;
            var inside = profile.Distance(new Vec2(spec.PitchRadius * Math.Cos(angle), spec.PitchRadius * Math.Sin(angle))) < 0;
            crossings += inside != previous ? 1 : 0;
            previous = inside;
        }

        Assert.Equal(20, crossings);
        var measured = 2 * spec.PitchRadius * HalfAngleAt(profile, spec.Teeth, spec.PitchRadius);
        Assert.InRange(measured - spec.PitchToothThickness, -0.003, 0.003);
    }

    internal static double HalfAngleAt(GearToothProfile profile, int teeth, double radius)
    {
        var a = 0.0;
        var b = Math.PI / teeth;

        for (var i = 0; i < 80; i++)
        {
            var m = (a + b) / 2;

            if (profile.Distance(new Vec2(radius * Math.Cos(m), radius * Math.Sin(m))) < 0)
            {
                a = m;
            }
            else
            {
                b = m;
            }
        }

        return (a + b) / 2;
    }
}
