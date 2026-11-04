using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.MoldLayout;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Tests.Engineering.MoldAccess;
using PicoMoldForge.Core.Tests.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.MoldLayout;

public sealed class MoldLayoutPlannerTests
{
    // 40 mm of steel around a 20 x 10 x 5 box: room for every feature.
    private static readonly MoldLayoutBlock RoomyBlock = new(-40, -40, -40, 60, 50, 45);

    [Fact]
    public void Box_SplitAtItsBottomFace_GateReachesTheCavityAndTheFeedKeepsItsClearances()
    {
        var plan = MoldLayoutPlanner.Plan(Request(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 0, RoomyBlock));
        var feed = plan.Feed!;

        // The part sits in the + half (more of its surface), so the sprue goes through the - half.
        Assert.Equal(1, plan.Halves.MovingHalf);
        Assert.True(feed.IsFeasible, string.Join("; ", feed.Problems));
        Assert.True(feed.GateReachesCavity);
        Assert.True(feed.GateClearBeyondEntry);

        // Edge gate on a side wall, on the plane, entering along the wall normal a section as thick as the box.
        Assert.Equal(0, feed.GatePoint.Z, 9);
        Assert.True(OnSideWall(feed.GatePoint), $"gate at {feed.GatePoint}");
        Assert.Equal(1, Math.Abs(feed.OutwardDirection.X) + Math.Abs(feed.OutwardDirection.Y), 9);
        Assert.InRange(feed.EntryThicknessMm, 4.99, 5.0001);

        var gate = feed.Segments.Single(s => s.Type == GateSystemFeatureType.Gate);
        var runner = feed.Segments.Single(s => s.Type == GateSystemFeatureType.Runner);
        var sprue = feed.Segments.Single(s => s.Type == GateSystemFeatureType.Sprue);
        Assert.InRange(gate.DiameterMm, 2.99, 3.0001);
        Assert.Equal(6.5, runner.DiameterMm, 9);
        Assert.Equal(7.5, sprue.DiameterMm, 9);

        // Next to the gate the runner is one gate land from the cavity; the sprue is farther.
        Assert.Equal(1.0, runner.CavityClearanceMm, 6);
        Assert.True(sprue.CavityClearanceMm >= 0.5);
        Assert.Equal(-40, sprue.End.Z, 9);
        Assert.All(feed.Segments, s =>
        {
            Assert.True(Inside(RoomyBlock, s.Start) && Inside(RoomyBlock, s.End), s.Id);
            Assert.True(s.MoldEdgeClearanceMm >= 10, s.Id);
        });
    }

    [Fact]
    public void Box_SplitAtItsBottomFace_PinsLandOnTheTopFaceInsideItsOutlineAndApart()
    {
        var plan = MoldLayoutPlanner.Plan(Request(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 0, RoomyBlock));

        // The 20 x 10 face takes three 4 mm pins 0.5 mm inside its outline and 8 mm apart, not four.
        Assert.Equal(3, plan.Pins.Count);
        Assert.Equal(4, plan.RequestedPinCount);
        Assert.Equal(8, plan.PinSpacingMm, 9);
        Assert.All(plan.Pins, pin =>
        {
            Assert.Equal(5, pin.Tip.Z, 6);
            Assert.InRange(pin.Tip.X, 2.5 - 1e-6, 17.5 + 1e-6);
            Assert.InRange(pin.Tip.Y, 2.5 - 1e-6, 7.5 + 1e-6);
            Assert.Equal(45, pin.End.Z, 9);
            Assert.Equal(5, pin.StrokeMm, 6);
            Assert.True(pin.FeedClearanceMm >= 3, pin.Id);
        });

        for (var a = 0; a < plan.Pins.Count; a++)
        {
            for (var b = a + 1; b < plan.Pins.Count; b++)
            {
                Assert.True(Planar(plan.Pins[a].Tip, plan.Pins[b].Tip) >= 8 - 1e-9);
            }
        }
    }

    [Fact]
    public void Box_Cooling_KeepsTheCavityClearanceAndPassesNoPinNorTheFeed()
    {
        var plan = MoldLayoutPlanner.Plan(Request(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 0, RoomyBlock));

        // The - half (fixed) also holds the sprue and the guide bores: two of its five positions are left.
        Assert.Equal(3, plan.Channels.Count(c => c.Half == 1));
        Assert.Equal(2, plan.Channels.Count(c => c.Half == -1));
        Assert.Contains(plan.Notes, n => n.Contains("would pass 0 mm from a guide pin", StringComparison.Ordinal));

        foreach (var channel in plan.Channels)
        {
            // Along X, through the block; distance to the box worked out by hand.
            Assert.Equal(-40, channel.Start.X, 9);
            Assert.Equal(60, channel.End.X, 9);
            var dy = Math.Max(0, Math.Max(-channel.Start.Y, channel.Start.Y - 10));
            var dz = Math.Max(0, Math.Max(-channel.Start.Z, channel.Start.Z - 5));
            Assert.True(Math.Sqrt((dy * dy) + (dz * dz)) - 3 >= 10, channel.Id);
            Assert.Equal(Math.Sqrt((dy * dy) + (dz * dz)) - 3, channel.CavityClearanceMm, 6);

            foreach (var pin in plan.Pins)
            {
                Assert.True(MoldLayoutPlanner.Distance(channel.Start, channel.End, pin.Tip, pin.End) - 3 - 2 >= 3, $"{channel.Id} {pin.Id}");
            }

            foreach (var segment in plan.Feed!.Segments)
            {
                Assert.True(MoldLayoutPlanner.Distance(channel.Start, channel.End, segment.Start, segment.End) - 3 - (segment.DiameterMm / 2) >= 3, $"{channel.Id} {segment.Id}");
            }

            foreach (var guide in plan.GuidePins)
            {
                Assert.True(MoldLayoutPlanner.Distance(channel.Start, channel.End, guide.Base, guide.BoreEnd) - 3 - (guide.BoreDiameterMm / 2) >= 3, $"{channel.Id} {guide.Id}");
            }
        }
    }

    [Fact]
    public void Box_ThinBlock_LeavesTheHalfWithoutCoolingAndSaysWhy()
    {
        // 25 mm behind the cavity; a channel needs 10 + 6 + 0.1 + 10 mm.
        var plan = MoldLayoutPlanner.Plan(Request(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 0, new MoldLayoutBlock(-25, -25, -25, 45, 35, 30)));

        Assert.Empty(plan.Channels);
        Assert.Contains(plan.Notes, n => n.StartsWith("Cooling:", StringComparison.Ordinal) && n.Contains("needs 26.1 mm", StringComparison.Ordinal));
    }

    [Fact]
    public void Tray_ShrinksOntoTheCoreHalf_EvenWithLessCavityArea()
    {
        // 40 x 40 x 3 plate with a 36 x 36 x 1 recess under it, split at the recess ceiling: the recess (a core of
        // the - half) grips the part, although the + half holds more of the surface.
        var plan = MoldLayoutPlanner.Plan(Request(Tray(), PartingAxis.Z, 1, new MoldLayoutBlock(-40, -40, -40, 80, 80, 45)));

        Assert.True(plan.Halves.AreaPositiveMm2 > plan.Halves.AreaNegativeMm2);
        Assert.Equal(-1, plan.Halves.MovingHalf);
        Assert.Contains("shrinks onto it", plan.Halves.Reason, StringComparison.Ordinal);

        // Pins come up through the core onto the recess ceiling.
        Assert.NotEmpty(plan.Pins);
        Assert.All(plan.Pins, pin => Assert.Equal(1, pin.Tip.Z, 6));
    }

    [Fact]
    public void SquareFrame_GateOnTheOuterOutlineAndPinsOnTheRingOnly()
    {
        var plan = MoldLayoutPlanner.Plan(Request(MeshMoldAccessAnalyzerTests.SquareFrame(), PartingAxis.Z, 0, new MoldLayoutBlock(-40, -40, -40, 60, 60, 50)));

        Assert.True(plan.Feed!.IsFeasible, string.Join("; ", plan.Feed.Problems));
        var g = plan.Feed.GatePoint;
        Assert.True(Math.Abs(g.X) < 1e-6 || Math.Abs(g.X - 20) < 1e-6 || Math.Abs(g.Y) < 1e-6 || Math.Abs(g.Y - 20) < 1e-6, $"gate at {g}");

        // The 6 mm ring takes a 4 mm pin with its margin; four fit at the corners, none over the hole.
        Assert.Equal(4, plan.Pins.Count);
        Assert.All(plan.Pins, pin =>
        {
            Assert.Equal(10, pin.Tip.Z, 6);
            Assert.False(pin.Tip.X > 6 - 2.5 + 1e-6 && pin.Tip.X < 14 + 2.5 - 1e-6 && pin.Tip.Y > 6 - 2.5 + 1e-6 && pin.Tip.Y < 14 + 2.5 - 1e-6, $"pin at {pin.Tip}");
        });
    }

    [Fact]
    public void CShape_PinsDoNotLandUnderTheOverhang()
    {
        // Seen along Y: a bottom bar (z 0..4), a web (x 0..8) and a top bar (z 11..15). The top of the bottom bar
        // faces the back plate of the + half but lies under the top bar.
        var shape = MeshWallThicknessSamplerTests.Extrude(
            new[] { (0.0, 0.0), (30.0, 0.0), (30.0, 4.0), (8.0, 4.0), (8.0, 11.0), (30.0, 11.0), (30.0, 15.0), (0.0, 15.0) },
            10,
            new[] { (0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 7), (4, 5, 6), (4, 6, 7) });

        var plan = MoldLayoutPlanner.Plan(Request(shape, PartingAxis.Z, 0, new MoldLayoutBlock(-40, -40, -40, 70, 50, 55)));

        Assert.Equal(1, plan.Halves.MovingHalf);
        Assert.NotEmpty(plan.Pins);
        Assert.All(plan.Pins, pin => Assert.Equal(15, pin.Tip.Z, 6));
    }

    [Fact]
    public void CShapeWithARidgedTop_TakesNoPinUnderTheOverhang()
    {
        // As above with a ridge on top (faces 11 deg off flat): the only flat face towards the back plate of the
        // + half is the top of the bottom bar, and the top bar lies over it.
        var shape = MeshWallThicknessSamplerTests.Extrude(
            new[] { (0.0, 0.0), (30.0, 0.0), (30.0, 4.0), (8.0, 4.0), (8.0, 11.0), (30.0, 11.0), (30.0, 15.0), (15.0, 18.0), (0.0, 15.0) },
            10,
            new[] { (0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 8), (4, 5, 6), (4, 6, 7), (4, 7, 8) });

        var plan = MoldLayoutPlanner.Plan(Request(shape, PartingAxis.Z, 0, new MoldLayoutBlock(-40, -40, -40, 70, 50, 60)));

        Assert.Equal(1, plan.Halves.MovingHalf);
        Assert.Empty(plan.Pins);
        Assert.Contains(plan.Notes, n => n.StartsWith("Ejector pins: 0 of 4", StringComparison.Ordinal));
    }

    [Fact]
    public void Box_SplitAcrossItsLength_PinGoesOnTheEndFaceOfTheMovingHalf()
    {
        // The end-to-end cavity (the box scaled by 1.011) split at X = 10 in the end-to-end block: the halves hold
        // the part alike, so the - half moves; its only face towards its back plate is the 10.11 x 5.055 mm end face,
        // which takes one 4 mm pin with its 0.5 mm margin (0.055 mm to spare across Z).
        var cavity = Scaled(MeshWallThicknessSamplerTests.GeneratorTestBox(), new MeshVertex(10, 5, 2.5), 1.011);
        var plan = MoldLayoutPlanner.Plan(Request(cavity, PartingAxis.X, 10, new MoldLayoutBlock(-25, -25, -25, 125, 85, 55)));

        Assert.Equal(-1, plan.Halves.MovingHalf);
        var pin = Assert.Single(plan.Pins);
        Assert.Equal(-0.11, pin.Tip.X, 9);
        Assert.Equal(-25, pin.End.X, 9);
        Assert.InRange(pin.Tip.Z, -0.0275 + 2.5 - 1e-9, 5.0275 - 2.5 + 1e-9);
        Assert.Equal(10.11, pin.StrokeMm, 6);
        Assert.True(plan.Feed!.IsFeasible, string.Join("; ", plan.Feed.Problems));
    }

    [Fact]
    public void Box_EndFaceExactlyAsTallAsPinAndMargin_TakesNoPin()
    {
        // The unscaled 5 mm face leaves no room around a 4 mm pin with 0.5 mm margin on both sides.
        var plan = MoldLayoutPlanner.Plan(Request(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.X, 10, new MoldLayoutBlock(-25, -25, -25, 125, 85, 55)));

        Assert.Empty(plan.Pins);
        Assert.Contains(plan.Notes, n => n.StartsWith("Ejector pins: 0 of 4", StringComparison.Ordinal) && n.Contains("FAIL", StringComparison.Ordinal));
    }

    [Fact]
    public void Mushroom_PushesTheSprueBeyondTheCapOfTheFixedHalf()
    {
        // A 30 x 30 x 50 block (the moving - half, more surface) with a stem and a 46 x 46 cap on top, 8 mm wider
        // than the block on every side. Split at the top of the block: the runner passes under the cap, the sprue
        // rises through the fixed half and must clear the cap: 8 + 3.75 + 0.5 mm from the block edge at least.
        var plan = MoldLayoutPlanner.Plan(Request(Mushroom(stemHeight: 10), PartingAxis.Z, 50, new MoldLayoutBlock(-40, -40, -40, 70, 70, 100)));
        var feed = plan.Feed!;

        Assert.Equal(-1, plan.Halves.MovingHalf);
        Assert.True(feed.IsFeasible, string.Join("; ", feed.Problems));
        var sprue = feed.Segments.Single(s => s.Type == GateSystemFeatureType.Sprue);
        Assert.InRange(sprue.CavityClearanceMm, 0.5, 1.0);
        var beyondBlock = Math.Max(Math.Max(-sprue.Start.X, sprue.Start.X - 30), Math.Max(-sprue.Start.Y, sprue.Start.Y - 30));
        Assert.True(beyondBlock >= 8 + 3.75 + 0.5, $"sprue at {sprue.Start}");
        Assert.Equal(100, sprue.End.Z, 9);
    }

    [Fact]
    public void Mushroom_CapJustOverThePlane_RunnerHasNoRoomAndTheFeedFails()
    {
        // The cap 2 mm over the plane: the 6.5 mm runner on the plane would cut into it on every side.
        var plan = MoldLayoutPlanner.Plan(Request(Mushroom(stemHeight: 2), PartingAxis.Z, 50, new MoldLayoutBlock(-40, -40, -40, 70, 70, 100)));

        Assert.False(plan.Feed!.IsFeasible);
        Assert.Contains(plan.Feed.Problems, p => p.Contains("runner", StringComparison.Ordinal));
        Assert.Contains(plan.Notes, n => n.StartsWith("Feed:", StringComparison.Ordinal) && n.Contains("FAIL", StringComparison.Ordinal));
    }

    [Fact]
    public void Mushroom_CapOverTheGate_GateMeetsTheCavityAgain()
    {
        // The cap 1 mm over the plane comes down onto the 3 mm gate beyond its entry; its axis alone would not see it.
        var plan = MoldLayoutPlanner.Plan(Request(Mushroom(stemHeight: 1), PartingAxis.Z, 50, new MoldLayoutBlock(-40, -40, -40, 70, 70, 100)));

        Assert.False(plan.Feed!.GateClearBeyondEntry);
        Assert.False(plan.Feed.IsFeasible);
    }

    [Fact]
    public void Layout_IsDeterministic()
    {
        var first = MoldLayoutPlanner.Plan(Request(MeshMoldAccessAnalyzerTests.SquareFrame(), PartingAxis.Z, 0, new MoldLayoutBlock(-40, -40, -40, 60, 60, 50)));
        var second = MoldLayoutPlanner.Plan(Request(MeshMoldAccessAnalyzerTests.SquareFrame(), PartingAxis.Z, 0, new MoldLayoutBlock(-40, -40, -40, 60, 60, 50)));

        Assert.Equal(first.Notes, second.Notes);
        Assert.Equal(first.Pins, second.Pins);
        Assert.Equal(first.Channels, second.Channels);
        Assert.Equal(first.Feed!.Segments, second.Feed!.Segments);
    }

    [Fact]
    public void PlaneOutsideThePart_PlacesNoFeedAndSaysWhy()
    {
        var plan = MoldLayoutPlanner.Plan(Request(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 20, RoomyBlock));

        Assert.Null(plan.Feed);
        Assert.Contains(plan.Notes, n => n.Contains("does not cut the cavity", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-47.0, 1, 1.0, -46.75)]
    [InlineData(47.0, -1, 1.0, 46.75)]
    [InlineData(-46.9, 1, 1.0, -46.75)]
    [InlineData(46.7, -1, 1.0, 45.75)]
    [InlineData(10.0, 1, 0.5, 10.125)]
    public void OffGrid_MovesInwardToAQuarterVoxelOffTheGrid(double x, int inward, double grid, double expected)
    {
        Assert.Equal(expected, MoldLayoutPlanner.OffGrid(x, inward, grid), 9);
    }

    [Fact]
    public void GuidePins_WithAVoxelGrid_StayAQuarterVoxelOffIt()
    {
        // The corners of the roomy block fall on whole millimetres; at 1 mm voxels the circles of the pins and bores
        // would pass through grid nodes. With the grid given, every centre sits a quarter voxel off it, further inside.
        var request = Request(MeshWallThicknessSamplerTests.GeneratorTestBox(), PartingAxis.Z, 0, RoomyBlock);
        var onGrid = MoldLayoutPlanner.Plan(request);
        var offGrid = MoldLayoutPlanner.Plan(request with { VoxelGridMm = 1.0 });

        Assert.Equal(onGrid.GuidePins.Count, offGrid.GuidePins.Count);
        Assert.NotEmpty(offGrid.GuidePins);

        foreach (var guide in offGrid.GuidePins)
        {
            foreach (var coordinate in new[] { guide.Base.X, guide.Base.Y })
            {
                var fraction = coordinate - Math.Floor(coordinate);
                Assert.True(Math.Abs(fraction - 0.25) < 1e-9 || Math.Abs(fraction - 0.75) < 1e-9, $"{guide.Id}: {coordinate}");
            }

            Assert.True(guide.Base.X > RoomyBlock.MinX + 15 && guide.Base.X < RoomyBlock.MaxX - 15, guide.Id);
            Assert.True(guide.Base.Y > RoomyBlock.MinY + 15 && guide.Base.Y < RoomyBlock.MaxY - 15, guide.Id);
        }
    }

    private static MoldLayoutRequest Request(IReadOnlyList<MeshTriangle> cavity, PartingAxis axis, double offset, MoldLayoutBlock block) =>
        new(cavity, axis, offset, block, MaximumWallThicknessMm: 5, EjectorPinDiameterMm: 4, EjectorPinCount: 4,
            CoolingChannelDiameterMm: 6, CoolingChannelSpacingMm: 15, CoolingChannelsPerHalf: 3, CoolingCavityClearanceMm: 10,
            MoldEdgeClearanceMm: 10);

    private static IReadOnlyList<MeshTriangle> Scaled(IReadOnlyList<MeshTriangle> triangles, MeshVertex center, double factor)
    {
        MeshVertex S(MeshVertex v) => new(center.X + ((v.X - center.X) * factor), center.Y + ((v.Y - center.Y) * factor), center.Z + ((v.Z - center.Z) * factor));
        return triangles.Select(t => new MeshTriangle(S(t.A), S(t.B), S(t.C))).ToArray();
    }

    private static bool OnSideWall(MoldLayoutPoint p) =>
        (Math.Abs(p.X) < 1e-6 || Math.Abs(p.X - 20) < 1e-6) && p.Y > 0 && p.Y < 10 ||
        (Math.Abs(p.Y) < 1e-6 || Math.Abs(p.Y - 10) < 1e-6) && p.X > 0 && p.X < 20;

    private static bool Inside(MoldLayoutBlock b, MoldLayoutPoint p) =>
        p.X >= b.MinX && p.X <= b.MaxX && p.Y >= b.MinY && p.Y <= b.MaxY && p.Z >= b.MinZ && p.Z <= b.MaxZ;

    private static double Planar(MoldLayoutPoint a, MoldLayoutPoint b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    /// <summary>
    /// A 30 x 30 x 50 mm block (x, y 0..30), a 10 x 10 stem on it (x, y 10..20) and a 46 x 46 x 3 mm cap (x, y -8..38)
    /// on the stem.
    /// </summary>
    internal static IReadOnlyList<MeshTriangle> Mushroom(double stemHeight)
    {
        var block = Square(0, 30);
        var stem = Square(10, 20);
        var cap = Square(-8, 38);
        var top = 50 + stemHeight;
        var triangles = new List<MeshTriangle>();
        Flat(triangles, block, 0, up: false);
        Walls(triangles, block, 0, 50);
        Ring(triangles, block, stem, 50, up: true);
        Walls(triangles, stem, 50, top);
        Ring(triangles, cap, stem, top, up: false);
        Walls(triangles, cap, top, top + 3);
        Flat(triangles, cap, top + 3, up: true);
        return triangles;
    }

    private static (double X, double Y)[] Square(double min, double max) => new[] { (min, min), (max, min), (max, max), (min, max) };

    private static void Quad(List<MeshTriangle> triangles, MeshVertex a, MeshVertex b, MeshVertex c, MeshVertex d)
    {
        triangles.Add(new MeshTriangle(a, b, c));
        triangles.Add(new MeshTriangle(a, c, d));
    }

    private static MeshVertex At((double X, double Y) p, double z) => new(p.X, p.Y, z);

    /// <summary>Walls of a square (counterclockwise seen from +Z) between two heights, facing out.</summary>
    private static void Walls(List<MeshTriangle> triangles, (double X, double Y)[] square, double z0, double z1)
    {
        for (var k = 0; k < 4; k++)
        {
            var (a, b) = (square[k], square[(k + 1) % 4]);
            Quad(triangles, At(a, z0), At(b, z0), At(b, z1), At(a, z1));
        }
    }

    /// <summary>The flat ring between two squares at one height, facing up or down.</summary>
    private static void Ring(List<MeshTriangle> triangles, (double X, double Y)[] outer, (double X, double Y)[] inner, double z, bool up)
    {
        for (var k = 0; k < 4; k++)
        {
            var (a, b) = (outer[k], outer[(k + 1) % 4]);
            var (c, d) = (inner[k], inner[(k + 1) % 4]);

            if (up)
            {
                Quad(triangles, At(a, z), At(b, z), At(d, z), At(c, z));
            }
            else
            {
                Quad(triangles, At(a, z), At(c, z), At(d, z), At(b, z));
            }
        }
    }

    private static void Flat(List<MeshTriangle> triangles, (double X, double Y)[] square, double z, bool up)
    {
        if (up)
        {
            Quad(triangles, At(square[0], z), At(square[1], z), At(square[2], z), At(square[3], z));
        }
        else
        {
            Quad(triangles, At(square[0], z), At(square[3], z), At(square[2], z), At(square[1], z));
        }
    }

    /// <summary>40 x 40 x 3 mm plate with a 36 x 36 x 1 mm recess in its bottom (x and y 2..38, z 0..1).</summary>
    internal static IReadOnlyList<MeshTriangle> Tray()
    {
        var outer = new[] { (0.0, 0.0), (40.0, 0.0), (40.0, 40.0), (0.0, 40.0) };
        var inner = new[] { (2.0, 2.0), (38.0, 2.0), (38.0, 38.0), (2.0, 38.0) };
        var triangles = new List<MeshTriangle>();

        MeshVertex V((double X, double Y) p, double z) => new(p.X, p.Y, z);

        void Quad(MeshVertex a, MeshVertex b, MeshVertex c, MeshVertex d)
        {
            triangles.Add(new MeshTriangle(a, b, c));
            triangles.Add(new MeshTriangle(a, c, d));
        }

        for (var k = 0; k < 4; k++)
        {
            var (a, b) = (outer[k], outer[(k + 1) % 4]);
            var (c, d) = (inner[k], inner[(k + 1) % 4]);
            Quad(V(a, 0), V(b, 0), V(b, 3), V(a, 3));          // outer wall
            Quad(V(d, 0), V(c, 0), V(c, 1), V(d, 1));          // recess wall, facing the recess
            Quad(V(a, 0), V(c, 0), V(d, 0), V(b, 0));          // rim around the recess, facing down
        }

        Quad(V(outer[0], 3), V(outer[1], 3), V(outer[2], 3), V(outer[3], 3));   // top
        Quad(V(inner[0], 1), V(inner[3], 1), V(inner[2], 1), V(inner[1], 1));   // recess ceiling, facing down

        return triangles;
    }
}
