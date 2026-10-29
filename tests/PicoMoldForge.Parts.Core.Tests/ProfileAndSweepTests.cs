using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Core.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void Rectangle_WithRoundedCorners_IsExact()
    {
        var rectangle = new RectangleProfile(new Vec2(-10, -5), new Vec2(10, 5), 2);

        Assert.Equal(-5, rectangle.Distance(Vec2.Zero), 12);
        Assert.Equal(1, rectangle.Distance(new Vec2(11, 0)), 12);
        // Corner arc center is (8, 3) with radius 2: a point on the diagonal 1 mm beyond the arc.
        var onDiagonal = new Vec2(8, 3) + (new Vec2(1, 1) * ((2 + 1) / Math.Sqrt(2)));
        Assert.Equal(1, rectangle.Distance(onDiagonal), 12);
    }

    [Fact]
    public void Polygon_EvenOddLoops_MakeHoles()
    {
        var outer = new[] { new Vec2(-10, -10), new Vec2(10, -10), new Vec2(10, 10), new Vec2(-10, 10) };
        var hole = new[] { new Vec2(-4, -4), new Vec2(4, -4), new Vec2(4, 4), new Vec2(-4, 4) };
        var profile = new PolygonProfile(new IReadOnlyList<Vec2>[] { outer, hole });

        Assert.Equal(4, profile.Distance(Vec2.Zero), 12);
        Assert.Equal(-3, profile.Distance(new Vec2(7, 0)), 12);
        Assert.Equal(2, profile.Distance(new Vec2(12, 0)), 12);
    }

    [Fact]
    public void HalfSection_IsMirroredAboutTheAxis()
    {
        var profile = PolygonProfile.FromHalfSection(new[] { new Vec2(0, 0), new Vec2(20, 0), new Vec2(20, 10), new Vec2(0, 10) });

        Assert.Equal(-5, profile.Distance(new Vec2(0, 5)), 12);
        Assert.Equal(profile.Distance(new Vec2(7, 3)), profile.Distance(new Vec2(-7, 3)), 12);
        Assert.Throws<ArgumentException>(() => PolygonProfile.FromHalfSection(new[] { new Vec2(1, 0), new Vec2(20, 0), new Vec2(0, 10) }));
    }

    [Fact]
    public void Difference_RemovesTheCut()
    {
        var plate = new RectangleProfile(new Vec2(-10, -10), new Vec2(10, 10));
        var hole = new CircleProfile(Vec2.Zero, 3);
        var difference = new ProfileDifference(plate, hole);

        Assert.True(difference.Distance(Vec2.Zero) > 0);
        Assert.True(difference.Distance(new Vec2(6, 0)) < 0);
    }
}

public sealed class SweepTests
{
    [Fact]
    public void LinearSweep_AlongZ_IsAnExactExtrusion()
    {
        var sweep = new LinearSweepSdf(new RectangleProfile(new Vec2(-10, -5), new Vec2(10, 5)), LinearAxis.Z, 0, 8, 0);

        Assert.Equal(-4, sweep.Distance(new Vec3(0, 0, 4)), 12);
        Assert.Equal(2, sweep.Distance(new Vec3(0, 0, 10)), 12);
        Assert.Equal(Math.Sqrt(2), sweep.Distance(new Vec3(11, 0, 9)), 12);
        Assert.Equal(new Vec3(-10, -5, 0), sweep.Bounds.Min);
        Assert.Equal(new Vec3(10, 5, 8), sweep.Bounds.Max);
    }

    [Fact]
    public void LinearSweep_AlongX_UsesTheYzFrame()
    {
        // Frame for X is (U, V, W) = (Y, Z, X): the profile's first coordinate is world Y.
        var sweep = new LinearSweepSdf(new CircleProfile(new Vec2(5, 0), 1), LinearAxis.X, 0, 10, 0);

        Assert.True(sweep.Distance(new Vec3(5, 5, 0)) < 0);
        Assert.True(sweep.Distance(new Vec3(5, 0, 5)) > 0);
    }

    [Fact]
    public void TwistedSweep_RotatesTheProfileRightHanded()
    {
        // Twist +90 degrees from z = 0 to z = 10 (Z + C): a feature at +X ends at +Y.
        var profile = new CircleProfile(new Vec2(5, 0), 1);
        var sweep = new LinearSweepSdf(profile, LinearAxis.Z, 0, 10, Math.PI / 2);

        Assert.True(sweep.Distance(new Vec3(5, 0, 0.01)) < 0);
        Assert.True(sweep.Distance(new Vec3(0, 5, 9.99)) < 0);
        Assert.True(sweep.Distance(new Vec3(5, 0, 9.99)) > 0);
        Assert.Equal(Math.PI / 4, sweep.TwistAt(5), 12);
        Assert.True(sweep.Lipschitz > 1);
    }

    [Fact]
    public void TwistedSweep_IsOneLipschitzInsideTheTwistCylinder()
    {
        // Profile bounds [2,6] x [-1,1]: twist radius sqrt(37). Half a turn over 10 mm is a strong twist
        // (shear 1.91), where a sqrt(1 + c^2) normalization would be about 8 % too small.
        var profile = new RectangleProfile(new Vec2(2, -1), new Vec2(6, 1));
        var sweep = new LinearSweepSdf(profile, LinearAxis.Z, 0, 10, Math.PI);
        var random = new Random(3);

        for (var i = 0; i < 20000; i++)
        {
            var radius = random.NextDouble() * 5.7;
            var angle = random.NextDouble() * 2 * Math.PI;
            var p = new Vec3(radius * Math.Cos(angle), radius * Math.Sin(angle), (random.NextDouble() * 14) - 2);
            var q = p + (new Vec3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalized() * 0.3);

            Assert.True(Math.Abs(sweep.Distance(p) - sweep.Distance(q)) <= ((q - p).Length * (1 + 1e-9)) + 1e-12, $"Lipschitz violated at {p}");
        }
    }

    [Fact]
    public void TwistedSweep_IsALowerBoundOfTheTrueDistance()
    {
        // A centered circle is invariant under the twist, so the exact distance (a cylinder) is known.
        var sweep = new LinearSweepSdf(new CircleProfile(Vec2.Zero, 5), LinearAxis.Z, 0, 10, Math.PI);
        var random = new Random(5);

        for (var i = 0; i < 20000; i++)
        {
            var p = new Vec3((random.NextDouble() * 24) - 12, (random.NextDouble() * 24) - 12, (random.NextDouble() * 16) - 3);
            var radial = Math.Sqrt((p.X * p.X) + (p.Y * p.Y)) - 5;
            var axial = Math.Max(-p.Z, p.Z - 10);
            var exact = Math.Min(Math.Max(radial, axial), 0) + Math.Sqrt((Math.Max(radial, 0) * Math.Max(radial, 0)) + (Math.Max(axial, 0) * Math.Max(axial, 0)));
            var value = sweep.Distance(p);

            Assert.Equal(exact < 0, value < 0);
            Assert.True(Math.Abs(value) <= Math.Abs(exact) + 1e-12, $"overestimate at {p}: {value} vs {exact}");
        }
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(1.0)]
    [InlineData(3.0)]
    public void TwistStretch_IsTheLargestSingularValueOfTheTwistJacobian(double shear)
    {
        // Jacobian of (x, w) -> (Rot(k w) x, w) at radius r with k r = shear, written in the rotated frame:
        // [[1, 0, 0], [0, 1, shear], [0, 0, 1]]. Its largest singular value via power iteration on J^T J.
        var v = new[] { 0.3, 0.5, 0.8 };

        for (var i = 0; i < 500; i++)
        {
            var jv = new[] { v[0], v[1] + (shear * v[2]), v[2] };
            var jtjv = new[] { jv[0], jv[1], (shear * jv[1]) + jv[2] };
            var norm = Math.Sqrt((jtjv[0] * jtjv[0]) + (jtjv[1] * jtjv[1]) + (jtjv[2] * jtjv[2]));
            v = new[] { jtjv[0] / norm, jtjv[1] / norm, jtjv[2] / norm };
        }

        var image = new[] { v[0], v[1] + (shear * v[2]), v[2] };
        var sigma = Math.Sqrt((image[0] * image[0]) + (image[1] * image[1]) + (image[2] * image[2]));
        Assert.Equal((shear / 2) + Math.Sqrt(1 + (shear * shear / 4)), sigma, 9);

        // The sweep reports profile Lipschitz times that stretch at its twist radius.
        var profile = new CircleProfile(new Vec2(3, 4), 1e-3);
        var twistRadius = profile.Bounds.MaxRadiusFromOrigin() * (1 + 1e-9) + 1e-9;
        var sweep = new LinearSweepSdf(profile, LinearAxis.Z, 0, 10, shear / twistRadius * 10);
        Assert.Equal((shear / 2) + Math.Sqrt(1 + (shear * shear / 4)), sweep.Lipschitz, 6);
    }

    [Fact]
    public void RotarySweep_AboutC_MakesARingExactly()
    {
        // Rectangle radial 10..20, axial 0..5 turned about C (Z): a washer.
        var sweep = new RotarySweepSdf(new RectangleProfile(new Vec2(10, 0), new Vec2(20, 5)), RotaryAxis.C, 0, 2 * Math.PI);

        Assert.True(sweep.IsFullTurn);
        Assert.Equal(-2.5, sweep.Distance(new Vec3(0, 15, 2.5)), 12);
        Assert.Equal(5, sweep.Distance(new Vec3(5, 0, 2.5)), 12);
        Assert.Equal(1, sweep.Distance(new Vec3(-15, 0, 6)), 12);
    }

    [Fact]
    public void RotarySweep_PartialTurn_IsAWedge()
    {
        // Quarter turn from +X toward +Y (right-hand about Z).
        var sweep = new RotarySweepSdf(new RectangleProfile(new Vec2(10, 0), new Vec2(20, 5)), RotaryAxis.C, 0, Math.PI / 2);

        Assert.True(sweep.Distance(new Vec3(10.6, 10.6, 2.5)) < 0);
        Assert.True(sweep.Distance(new Vec3(-10.6, 10.6, 2.5)) > 0);
        Assert.True(sweep.Distance(new Vec3(10.6, -10.6, 2.5)) > 0);
    }
}

public sealed class CsgTests
{
    [Fact]
    public void Csg_MatchesTheBruteForceFold_WithCulling()
    {
        var block = new LinearSweepSdf(new RectangleProfile(new Vec2(-20, -20), new Vec2(20, 20)), LinearAxis.Z, 0, 10, 0);
        var hole = new LinearSweepSdf(new CircleProfile(Vec2.Zero, 5), LinearAxis.Z, 11, -1, 0);
        var far = new TransformedSdf(new SphereSdf(Vec3.Zero, 3), RigidTransform.FromTranslation(new Vec3(40, 0, 5)));
        var limit = new LinearSweepSdf(new RectangleProfile(new Vec2(-30, -30), new Vec2(30, 30)), LinearAxis.Z, -5, 8, 0);
        var csg = new CsgSdf(new (CsgOperation, ISdf)[]
        {
            (CsgOperation.Add, block),
            (CsgOperation.Subtract, hole),
            (CsgOperation.Add, far),
            (CsgOperation.Intersect, limit)
        });
        var random = new Random(11);

        for (var i = 0; i < 5000; i++)
        {
            var p = new Vec3((random.NextDouble() * 100) - 50, (random.NextDouble() * 60) - 30, (random.NextDouble() * 30) - 10);
            var expected = Math.Max(Math.Min(Math.Max(block.Distance(p), -hole.Distance(p)), far.Distance(p)), limit.Distance(p));

            Assert.Equal(expected, csg.Distance(p), 12);
        }
    }

    [Fact]
    public void Csg_FirstStepMustAdd()
    {
        var sphere = new SphereSdf(Vec3.Zero, 1);

        Assert.ThrowsAny<ArgumentException>(() => new CsgSdf(new (CsgOperation, ISdf)[] { (CsgOperation.Subtract, sphere) }));
    }
}
