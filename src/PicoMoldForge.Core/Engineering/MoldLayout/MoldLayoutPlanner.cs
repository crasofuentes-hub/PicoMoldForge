using System.Globalization;
using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.MoldLayout;

/// <summary>
/// Places the feed system, the ejector pins and the cooling channels of a two-half mold around the real cavity
/// (pure .NET, no PicoGK runtime). The cavity is split by the plane {axis = offset}; each triangle belongs to the half
/// that holds its centroid (within the resolution of the plane, to the half its normal faces), as in
/// <see cref="MeshMoldAccessAnalyzer"/>.
/// <list type="number">
/// <item>Moving half: the half the part shrinks onto (more grip: area x inward in-plane distance of the faces whose
/// normal points towards the centre of the cavity, i.e. core surfaces); with no clear difference, the half holding
/// more of the cavity surface; otherwise the - half.</item>
/// <item>Feed: an edge gate on the parting plane where the cavity outline is extreme along one of
/// <see cref="MoldLayoutOptions.GateDirectionCount"/> in-plane directions, entering the thickest section; a straight
/// runner outward on the plane; a sprue from the end of the runner through the fixed half to its outer face. The
/// runner is lengthened until the sprue keeps its clearance from the cavity. Among directions that work: the thickest
/// entry (within 5 %), then the sprue nearest the centre of the block.</item>
/// <item>Ejector pins: on the faces of the moving half that face its back plate and lie across the axis
/// (|n . axis| &gt;= <see cref="FlatFaceDot"/>), grouped by level; a pin fits where its disc plus the edge margin lies
/// inside the face (exact distance to the face outline) and nothing of the part lies in its column between the face
/// and the back plate. Pins are spread by farthest-point selection from the point farthest from the centre of the
/// places that fit.</item>
/// <item>Guide pins near the corners of the block, on the moving half, entering bores of the fixed half.</item>
/// <item>Cooling: straight channels drilled through the block along the longer in-plane side of the cavity, in each
/// half beyond the cavity by the cavity clearance, spaced across and centred on the cavity; a position is skipped when
/// it does not keep its clearances from the cavity, the pins, the feed, the guide pins or the block.</item>
/// </list>
/// Distances between features and from features to the cavity are exact (segment to segment and segment to
/// triangle), so a measured clearance is the real one of the straight centrelines.
/// </summary>
public static class MoldLayoutPlanner
{
    /// <summary>A face lies across the axis when |n . axis| reaches this (as in <see cref="PartingPlaneSearch"/>).</summary>
    public const double FlatFaceDot = 0.9999;

    /// <summary>Entries whose thickness is within this share of the thickest count as equally thick.</summary>
    public const double EntryThicknessTolerance = 0.05;

    private const int MaxGridCells = 2048;
    private const int MaxPinCandidatesPerLevel = 20000;

    public static MoldLayoutPlan Plan(
        MoldLayoutRequest request,
        MoldLayoutOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= new MoldLayoutOptions();
        Validate(request, options);

        var context = Context.Create(request, options);
        var notes = new List<string>();
        var halves = Halves(context);
        notes.Add(HalvesNote(context, halves));

        var feed = PlanFeed(context, request, options, halves.MovingHalf, notes, cancellationToken);
        var feedSegments = feed?.Segments ?? Array.Empty<MoldFeedSegment>();
        var pins = PlanPins(context, request, options, halves.MovingHalf, feedSegments, notes, cancellationToken);

        // Guide pins before cooling: without them the halves do not align, while a channel has other positions.
        var guides = PlanGuides(context, request, options, halves.MovingHalf, feedSegments, pins, notes);
        var channels = PlanCooling(context, request, options, feedSegments, pins, guides, notes, cancellationToken);

        pins = pins
            .Select(pin => pin with
            {
                CoolingClearanceMm = channels.Count == 0
                    ? double.PositiveInfinity
                    : channels.Min(channel => Distance(pin.Tip, pin.End, channel.Start, channel.End) - (pin.DiameterMm / 2) - (channel.DiameterMm / 2))
            })
            .ToList();

        return new MoldLayoutPlan(
            request.Axis,
            request.PartingOffsetMm,
            halves,
            feed,
            pins,
            Math.Max(0, request.EjectorPinCount),
            options.PinSpacingToDiameter * request.EjectorPinDiameterMm,
            channels,
            options,
            notes,
            guides);
    }

    private static void Validate(MoldLayoutRequest request, MoldLayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(request.CavityTriangles);
        ArgumentNullException.ThrowIfNull(request.Block);

        if (request.CavityTriangles.Count == 0)
        {
            throw new ArgumentException("The cavity has no triangles.", nameof(request));
        }

        if (!double.IsFinite(request.PartingOffsetMm))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "PartingOffsetMm must be finite.");
        }

        var b = request.Block;

        if (!(b.MinX < b.MaxX && b.MinY < b.MaxY && b.MinZ < b.MaxZ))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The mold block must have positive size on X, Y and Z.");
        }

        if (request.EjectorPinDiameterMm < 0 || request.CoolingChannelDiameterMm < 0 || request.MaximumWallThicknessMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Diameters and thicknesses cannot be negative.");
        }

        if (request.CoolingCavityClearanceMm < 0 || request.MoldEdgeClearanceMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Clearances cannot be negative.");
        }

        if (request.CoolingChannelsPerHalf > 1 && !(request.CoolingChannelSpacingMm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "CoolingChannelSpacingMm must be positive for more than one channel.");
        }

        if (!(options.ResolutionMm > 0) || options.GateDirectionCount < 4 || !(options.GateLandMm > 0) ||
            !(options.GateDiameterToWallRatio > 0) || options.FeedCavityClearanceMm < 0 || options.FeatureClearanceMm < 0 ||
            options.PinEdgeMarginMm < 0 || options.PinSpacingToDiameter < 0 || options.PinFlatToleranceMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid mold layout options.");
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Halves

    private static MoldLayoutHalves Halves(Context c)
    {
        double areaPositive = 0, areaNegative = 0, gripPositive = 0, gripNegative = 0;

        for (var i = 0; i < c.Triangles.Count; i++)
        {
            var area = c.Areas[i];

            if (!(area > 1e-12))
            {
                continue;
            }

            var n = c.Normals[i];
            var radial = c.Centroids[i] - c.Center;
            var radialInPlane = radial - (c.Pull * Vec3d.Dot(radial, c.Pull));
            var normalInPlane = n - (c.Pull * Vec3d.Dot(n, c.Pull));

            // Shrinking towards its centre, the part presses on the steel of the faces whose normal points towards
            // the centre (the inside of a cup, a bore): they hold the part on their half. Only the in-plane part
            // resists the opening along the axis.
            var grip = area * Math.Max(0, -Vec3d.Dot(radialInPlane, normalInPlane));

            if (c.Halves[i] > 0)
            {
                areaPositive += area;
                gripPositive += grip;
            }
            else
            {
                areaNegative += area;
                gripNegative += grip;
            }
        }

        var totalArea = areaPositive + areaNegative;
        var largerGrip = Math.Max(gripPositive, gripNegative);
        int moving;
        string reason;

        if (largerGrip > 1e-9 * Math.Max(1, totalArea) && Math.Abs(gripPositive - gripNegative) > 0.1 * largerGrip)
        {
            moving = gripPositive > gripNegative ? 1 : -1;
            reason = "the part shrinks onto it (core surfaces)";
        }
        else if (Math.Abs(areaPositive - areaNegative) > Math.Max(1.0, 0.001 * totalArea))
        {
            moving = areaPositive > areaNegative ? 1 : -1;
            reason = "it holds more of the cavity surface";
        }
        else
        {
            moving = -1;
            reason = "both halves hold the part alike, so the - half is taken";
        }

        return new MoldLayoutHalves(moving, reason, areaPositive, areaNegative, gripPositive, gripNegative);
    }

    private static string HalvesNote(Context c, MoldLayoutHalves halves)
    {
        var axis = c.AxisName;
        var moving = halves.MovingHalf > 0 ? "+" : "-";
        var fixedHalf = halves.MovingHalf > 0 ? "-" : "+";
        return Invariant($"Moving half (stays with the part, carries the ejector pins): the {moving} half, opening along {moving}{axis}: {halves.Reason}. Cavity surface {halves.AreaPositiveMm2:0.#} mm2 in the + half, {halves.AreaNegativeMm2:0.#} mm2 in the - half; shrinkage grip {halves.GripPositiveMm3:0.#} and {halves.GripNegativeMm3:0.#} mm3. The fixed half ({fixedHalf}) carries the sprue.");
    }

    // ---------------------------------------------------------------------------------------------------------
    // Feed

    private sealed record FeedCandidate(int Index, MoldFeedLayout Layout, double CentreDistance);

    private static MoldFeedLayout? PlanFeed(
        Context c,
        MoldLayoutRequest request,
        MoldLayoutOptions o,
        int moving,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var outline = OutlinePoints(c);

        if (outline.Count == 0)
        {
            notes.Add(Invariant($"Feed: the parting plane {c.AxisName} = {c.Offset:0.###} mm does not cut the cavity, so no gate can be placed on it."));
            return null;
        }

        var runnerDiameter = Math.Max(request.MaximumWallThicknessMm + o.RunnerOverWallMm, o.MinimumRunnerDiameterMm);
        var sprueDiameter = runnerDiameter + o.SprueOverRunnerMm;
        var candidates = new List<FeedCandidate>();

        for (var k = 0; k < o.GateDirectionCount; k++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var angle = 2 * Math.PI * k / o.GateDirectionCount;
            var candidate = TryFeed(c, request, o, -moving, outline, Snap(Math.Cos(angle)), Snap(Math.Sin(angle)), runnerDiameter, sprueDiameter, k);

            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            notes.Add("Feed: no gate position found on the cavity outline in the parting plane.");
            return null;
        }

        var pool = candidates.Where(x => x.Layout.IsFeasible).ToList();

        if (pool.Count == 0)
        {
            pool = candidates;
        }

        var thickest = pool.Max(x => x.Layout.EntryThicknessMm);
        var chosen = pool
            .Where(x => x.Layout.EntryThicknessMm >= thickest * (1 - EntryThicknessTolerance))
            .OrderBy(x => x.CentreDistance)
            .ThenBy(x => x.Index)
            .First();

        notes.Add(FeedNote(o, chosen.Layout));
        return chosen.Layout;
    }

    private static FeedCandidate? TryFeed(
        Context c,
        MoldLayoutRequest request,
        MoldLayoutOptions o,
        int fixedHalf,
        List<OutlinePoint> outline,
        double ux,
        double uy,
        double runnerDiameter,
        double sprueDiameter,
        int index)
    {
        var r = c.Resolution;
        var extreme = Extreme(outline, ux, uy, r);

        if (extreme < 0)
        {
            return null;
        }

        // The gate enters along the outward normal of the outline there, at the middle of the straight (or gently
        // curved) stretch of outline that holds the extreme point: the entry. Its width is the length of that
        // stretch: the outline within a quarter of the gate size of its tangent, so corners and thin ribs give
        // narrow entries.
        var (nu, nv) = (outline[extreme].NU, outline[extreme].NV);
        var firstColumn = ColumnThickness(c, c.Point(outline[extreme].U - (nu * r), outline[extreme].V - (nv * r), c.Offset));
        var tolerance = Math.Max(r, 0.25 * o.GateDiameterToWallRatio * firstColumn);
        var (middle, width) = Stretch(outline, extreme, tolerance, r);
        (nu, nv) = (outline[middle].NU, outline[middle].NV);
        var u3 = (c.SideA * nu) + (c.SideB * nv);
        var gatePoint = c.Point(outline[middle].U, outline[middle].V, c.Offset);
        var column = ColumnThickness(c, gatePoint - (u3 * r));
        var depth = InwardDepth(c, gatePoint, u3);

        // The section the gate enters: the largest ball inside the part within reach of the gate (its centre no
        // farther from the gate point than 1.5 radii), bounded by the part along the axis there, the width of the
        // entry and the depth of the part along the gate.
        var bound = Math.Min(column, Math.Min(width, depth));
        var thickness = bound > 0 ? Math.Min(bound, LocalBallDiameter(c, gatePoint, u3, bound)) : 0;

        // No section is thicker than the thickest wall of the part, so the gate stays narrower than the runner.
        if (request.MaximumWallThicknessMm > 0)
        {
            thickness = Math.Min(thickness, request.MaximumWallThicknessMm);
        }

        var problems = new List<string>();

        if (!(column > 0))
        {
            problems.Add("there is no material just inside the outline at the gate");
        }

        var gateDiameter = Math.Max(o.MinimumGateDiameterMm, o.GateDiameterToWallRatio * thickness);

        if (column > 0 && gateDiameter > width + 1e-9)
        {
            problems.Add(Invariant($"the entry is {width:0.##} mm wide, narrower than the {gateDiameter:0.##} mm gate"));
        }

        if (column > 0 && gateDiameter > column + 1e-9)
        {
            problems.Add(Invariant($"the section is {column:0.##} mm thick, thinner than the {gateDiameter:0.##} mm gate"));
        }

        var rg = gateDiameter / 2;
        var rr = runnerDiameter / 2;
        var rs = sprueDiameter / 2;
        var gateOuter = gatePoint + (u3 * (o.GateLandMm + rr));

        // Beyond its entry the gate must not meet the cavity again: its axis just above and below the plane, and its
        // body once its end cap has left the entry (from one gate radius plus the feed clearance out).
        var clearBeyond = true;

        foreach (var side in new[] { 1.0, -1.0 })
        {
            var start = gatePoint + (u3 * r) + (c.Pull * (side * r));

            if (c.Bvh.RayHit(start, u3, -1).Distance <= o.GateLandMm + rr)
            {
                clearBeyond = false;
            }
        }

        var bodyStart = gatePoint + (u3 * (rg + o.FeedCavityClearanceMm));

        if (o.GateLandMm + rr > rg + o.FeedCavityClearanceMm && c.Bvh.SegmentDistance(bodyStart, gateOuter) < rg)
        {
            clearBeyond = false;
        }

        if (!clearBeyond)
        {
            problems.Add("beyond its entry the gate meets the cavity again");
        }

        // The runner starts one gate land away from the cavity; its end (the sprue) moves out until the sprue keeps
        // its clearance from the cavity.
        var fixedFace = fixedHalf > 0 ? c.H(c.BlockMax) : c.H(c.BlockMin);
        var step = Math.Max(5 * r, 0.5);
        var distance = o.GateLandMm + rr + rs;
        var runnerClearance = double.NaN;
        var sprueClearance = double.NaN;
        var sprue = gatePoint;
        var placed = false;

        if (Math.Abs(fixedFace - c.Offset) < 1e-9)
        {
            problems.Add("the fixed half has no thickness behind the parting plane");
        }
        else
        {
            for (var guard = 0; guard < 100_000; guard++)
            {
                sprue = gatePoint + (u3 * distance);

                if (InPlaneFaceDistance(c, sprue) - rs < request.MoldEdgeClearanceMm)
                {
                    problems.Add(Invariant($"no room in the block for the sprue clear of the cavity (it would come within {request.MoldEdgeClearanceMm:0.##} mm of a block face)"));
                    break;
                }

                runnerClearance = c.Bvh.SegmentDistance(gateOuter, sprue) - rr;

                if (runnerClearance < o.FeedCavityClearanceMm)
                {
                    problems.Add(Invariant($"the runner comes within {Math.Max(0, runnerClearance):0.##} mm of the cavity"));
                    break;
                }

                sprueClearance = c.Bvh.SegmentDistance(sprue, c.WithHeight(sprue, fixedFace)) - rs;

                if (sprueClearance >= o.FeedCavityClearanceMm)
                {
                    placed = true;
                    break;
                }

                distance += step;
            }
        }

        var sprueEnd = c.WithHeight(sprue, fixedFace);
        var gateStart = gatePoint - (u3 * r);
        var segments = new List<MoldFeedSegment>
        {
            new(
                "gate-1",
                GateSystemFeatureType.Gate,
                ToPoint(gateStart),
                ToPoint(gateOuter),
                gateDiameter,
                0,
                c.FaceDistance(gateStart, gateOuter) - rg),
            new(
                "runner-1",
                GateSystemFeatureType.Runner,
                ToPoint(gateOuter),
                ToPoint(sprue),
                runnerDiameter,
                double.IsNaN(runnerClearance) ? 0 : runnerClearance,
                c.FaceDistance(gateOuter, sprue) - rr),
            new(
                "sprue-1",
                GateSystemFeatureType.Sprue,
                ToPoint(sprue),
                ToPoint(sprueEnd),
                sprueDiameter,
                double.IsNaN(sprueClearance) ? 0 : sprueClearance,
                c.FaceDistance(sprue, sprueEnd, c.AxisIndex, fixedHalf) - rs)
        };

        foreach (var segment in segments.Where(s => s.Type != GateSystemFeatureType.Sprue || placed))
        {
            if (segment.MoldEdgeClearanceMm < request.MoldEdgeClearanceMm)
            {
                problems.Add(Invariant($"the {segment.Type.ToString().ToLowerInvariant()} comes within {Math.Max(0, segment.MoldEdgeClearanceMm):0.##} mm of a block face"));
            }
        }

        var layout = new MoldFeedLayout(
            placed && problems.Count == 0,
            ToPoint(gatePoint),
            ToPoint(u3),
            thickness,
            width,
            column > 0,
            clearBeyond,
            segments,
            problems);

        var centreU = (c.U(c.BlockMin) + c.U(c.BlockMax)) / 2;
        var centreV = (c.V(c.BlockMin) + c.V(c.BlockMax)) / 2;
        var centreDistance = Math.Sqrt(Square(c.U(sprue) - centreU) + Square(c.V(sprue) - centreV));
        return new FeedCandidate(index, layout, centreDistance);
    }

    /// <summary>
    /// Index of the outline point at the middle of the longest contiguous run of points within
    /// <paramref name="tolerance"/> of the extreme of the outline along u; -1 for an empty outline.
    /// </summary>
    private static int Extreme(List<OutlinePoint> outline, double ux, double uy, double tolerance)
    {
        var extreme = double.NegativeInfinity;

        foreach (var p in outline)
        {
            extreme = Math.Max(extreme, (p.U * ux) + (p.V * uy));
        }

        if (!double.IsFinite(extreme))
        {
            return -1;
        }

        var members = Enumerable.Range(0, outline.Count)
            .Where(i => (outline[i].U * ux) + (outline[i].V * uy) >= extreme - tolerance)
            .Select(i => (W: (-outline[i].U * uy) + (outline[i].V * ux), Index: i))
            .ToArray();

        return Middle(outline, members, tolerance, containing: double.NaN).Index;
    }

    /// <summary>
    /// The stretch of outline through point <paramref name="at"/>: the contiguous points within
    /// <paramref name="tolerance"/> of the tangent there. Returns its middle point and its length.
    /// </summary>
    private static (int Middle, double Width) Stretch(List<OutlinePoint> outline, int at, double tolerance, double step)
    {
        var origin = outline[at];
        var members = Enumerable.Range(0, outline.Count)
            .Where(i => Math.Abs(((outline[i].U - origin.U) * origin.NU) + ((outline[i].V - origin.V) * origin.NV)) <= tolerance)
            .Select(i => (W: (-(outline[i].U - origin.U) * origin.NV) + ((outline[i].V - origin.V) * origin.NU), Index: i))
            .ToArray();

        var (index, width) = Middle(outline, members, step, containing: 0);
        return (index, width);
    }

    /// <summary>
    /// Sorts points by W, splits them where W jumps by more than 2.5 steps, takes the run that contains
    /// <paramref name="containing"/> (or the longest run when it is NaN) and returns the point nearest the middle of
    /// that run and the run length.
    /// </summary>
    private static (int Index, double Width) Middle(List<OutlinePoint> outline, (double W, int Index)[] members, double step, double containing)
    {
        var sorted = members
            .OrderBy(m => m.W)
            .ThenBy(m => outline[m.Index].U)
            .ThenBy(m => outline[m.Index].V)
            .ToArray();
        var bestStart = 0;
        var bestEnd = 0;
        var start = 0;
        var gap = Math.Max(2.5 * step, 1e-6);

        for (var i = 1; i <= sorted.Length; i++)
        {
            if (i == sorted.Length || sorted[i].W - sorted[i - 1].W > gap)
            {
                var runLength = sorted[i - 1].W - sorted[start].W;
                var take = double.IsNaN(containing)
                    ? runLength > sorted[bestEnd].W - sorted[bestStart].W
                    : sorted[start].W - gap <= containing && containing <= sorted[i - 1].W + gap;

                if (take)
                {
                    bestStart = start;
                    bestEnd = i - 1;
                }

                start = i;
            }
        }

        var middle = (sorted[bestStart].W + sorted[bestEnd].W) / 2;
        var chosen = bestStart;

        for (var i = bestStart; i <= bestEnd; i++)
        {
            if (Math.Abs(sorted[i].W - middle) < Math.Abs(sorted[chosen].W - middle))
            {
                chosen = i;
            }
        }

        return (sorted[chosen].Index, sorted[bestEnd].W - sorted[bestStart].W);
    }

    /// <summary>A point of the cavity outline on the parting plane and the outward normal of the outline there.</summary>
    private readonly record struct OutlinePoint(double U, double V, double NU, double NV);

    /// <summary>
    /// Points along the sections of the cavity one resolution above and below the parting plane, each with the
    /// outward normal of its triangle projected on the plane.
    /// </summary>
    private static List<OutlinePoint> OutlinePoints(Context c)
    {
        var points = new List<OutlinePoint>();
        var r = c.Resolution;
        Span<Vec3d> crossing = stackalloc Vec3d[2];

        foreach (var level in new[] { c.Offset + r, c.Offset - r })
        {
            for (var i = 0; i < c.Triangles.Count; i++)
            {
                var t = c.Triangles[i];
                var a = Vec3d.From(t.A);
                var b = Vec3d.From(t.B);
                var d = Vec3d.From(t.C);
                var count = 0;
                Cross(c.H(a) - level, c.H(b) - level, a, b, crossing, ref count);
                Cross(c.H(b) - level, c.H(d) - level, b, d, crossing, ref count);
                Cross(c.H(d) - level, c.H(a) - level, d, a, crossing, ref count);
                var nu = c.U(c.Normals[i]);
                var nv = c.V(c.Normals[i]);
                var nl = Math.Sqrt((nu * nu) + (nv * nv));

                if (count != 2 || !(nl > 1e-9))
                {
                    continue;
                }

                var length = (crossing[1] - crossing[0]).Length;
                var samples = Math.Max(1, (int)Math.Ceiling(length / r));

                for (var s = 0; s <= samples; s++)
                {
                    var p = crossing[0] + ((crossing[1] - crossing[0]) * ((double)s / samples));
                    points.Add(new OutlinePoint(c.U(p), c.V(p), nu / nl, nv / nl));
                }
            }
        }

        return points;
    }

    private static void Cross(double hp, double hq, Vec3d p, Vec3d q, Span<Vec3d> crossing, ref int count)
    {
        if ((hp >= 0) != (hq >= 0) && count < 2)
        {
            crossing[count++] = p + ((q - p) * (hp / (hp - hq)));
        }
    }

    /// <summary>
    /// Length of material along the axis through q: from one resolution above (below) q, a ray along +axis (-axis)
    /// that leaves the part through its first face started inside it.
    /// </summary>
    private static double ColumnThickness(Context c, Vec3d q)
    {
        var total = 0.0;

        foreach (var side in new[] { 1.0, -1.0 })
        {
            var direction = c.Pull * side;
            var (distance, triangle) = c.Bvh.RayHit(q + (direction * c.Resolution), direction, -1);

            if (triangle >= 0 && Vec3d.Dot(c.Normals[triangle], direction) > 0)
            {
                total += distance + c.Resolution;
            }
        }

        return total;
    }

    /// <summary>
    /// Diameter of the largest ball inside the part whose centre lies no farther from the gate point than 1.5 times
    /// its radius (a ball that reaches the gate; in a 90 degree edge a ball touching both faces comes within 0.42
    /// radii of it). Searched on a grid of the plane through the gate point spanned by the inward direction and the
    /// axis, within 0.75 <paramref name="bound"/> of the gate point, then refined around the best point, also along
    /// the outline.
    /// </summary>
    private static double LocalBallDiameter(Context c, Vec3d gatePoint, Vec3d u3, double bound)
    {
        var reach = 0.75 * bound;
        var step = reach / 12;
        var along = Vec3d.Cross(u3, c.Pull);
        var best = 0.0;
        var (bestS, bestH, bestW) = (0.0, 0.0, 0.0);

        double Ball(double s, double h, double w)
        {
            if (!(s > 0))
            {
                return 0;
            }

            var q = gatePoint - (u3 * s) + (c.Pull * h) + (along * w);
            var (distance, _) = c.Bvh.Nearest(q);

            if (!(2 * distance > best) || (q - gatePoint).Length > 1.5 * distance || !Inside(c, q))
            {
                return 0;
            }

            return 2 * distance;
        }

        for (var i = 1; i <= 12; i++)
        {
            for (var j = -12; j <= 12; j++)
            {
                var d = Ball(i * step, j * step, 0);

                if (d > best)
                {
                    (best, bestS, bestH, bestW) = (d, i * step, j * step, 0);
                }
            }
        }

        for (var round = 0; round < 6 && best > 0; round++)
        {
            step /= 2;
            var (s0, h0, w0) = (bestS, bestH, bestW);

            for (var i = -1; i <= 1; i++)
            {
                for (var j = -1; j <= 1; j++)
                {
                    for (var k = -1; k <= 1; k++)
                    {
                        var (s, h, w) = (s0 + (i * step), h0 + (j * step), w0 + (k * step));
                        var d = Ball(s, h, w);

                        if (d > best)
                        {
                            (best, bestS, bestH, bestW) = (d, s, h, w);
                        }
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// A point is inside the part when a ray from it leaves through the first face it meets (oblique, so it does
    /// not run along the faces of an axis-aligned part).
    /// </summary>
    private static bool Inside(Context c, Vec3d q)
    {
        var direction = new Vec3d(0.3093, 0.5162, 0.7987);
        var (_, triangle) = c.Bvh.RayHit(q, direction, -1);
        return triangle >= 0 && Vec3d.Dot(c.Normals[triangle], direction) > 0;
    }

    /// <summary>
    /// Depth of the part from the gate point inwards (against u), just above and just below the plane: the longest
    /// ray started inside the part.
    /// </summary>
    private static double InwardDepth(Context c, Vec3d gatePoint, Vec3d u3)
    {
        var depth = 0.0;

        foreach (var side in new[] { 1.0, -1.0 })
        {
            var start = gatePoint - (u3 * c.Resolution) + (c.Pull * (side * c.Resolution));
            var (distance, triangle) = c.Bvh.RayHit(start, -u3, -1);

            if (triangle >= 0 && Vec3d.Dot(c.Normals[triangle], -u3) > 0)
            {
                depth = Math.Max(depth, distance + c.Resolution);
            }
        }

        return depth;
    }

    private static string FeedNote(MoldLayoutOptions o, MoldFeedLayout feed)
    {
        var gate = feed.Segments[0];
        var runner = feed.Segments[1];
        var sprue = feed.Segments[2];
        var text = Invariant($"Feed: edge gate on the parting plane at ({feed.GatePoint.X:0.##}, {feed.GatePoint.Y:0.##}, {feed.GatePoint.Z:0.##}) mm, entering a section {feed.EntryThicknessMm:0.##} mm thick: gate {gate.DiameterMm:0.##} mm, land {o.GateLandMm:0.##} mm; runner {runner.DiameterMm:0.##} mm x {Length(runner):0.##} mm, {runner.CavityClearanceMm:0.##} mm from the cavity; sprue {sprue.DiameterMm:0.##} mm x {Length(sprue):0.##} mm through the fixed half to its outer face, {sprue.CavityClearanceMm:0.##} mm from the cavity.");

        return feed.IsFeasible
            ? text
            : text + " FAIL: " + string.Join("; ", feed.Problems) + ".";
    }

    // ---------------------------------------------------------------------------------------------------------
    // Ejector pins

    private static List<MoldEjectorPinLayout> PlanPins(
        Context c,
        MoldLayoutRequest request,
        MoldLayoutOptions o,
        int moving,
        IReadOnlyList<MoldFeedSegment> feed,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var count = request.EjectorPinCount;
        var diameter = request.EjectorPinDiameterMm;
        var pins = new List<MoldEjectorPinLayout>();

        if (count <= 0 || !(diameter > 0))
        {
            notes.Add("Ejector pins: none configured (moldSystem.ejectorPinCount, moldSystem.ejectorPinDiameterMm).");
            return pins;
        }

        var radius = diameter / 2;
        var needed = radius + o.PinEdgeMarginMm;
        var spacing = o.PinSpacingToDiameter * diameter;
        var direction = c.Pull * moving;
        var back = moving > 0 ? c.H(c.BlockMax) : c.H(c.BlockMin);
        var faces = Enumerable.Range(0, c.Triangles.Count)
            .Where(i => c.Halves[i] == moving && c.Areas[i] > 1e-12 && Vec3d.Dot(c.Normals[i], direction) >= FlatFaceDot)
            .OrderByDescending(i => moving * c.H(c.Centroids[i]))
            .ThenBy(i => i)
            .ToArray();

        var extent = Math.Max(c.Max1 - c.Min1, c.Max2 - c.Min2);
        var map = new ShadowMap(c.Triangles, c.Pull, c.SideA, c.SideB, Math.Max(c.Resolution / 2, extent / MaxGridCells));
        var accepted = new List<(double U, double V, double H)>();
        var widest = 0.0;
        var start = 0;

        // Faces at one level (within the resolution) form one landing region.
        while (start < faces.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = start + 1;
            var top = moving * c.H(c.Centroids[faces[start]]);

            while (end < faces.Length && top - (moving * c.H(c.Centroids[faces[end]])) <= c.Resolution)
            {
                end++;
            }

            var level = faces[start..end];
            var height = level.Average(i => c.H(c.Centroids[i]));

            if (moving * (height - c.Offset) >= -c.Resolution)
            {
                widest = Math.Max(widest, LandingPoints(c, map, o, level, height, moving, radius, needed, feed, back, accepted));
            }

            start = end;
        }

        var chosen = SpreadPins(accepted, count, spacing);
        var extreme = moving > 0 ? c.MaxHeight : c.MinHeight;
        var stroke = Math.Max(moving * (extreme - c.Offset), c.Resolution);

        for (var k = 0; k < chosen.Count; k++)
        {
            var (u, v, h) = accepted[chosen[k]];
            var tip = c.Point(u, v, h);
            var tail = c.Point(u, v, back);
            var feedClearance = feed.Count == 0
                ? double.PositiveInfinity
                : feed.Min(s => Distance(ToPoint(tip), ToPoint(tail), s.Start, s.End) - radius - (s.DiameterMm / 2));

            pins.Add(new MoldEjectorPinLayout(
                Invariant($"ejector-pin-{k + 1:000}"),
                ToPoint(tip),
                ToPoint(tail),
                diameter,
                stroke,
                Math.PI * radius * radius,
                feedClearance,
                double.PositiveInfinity,
                c.FaceDistance(tip, tail, c.AxisIndex, moving) - radius));
        }

        var sign = moving > 0 ? "+" : "-";
        var head = Invariant($"Ejector pins: {pins.Count} of {count} pins {diameter:0.##} mm on the moving half, on its faces across {c.AxisName} that face its back plate (flat within {o.PinFlatToleranceMm:0.##} mm), {o.PinEdgeMarginMm:0.##} mm inside their outline, at least {spacing:0.##} mm apart, nothing of the part in their way to the {sign}{c.AxisName} face of the block; stroke {stroke:0.##} mm (the depth of the cavity in the moving half).");

        if (pins.Count == 0)
        {
            notes.Add(head + Invariant($" FAIL: no such face takes a {diameter:0.##} mm pin; the widest takes {Math.Max(0, 2 * (widest - o.PinEdgeMarginMm)):0.##} mm."));
        }
        else if (pins.Count < count)
        {
            notes.Add(head + Invariant($" The faces leave room for no more at that spacing."));
        }
        else
        {
            notes.Add(head);
        }

        return pins;
    }

    /// <summary>
    /// Places where a pin fits on one landing region, added to <paramref name="accepted"/>; returns the largest
    /// distance from the region outline reached by a candidate (to report the widest landing).
    /// </summary>
    private static double LandingPoints(
        Context c,
        ShadowMap map,
        MoldLayoutOptions o,
        int[] level,
        double height,
        int moving,
        double radius,
        double needed,
        IReadOnlyList<MoldFeedSegment> feed,
        double back,
        List<(double U, double V, double H)> accepted)
    {
        var triangles = new (double U0, double V0, double U1, double V1, double U2, double V2)[level.Length];
        var edgeCount = new Dictionary<(Vec3d, Vec3d), int>();
        double minU = double.PositiveInfinity, minV = double.PositiveInfinity;
        double maxU = double.NegativeInfinity, maxV = double.NegativeInfinity;
        var area = 0.0;

        for (var k = 0; k < level.Length; k++)
        {
            var t = c.Triangles[level[k]];
            var a = Vec3d.From(t.A);
            var b = Vec3d.From(t.B);
            var d = Vec3d.From(t.C);
            triangles[k] = (c.U(a), c.V(a), c.U(b), c.V(b), c.U(d), c.V(d));
            minU = Math.Min(minU, Math.Min(triangles[k].U0, Math.Min(triangles[k].U1, triangles[k].U2)));
            maxU = Math.Max(maxU, Math.Max(triangles[k].U0, Math.Max(triangles[k].U1, triangles[k].U2)));
            minV = Math.Min(minV, Math.Min(triangles[k].V0, Math.Min(triangles[k].V1, triangles[k].V2)));
            maxV = Math.Max(maxV, Math.Max(triangles[k].V0, Math.Max(triangles[k].V1, triangles[k].V2)));
            area += c.Areas[level[k]];
            CountEdge(edgeCount, a, b);
            CountEdge(edgeCount, b, d);
            CountEdge(edgeCount, d, a);
        }

        // The outline of the region: edges used by one of its triangles only.
        var outline = edgeCount
            .Where(e => e.Value == 1)
            .Select(e => (U0: c.U(e.Key.Item1), V0: c.V(e.Key.Item1), U1: c.U(e.Key.Item2), V1: c.V(e.Key.Item2)))
            .OrderBy(e => e.U0).ThenBy(e => e.V0).ThenBy(e => e.U1).ThenBy(e => e.V1)
            .ToArray();

        var bucket = Math.Max(needed, c.Resolution);
        var triangleGrid = new Grid2D(minU, minV, maxU, maxV, bucket);

        for (var k = 0; k < triangles.Length; k++)
        {
            var t = triangles[k];
            triangleGrid.Add(k, Math.Min(t.U0, Math.Min(t.U1, t.U2)), Math.Min(t.V0, Math.Min(t.V1, t.V2)), Math.Max(t.U0, Math.Max(t.U1, t.U2)), Math.Max(t.V0, Math.Max(t.V1, t.V2)));
        }

        var outlineGrid = new Grid2D(minU, minV, maxU, maxV, bucket);

        for (var k = 0; k < outline.Length; k++)
        {
            var e = outline[k];
            outlineGrid.Add(k, Math.Min(e.U0, e.U1), Math.Min(e.V0, e.V1), Math.Max(e.U0, e.U1), Math.Max(e.V0, e.V1));
        }

        bool Inside(double u, double v)
        {
            foreach (var k in triangleGrid.At(u, v))
            {
                if (PointInTriangle(u, v, triangles[k]))
                {
                    return true;
                }
            }

            return false;
        }

        // Distance to the outline up to a cap (enough to decide and to push), with the nearest outline point.
        (double Distance, double U, double V) Outline(double u, double v, double cap)
        {
            var best = (Distance: cap, U: double.NaN, V: double.NaN);

            foreach (var k in outlineGrid.Around(u, v, cap))
            {
                var e = outline[k];
                var (distance, pu, pv) = PointSegment(u, v, e.U0, e.V0, e.U1, e.V1);

                if (distance < best.Distance)
                {
                    best = (distance, pu, pv);
                }
            }

            return best;
        }

        var spacingGrid = Math.Max(Math.Min(0.5, 2 * radius / 4), Math.Sqrt(area / MaxPinCandidatesPerLevel));
        var cap = needed + spacingGrid;
        var widest = 0.0;
        var limit = (moving * height) + o.PinFlatToleranceMm;

        for (var gu = minU + (spacingGrid / 2); gu <= maxU; gu += spacingGrid)
        {
            for (var gv = minV + (spacingGrid / 2); gv <= maxV; gv += spacingGrid)
            {
                var u = gu;
                var v = gv;

                if (!Inside(u, v))
                {
                    continue;
                }

                var near = Outline(u, v, cap);

                // Move away from the nearest outline point until the margin is met (towards the middle of the face).
                for (var push = 0; push < 4 && near.Distance < needed && !double.IsNaN(near.U); push++)
                {
                    var du = u - near.U;
                    var dv = v - near.V;
                    var length = Math.Sqrt((du * du) + (dv * dv));

                    if (length < 1e-12)
                    {
                        break;
                    }

                    var move = needed - near.Distance + 1e-6;
                    var nu = u + (du / length * move);
                    var nv = v + (dv / length * move);

                    if (!Inside(nu, nv))
                    {
                        break;
                    }

                    u = nu;
                    v = nv;
                    near = Outline(u, v, cap);
                }

                widest = Math.Max(widest, near.Distance);

                if (near.Distance < needed || !ColumnClear(map, u, v, radius, moving, limit))
                {
                    continue;
                }

                var tip = ToPoint(c.Point(u, v, height));
                var tail = ToPoint(c.Point(u, v, back));

                if (feed.Any(s => Distance(tip, tail, s.Start, s.End) - radius - (s.DiameterMm / 2) < o.FeatureClearanceMm))
                {
                    continue;
                }

                accepted.Add((u, v, height));
            }
        }

        return widest;
    }

    /// <summary>Nothing of the part over the pin disc lies nearer the back plate than <paramref name="limit"/>.</summary>
    private static bool ColumnClear(ShadowMap map, double u, double v, double radius, int moving, double limit)
    {
        var (i0, j0) = map.CellOf(u - radius, v - radius);
        var (i1, j1) = map.CellOf(u + radius, v + radius);

        for (var j = Math.Max(0, j0); j <= Math.Min(map.Count2 - 1, j1); j++)
        {
            for (var i = Math.Max(0, i0); i <= Math.Min(map.Count1 - 1, i1); i++)
            {
                var (cu, cv) = map.CellCenter(i, j);
                var half = map.Cell / 2;
                var du = Math.Max(0, Math.Abs(u - cu) - half);
                var dv = Math.Max(0, Math.Abs(v - cv) - half);

                if ((du * du) + (dv * dv) <= radius * radius && map.Reach(i, j, moving) > limit)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Farthest-point selection: first the place farthest from the centre of all places, then each time the place
    /// farthest from the pins already chosen, while it is at least <paramref name="spacing"/> from them.
    /// </summary>
    private static List<int> SpreadPins(List<(double U, double V, double H)> places, int count, double spacing)
    {
        var chosen = new List<int>();

        if (places.Count == 0)
        {
            return chosen;
        }

        var cu = places.Average(p => p.U);
        var cv = places.Average(p => p.V);
        var first = 0;

        for (var i = 1; i < places.Count; i++)
        {
            if (Square(places[i].U - cu) + Square(places[i].V - cv) > Square(places[first].U - cu) + Square(places[first].V - cv) + 1e-12)
            {
                first = i;
            }
        }

        chosen.Add(first);
        var nearest = places.Select(p => Math.Sqrt(Square(p.U - places[first].U) + Square(p.V - places[first].V))).ToArray();

        while (chosen.Count < count)
        {
            var next = -1;

            for (var i = 0; i < places.Count; i++)
            {
                if (nearest[i] >= spacing && (next < 0 || nearest[i] > nearest[next] + 1e-12))
                {
                    next = i;
                }
            }

            if (next < 0)
            {
                break;
            }

            chosen.Add(next);

            for (var i = 0; i < places.Count; i++)
            {
                nearest[i] = Math.Min(nearest[i], Math.Sqrt(Square(places[i].U - places[next].U) + Square(places[i].V - places[next].V)));
            }
        }

        return chosen;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Cooling

    private static List<MoldCoolingChannelLayout> PlanCooling(
        Context c,
        MoldLayoutRequest request,
        MoldLayoutOptions o,
        IReadOnlyList<MoldFeedSegment> feed,
        IReadOnlyList<MoldEjectorPinLayout> pins,
        IReadOnlyList<MoldGuidePinLayout> guides,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        var channels = new List<MoldCoolingChannelLayout>();
        var count = request.CoolingChannelsPerHalf;
        var diameter = request.CoolingChannelDiameterMm;

        if (count <= 0 || !(diameter > 0))
        {
            notes.Add("Cooling: no channels configured (cooling.channelCount, cooling.channelDiameterMm).");
            return channels;
        }

        var rc = diameter / 2;
        var cavityClearance = request.CoolingCavityClearanceMm;
        var edge = request.MoldEdgeClearanceMm;
        var alongFirst = c.Max1 - c.Min1 >= c.Max2 - c.Min2;
        var along = alongFirst ? c.SideA : c.SideB;
        var across = alongFirst ? c.SideB : c.SideA;
        var alongName = AxisName(along);
        var acrossName = AxisName(across);
        var acrossCentre = alongFirst ? (c.Min2 + c.Max2) / 2 : (c.Min1 + c.Max1) / 2;
        var alongMin = Vec3d.Dot(c.BlockMin, along);
        var alongMax = Vec3d.Dot(c.BlockMax, along);
        var acrossMin = Vec3d.Dot(c.BlockMin, across);
        var acrossMax = Vec3d.Dot(c.BlockMax, across);
        var halfNotes = new List<string>();

        foreach (var half in new[] { 1, -1 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sign = half > 0 ? "+" : "-";
            var extreme = half > 0 ? Math.Max(c.Offset, c.MaxHeight) : Math.Min(c.Offset, c.MinHeight);

            // One resolution more than the minimum, so the measured clearance is not an exact tie with it.
            var level = extreme + (half * (cavityClearance + rc + c.Resolution));
            var outer = half > 0 ? c.H(c.BlockMax) : c.H(c.BlockMin);
            var room = (half * (outer - level)) - rc;

            if (room < edge)
            {
                var needed = (cavityClearance + diameter + c.Resolution + edge) - (half * (outer - extreme));
                halfNotes.Add(Invariant($"{sign} half: none, the block leaves {half * (outer - extreme):0.##} mm of steel behind the cavity and a channel needs {cavityClearance + diameter + c.Resolution + edge:0.##} mm ({needed:0.##} mm more)"));
                continue;
            }

            var placed = new List<MoldCoolingChannelLayout>();
            var skipped = new List<string>();

            foreach (var offset in Offsets(count, request.CoolingChannelSpacingMm, acrossMax - acrossMin))
            {
                if (placed.Count == count)
                {
                    break;
                }

                var position = acrossCentre + offset;
                var edgeAcross = Math.Min(position - acrossMin, acrossMax - position) - rc;

                if (edgeAcross < edge)
                {
                    continue;
                }

                var start = (along * alongMin) + (across * position) + (c.Pull * level);
                var end = (along * alongMax) + (across * position) + (c.Pull * level);
                var startPoint = ToPoint(start);
                var endPoint = ToPoint(end);
                var cavity = c.Bvh.SegmentDistance(start, end) - rc;
                var pinClearance = pins.Count == 0
                    ? double.PositiveInfinity
                    : pins.Min(p => Distance(startPoint, endPoint, p.Tip, p.End) - rc - (p.DiameterMm / 2));
                var feedClearance = feed.Count == 0
                    ? double.PositiveInfinity
                    : feed.Min(s => Distance(startPoint, endPoint, s.Start, s.End) - rc - (s.DiameterMm / 2));
                var channelClearance = placed.Count == 0
                    ? double.PositiveInfinity
                    : placed.Min(x => Distance(startPoint, endPoint, x.Start, x.End) - diameter);
                var guideClearance = guides.Count == 0
                    ? double.PositiveInfinity
                    : guides.Min(g => Distance(startPoint, endPoint, g.Base, g.BoreEnd) - rc - (g.BoreDiameterMm / 2));
                var where = Invariant($"{acrossName} = {position:0.##}");

                if (cavity < cavityClearance)
                {
                    skipped.Add(Invariant($"{where} ({Math.Max(0, cavity):0.##} mm from the cavity)"));
                    continue;
                }

                if (pinClearance < o.FeatureClearanceMm)
                {
                    skipped.Add(Invariant($"{where} (would pass {Math.Max(0, pinClearance):0.##} mm from an ejector pin)"));
                    continue;
                }

                if (feedClearance < o.FeatureClearanceMm)
                {
                    skipped.Add(Invariant($"{where} (would pass {Math.Max(0, feedClearance):0.##} mm from the feed)"));
                    continue;
                }

                if (channelClearance < o.FeatureClearanceMm)
                {
                    skipped.Add(Invariant($"{where} (too close to another channel)"));
                    continue;
                }

                if (guideClearance < o.FeatureClearanceMm)
                {
                    skipped.Add(Invariant($"{where} (would pass {Math.Max(0, guideClearance):0.##} mm from a guide pin)"));
                    continue;
                }

                placed.Add(new MoldCoolingChannelLayout(
                    Invariant($"cooling-channel-{channels.Count + placed.Count + 1:000}"),
                    half,
                    startPoint,
                    endPoint,
                    diameter,
                    cavity,
                    Math.Min(edgeAcross, room),
                    pinClearance,
                    feedClearance));
            }

            channels.AddRange(placed);
            var positions = string.Join(", ", placed.Select(x => Invariant($"{Vec3d.Dot(ToVec(x.Start), across):0.##}")));
            var text = Invariant($"{sign} half at {c.AxisName} = {level:0.##} mm: {placed.Count} of {count}") +
                (placed.Count > 0 ? Invariant($" ({acrossName} = {positions})") : string.Empty);

            if (skipped.Count > 0)
            {
                text += "; skipped " + string.Join(", ", skipped);
            }

            halfNotes.Add(text);
        }

        notes.Add(Invariant($"Cooling: channels {diameter:0.##} mm drilled through the block along {alongName}, in each half {cavityClearance:0.##} mm of steel from the cavity (from the channel wall) and {edge:0.##} mm from the block faces, spaced {request.CoolingChannelSpacingMm:0.##} mm across {acrossName} and centred on the cavity; ") + string.Join("; ", halfNotes) + ".");
        return channels;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Guide pins

    /// <summary>
    /// Guide pins near the corners of the block, inside the steel the block keeps from its faces: each stands on the
    /// moving half and enters a bore of the fixed half. A corner whose bore would come within the feature clearance of
    /// the cavity or of another feature moves towards the centre of the block, 1 mm at a time, while it stays out of the
    /// cavity footprint; otherwise it gets no pin.
    /// </summary>
    private static List<MoldGuidePinLayout> PlanGuides(
        Context c,
        MoldLayoutRequest request,
        MoldLayoutOptions o,
        int moving,
        IReadOnlyList<MoldFeedSegment> feed,
        IReadOnlyList<MoldEjectorPinLayout> pins,
        List<string> notes)
    {
        var guides = new List<MoldGuidePinLayout>();

        if (o.GuidePinCount <= 0 || !(o.GuidePinDiameterMm > 0))
        {
            return guides;
        }

        var diameter = o.GuidePinDiameterMm;
        var boreRadius = (diameter / 2) + request.GuideBoreClearanceMm;
        var fixedFace = moving > 0 ? c.H(c.BlockMin) : c.H(c.BlockMax);
        var fixedThickness = Math.Abs(fixedFace - c.Offset);

        // The pin enters the fixed half by 1.5 diameters, less when the half is thinner, leaving steel behind the bore.
        var protrusion = Math.Min(o.GuidePinProtrusionToDiameter * diameter, fixedThickness - request.GuideBoreClearanceMm - o.FeatureClearanceMm);

        if (protrusion < 0.5 * diameter)
        {
            notes.Add(Invariant($"Guide pins: none, the fixed half is {fixedThickness:0.##} mm thick behind the parting plane; a {diameter:0.##} mm pin needs {(0.5 * diameter) + request.GuideBoreClearanceMm + o.FeatureClearanceMm:0.##} mm."));
            return guides;
        }

        var towards = -moving;
        var inset = request.MoldEdgeClearanceMm + boreRadius;
        var (u0, u1) = (c.U(c.BlockMin) + inset, c.U(c.BlockMax) - inset);
        var (v0, v1) = (c.V(c.BlockMin) + inset, c.V(c.BlockMax) - inset);

        // The halves are sampled on a grid of VoxelGridMm. A pin or bore whose circle passes through grid nodes (round
        // corners and radii put it on four of them) came out of the Windows run with edges shared by more than two
        // triangles at exactly those nodes. A corner a quarter of a voxel inside the grid keeps the circles off them.
        if (request.VoxelGridMm > 0)
        {
            (u0, u1) = (OffGrid(u0, 1, request.VoxelGridMm), OffGrid(u1, -1, request.VoxelGridMm));
            (v0, v1) = (OffGrid(v0, 1, request.VoxelGridMm), OffGrid(v1, -1, request.VoxelGridMm));
        }

        var centreU = (c.U(c.BlockMin) + c.U(c.BlockMax)) / 2;
        var centreV = (c.V(c.BlockMin) + c.V(c.BlockMax)) / 2;
        var corners = new[] { (u0, v0), (u1, v0), (u1, v1), (u0, v1) }.Take(o.GuidePinCount).ToArray();
        var skipped = new List<string>();

        foreach (var (cu, cv) in corners)
        {
            var du = centreU - cu;
            var dv = centreV - cv;
            var length = Math.Sqrt((du * du) + (dv * dv));
            MoldGuidePinLayout? placed = null;

            for (var step = 0; step <= 200 && length > 0; step++)
            {
                var u = cu + (du / length * step);
                var v = cv + (dv / length * step);

                // Stop before the bore reaches over the cavity.
                if (u + boreRadius > c.Min1 && u - boreRadius < c.Max1 && v + boreRadius > c.Min2 && v - boreRadius < c.Max2)
                {
                    break;
                }

                var basePoint = c.Point(u, v, c.Offset);
                var boreEnd = c.Point(u, v, c.Offset + (towards * (protrusion + request.GuideBoreClearanceMm)));
                var clearance = c.Bvh.SegmentDistance(basePoint, boreEnd) - boreRadius;
                var b = ToPoint(basePoint);
                var e = ToPoint(boreEnd);

                foreach (var s in feed)
                {
                    clearance = Math.Min(clearance, Distance(b, e, s.Start, s.End) - boreRadius - (s.DiameterMm / 2));
                }

                foreach (var p in pins)
                {
                    clearance = Math.Min(clearance, Distance(b, e, p.Tip, p.End) - boreRadius - (p.DiameterMm / 2));
                }

                foreach (var g in guides)
                {
                    clearance = Math.Min(clearance, Distance(b, e, g.Base, g.BoreEnd) - (2 * boreRadius));
                }

                if (clearance >= o.FeatureClearanceMm)
                {
                    placed = new MoldGuidePinLayout(
                        Invariant($"guide-pin-{guides.Count + 1:000}"),
                        b,
                        ToPoint(c.Point(u, v, c.Offset + (towards * protrusion))),
                        diameter,
                        e,
                        2 * boreRadius,
                        clearance);
                    break;
                }
            }

            if (placed is null)
            {
                skipped.Add(Invariant($"({c.AxisPlaneName(cu, cv)})"));
            }
            else
            {
                guides.Add(placed);
            }
        }

        var text = Invariant($"Guide pins: {guides.Count} of {corners.Length} pins {diameter:0.##} mm near the corners of the block, standing on the moving half and entering {protrusion:0.##} mm into bores of {2 * boreRadius:0.##} mm in the fixed half; they align the halves and keep them parallel as the mold opens along {c.AxisName}.");

        if (skipped.Count > 0)
        {
            text += " No room near the corner " + string.Join(", ", skipped) + Invariant($" (the bore would come within {o.FeatureClearanceMm:0.##} mm of the cavity or another feature).");
        }

        notes.Add(text);
        return guides;
    }

    /// <summary>
    /// The position nearest to <paramref name="x"/>, on its inner side (<paramref name="inward"/> +1: towards larger
    /// values, -1: towards smaller ones), that lies a quarter of <paramref name="grid"/> off the grid.
    /// </summary>
    internal static double OffGrid(double x, int inward, double grid)
    {
        var k = inward > 0 ? Math.Ceiling((x - (0.25 * grid)) / grid) : Math.Floor((x + (0.25 * grid)) / grid);
        return (k * grid) + (inward * 0.25 * grid);
    }

    /// <summary>Offsets across the cavity centre: 0, +s, -s, +2s, ... for an odd count; +s/2, -s/2, +3s/2, ... for an even one.</summary>
    private static IEnumerable<double> Offsets(int count, double spacing, double span)
    {
        if (!(spacing > 0))
        {
            yield return 0;
            yield break;
        }

        var start = count % 2 == 1 ? 0.0 : spacing / 2;

        if (start == 0)
        {
            yield return 0;
            start = spacing;
        }

        for (var offset = start; offset <= span; offset += spacing)
        {
            yield return offset;
            yield return -offset;
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Geometry helpers

    /// <summary>Distance between two straight centrelines (segments).</summary>
    public static double Distance(MoldLayoutPoint p1, MoldLayoutPoint q1, MoldLayoutPoint p2, MoldLayoutPoint q2) =>
        Math.Sqrt(TriangleBvh.SegmentSegmentDistanceSquared(ToVec(p1), ToVec(q1), ToVec(p2), ToVec(q2)));

    private static void CountEdge(Dictionary<(Vec3d, Vec3d), int> edges, Vec3d a, Vec3d b)
    {
        var key = Compare(a, b) <= 0 ? (a, b) : (b, a);
        edges[key] = edges.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    private static int Compare(Vec3d a, Vec3d b)
    {
        var x = a.X.CompareTo(b.X);

        if (x != 0)
        {
            return x;
        }

        var y = a.Y.CompareTo(b.Y);
        return y != 0 ? y : a.Z.CompareTo(b.Z);
    }

    private static bool PointInTriangle(double u, double v, (double U0, double V0, double U1, double V1, double U2, double V2) t)
    {
        var d1 = ((u - t.U1) * (t.V0 - t.V1)) - ((t.U0 - t.U1) * (v - t.V1));
        var d2 = ((u - t.U2) * (t.V1 - t.V2)) - ((t.U1 - t.U2) * (v - t.V2));
        var d3 = ((u - t.U0) * (t.V2 - t.V0)) - ((t.U2 - t.U0) * (v - t.V0));
        var negative = d1 < 0 || d2 < 0 || d3 < 0;
        var positive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(negative && positive);
    }

    private static (double Distance, double U, double V) PointSegment(double u, double v, double u0, double v0, double u1, double v1)
    {
        var du = u1 - u0;
        var dv = v1 - v0;
        var lengthSquared = (du * du) + (dv * dv);
        var t = lengthSquared > 0 ? Math.Clamp((((u - u0) * du) + ((v - v0) * dv)) / lengthSquared, 0, 1) : 0;
        var pu = u0 + (du * t);
        var pv = v0 + (dv * t);
        return (Math.Sqrt(Square(u - pu) + Square(v - pv)), pu, pv);
    }

    private static double InPlaneFaceDistance(Context c, Vec3d p) => Math.Min(
        Math.Min(c.U(p) - c.U(c.BlockMin), c.U(c.BlockMax) - c.U(p)),
        Math.Min(c.V(p) - c.V(c.BlockMin), c.V(c.BlockMax) - c.V(p)));

    private static double Length(MoldFeedSegment s) =>
        Math.Sqrt(Square(s.End.X - s.Start.X) + Square(s.End.Y - s.Start.Y) + Square(s.End.Z - s.Start.Z));

    private static string AxisName(Vec3d axis) => axis.X != 0 ? "X" : axis.Y != 0 ? "Y" : "Z";

    private static MoldLayoutPoint ToPoint(Vec3d v) => new(v.X, v.Y, v.Z);

    private static Vec3d ToVec(MoldLayoutPoint p) => new(p.X, p.Y, p.Z);

    private static double Square(double x) => x * x;

    private static double Snap(double value) => Math.Abs(value) < 1e-12 ? 0 : value;

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Uniform grid of buckets over a rectangle; an item goes into every bucket its box overlaps.</summary>
    private sealed class Grid2D
    {
        private readonly double _minU;
        private readonly double _minV;
        private readonly double _size;
        private readonly int _nu;
        private readonly int _nv;
        private readonly List<int>[] _buckets;

        public Grid2D(double minU, double minV, double maxU, double maxV, double size)
        {
            _minU = minU;
            _minV = minV;
            _size = Math.Max(size, Math.Max(maxU - minU, maxV - minV) / 4000);
            _nu = (int)Math.Floor((maxU - minU) / _size) + 1;
            _nv = (int)Math.Floor((maxV - minV) / _size) + 1;
            _buckets = new List<int>[_nu * _nv];
        }

        public void Add(int item, double u0, double v0, double u1, double v1)
        {
            var (i0, j0) = Cell(u0, v0);
            var (i1, j1) = Cell(u1, v1);

            for (var j = j0; j <= j1; j++)
            {
                for (var i = i0; i <= i1; i++)
                {
                    (_buckets[(j * _nu) + i] ??= new List<int>()).Add(item);
                }
            }
        }

        public IEnumerable<int> At(double u, double v)
        {
            var (i, j) = Cell(u, v);
            return _buckets[(j * _nu) + i] ?? (IEnumerable<int>)Array.Empty<int>();
        }

        /// <summary>Items in the buckets within <paramref name="radius"/> of (u, v); an item may come more than once.</summary>
        public IEnumerable<int> Around(double u, double v, double radius)
        {
            var (i0, j0) = Cell(u - radius, v - radius);
            var (i1, j1) = Cell(u + radius, v + radius);

            for (var j = j0; j <= j1; j++)
            {
                for (var i = i0; i <= i1; i++)
                {
                    var bucket = _buckets[(j * _nu) + i];

                    if (bucket is null)
                    {
                        continue;
                    }

                    foreach (var item in bucket)
                    {
                        yield return item;
                    }
                }
            }
        }

        private (int I, int J) Cell(double u, double v) => (
            Math.Clamp((int)Math.Floor((u - _minU) / _size), 0, _nu - 1),
            Math.Clamp((int)Math.Floor((v - _minV) / _size), 0, _nv - 1));
    }

    private sealed class Context
    {
        public required IReadOnlyList<MeshTriangle> Triangles { get; init; }

        public required TriangleBvh Bvh { get; init; }

        public required Vec3d Pull { get; init; }

        public required Vec3d SideA { get; init; }

        public required Vec3d SideB { get; init; }

        public required int AxisIndex { get; init; }

        public required string AxisName { get; init; }

        public required double Offset { get; init; }

        public required double Resolution { get; init; }

        public required Vec3d BlockMin { get; init; }

        public required Vec3d BlockMax { get; init; }

        public required Vec3d[] Normals { get; init; }

        public required Vec3d[] Centroids { get; init; }

        public required double[] Areas { get; init; }

        public required int[] Halves { get; init; }

        public required Vec3d Center { get; init; }

        public required double MinHeight { get; init; }

        public required double MaxHeight { get; init; }

        public required double Min1 { get; init; }

        public required double Max1 { get; init; }

        public required double Min2 { get; init; }

        public required double Max2 { get; init; }

        public double H(Vec3d v) => Vec3d.Dot(v, Pull);

        public double U(Vec3d v) => Vec3d.Dot(v, SideA);

        public double V(Vec3d v) => Vec3d.Dot(v, SideB);

        public Vec3d Point(double u, double v, double h) => (SideA * u) + (SideB * v) + (Pull * h);

        public Vec3d WithHeight(Vec3d p, double h) => Point(U(p), V(p), h);

        /// <summary>A point of the plane written as its two in-plane coordinates, e.g. "Y 14.5, Z -10.5".</summary>
        public string AxisPlaneName(double u, double v) =>
            string.Create(CultureInfo.InvariantCulture, $"{MoldLayoutPlanner.AxisName(SideA)} {u:0.##}, {MoldLayoutPlanner.AxisName(SideB)} {v:0.##}");

        /// <summary>
        /// Smallest distance from the segment [p, q] to the faces of the block, leaving out the face on side
        /// <paramref name="excludedSide"/> (+1 max, -1 min) of axis <paramref name="excludedAxis"/>.
        /// </summary>
        public double FaceDistance(Vec3d p, Vec3d q, int excludedAxis = -1, int excludedSide = 0)
        {
            var best = double.PositiveInfinity;

            for (var axis = 0; axis < 3; axis++)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    if (axis == excludedAxis && side == excludedSide)
                    {
                        continue;
                    }

                    var dp = side < 0 ? p[axis] - BlockMin[axis] : BlockMax[axis] - p[axis];
                    var dq = side < 0 ? q[axis] - BlockMin[axis] : BlockMax[axis] - q[axis];
                    best = Math.Min(best, Math.Min(dp, dq));
                }
            }

            return best;
        }

        public static Context Create(MoldLayoutRequest request, MoldLayoutOptions options)
        {
            var (pull, sideA, sideB, index) = request.Axis switch
            {
                PartingAxis.X => (new Vec3d(1, 0, 0), new Vec3d(0, 1, 0), new Vec3d(0, 0, 1), 0),
                PartingAxis.Y => (new Vec3d(0, 1, 0), new Vec3d(0, 0, 1), new Vec3d(1, 0, 0), 1),
                PartingAxis.Z => (new Vec3d(0, 0, 1), new Vec3d(1, 0, 0), new Vec3d(0, 1, 0), 2),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Axis, "Unsupported parting axis.")
            };

            var triangles = request.CavityTriangles;
            var signedVolume = 0.0;

            foreach (var t in triangles)
            {
                signedVolume += Vec3d.Dot(Vec3d.From(t.A), Vec3d.Cross(Vec3d.From(t.B), Vec3d.From(t.C))) / 6.0;
            }

            var sign = signedVolume < 0 ? -1.0 : 1.0;
            var count = triangles.Count;
            var normals = new Vec3d[count];
            var centroids = new Vec3d[count];
            var areas = new double[count];
            var halves = new int[count];
            var r = options.ResolutionMm;
            var offset = request.PartingOffsetMm;

            for (var i = 0; i < count; i++)
            {
                var t = triangles[i];
                var a = Vec3d.From(t.A);
                var b = Vec3d.From(t.B);
                var d = Vec3d.From(t.C);
                var cross = Vec3d.Cross(b - a, d - a);
                var length = cross.Length;
                areas[i] = double.IsFinite(length) ? length / 2 : 0;
                normals[i] = length > 0 ? cross * (sign / length) : default;
                centroids[i] = (a + b + d) * (1.0 / 3.0);
                var height = Vec3d.Dot(centroids[i], pull) - offset;
                halves[i] = Math.Abs(height) > r ? Math.Sign(height) : Vec3d.Dot(normals[i], pull) >= 0 ? 1 : -1;
            }

            var bounds = MeshBounds.Of(triangles);
            var min = Vec3d.From(bounds.Min);
            var max = Vec3d.From(bounds.Max);
            var block = request.Block;

            return new Context
            {
                Triangles = triangles,
                Bvh = new TriangleBvh(triangles),
                Pull = pull,
                SideA = sideA,
                SideB = sideB,
                AxisIndex = index,
                AxisName = request.Axis.ToString(),
                Offset = offset,
                Resolution = r,
                BlockMin = new Vec3d(block.MinX, block.MinY, block.MinZ),
                BlockMax = new Vec3d(block.MaxX, block.MaxY, block.MaxZ),
                Normals = normals,
                Centroids = centroids,
                Areas = areas,
                Halves = halves,
                Center = (min + max) * 0.5,
                MinHeight = Vec3d.Dot(min, pull),
                MaxHeight = Vec3d.Dot(max, pull),
                Min1 = Vec3d.Dot(min, sideA),
                Max1 = Vec3d.Dot(max, sideA),
                Min2 = Vec3d.Dot(min, sideB),
                Max2 = Vec3d.Dot(max, sideB)
            };
        }
    }
}
