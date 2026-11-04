using System.Globalization;
using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Cooling;
using PicoMoldForge.Core.Engineering.Clearance;
using PicoMoldForge.Core.Engineering.CoolingGeometry;
using PicoMoldForge.Core.Engineering.EjectionGeometry;
using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.MoldLayout;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.MoldSystems;
using SeparationPartingAxis = PicoMoldForge.Core.Engineering.Separation.PartingAxis;

namespace PicoMoldForge.Generator;

/// <summary>
/// The mold layout of a generator run (<see cref="MoldLayoutPlanner"/>) in the forms the existing evaluators,
/// diagnostics and report take. The evaluators judge the measured clearances against:
/// <list type="bullet">
/// <item>feed: <see cref="MoldLayoutOptions.FeedCavityClearanceMm"/> from the cavity and cooling.minimumClearanceMm
/// from the block faces, as before;</item>
/// <item>cooling: cooling.minimumClearanceMm from the cavity (from the channel wall) and from the block faces;</item>
/// <item>ejector pins and the clearance matrix: <see cref="MoldLayoutOptions.FeatureClearanceMm"/> between
/// features.</item>
/// </list>
/// </summary>
public static class GeneratorMoldLayout
{
    /// <summary>Stands for "nothing to keep clear of" in the decimal evaluator inputs.</summary>
    public const decimal UnboundedClearanceMm = 1_000_000m;

    public static MoldLayoutRequest CreateRequest(
        GeneratorPipelineInput input,
        IReadOnlyList<MeshTriangle> cavityTriangles,
        SeparationPartingAxis axis,
        decimal moldPartingOffsetMm,
        double maximumWallThicknessMm)
    {
        ArgumentNullException.ThrowIfNull(input);
        var block = input.BooleanMoldBlockBounds;

        return new MoldLayoutRequest(
            cavityTriangles,
            axis,
            (double)moldPartingOffsetMm,
            new MoldLayoutBlock(
                (double)block.MinXmm,
                (double)block.MinYmm,
                (double)block.MinZmm,
                (double)block.MaxXmm,
                (double)block.MaxYmm,
                (double)block.MaxZmm),
            maximumWallThicknessMm,
            (double)input.MoldSystem.EjectorPinDiameterMm,
            input.MoldSystem.EjectorPinCount,
            (double)input.Cooling.ChannelDiameterMm,
            (double)input.Cooling.ChannelSpacingMm,
            input.Cooling.ChannelCount,
            (double)input.Cooling.MinimumClearanceMm,
            (double)input.Cooling.MinimumClearanceMm,
            GuideBoreClearanceMm: Math.Max(0.5, (double)input.Config.VoxelResolutionMm),
            VoxelGridMm: (double)input.Config.VoxelResolutionMm);
    }

    /// <summary>
    /// The features cut into (or added to) the functional halves: the feed when it works (gate and runner in both
    /// halves, sprue in the fixed half), the ejector pin holes in the moving half, the cooling channels in their half,
    /// the guide bores in the fixed half and the guide pins on the moving half. Holes run one voxel past the faces they
    /// open through, and guide pins one voxel into the moving half, so no skin of steel is left by the voxels.
    /// </summary>
    public static IReadOnlyList<MoldHalfFeature> HalfFeatures(MoldLayoutPlan? layout, double voxelSizeMm)
    {
        var features = new List<MoldHalfFeature>();

        if (layout is null)
        {
            return features;
        }

        var moving = layout.Halves.MovingHalf;
        var fixedHalf = -moving;
        var v = Math.Max(0, voxelSizeMm);

        if (layout.Feed is { IsFeasible: true } feed)
        {
            foreach (var s in feed.Segments)
            {
                var sprue = s.Type == GateSystemFeatureType.Sprue;
                features.Add(HalfFeature(s.Id, "Feed", sprue ? fixedHalf : 0, s.Start, s.End, s.DiameterMm / 2, roundEnds: !sprue, adds: false, 0, sprue ? v : 0));
            }
        }

        foreach (var pin in layout.Pins)
        {
            features.Add(HalfFeature(pin.Id, "EjectorPins", moving, pin.Tip, pin.End, pin.DiameterMm / 2, roundEnds: false, adds: false, v, v));
        }

        foreach (var channel in layout.Channels)
        {
            features.Add(HalfFeature(channel.Id, "Cooling", channel.Half, channel.Start, channel.End, channel.DiameterMm / 2, roundEnds: false, adds: false, v, v));
        }

        foreach (var guide in layout.GuidePins)
        {
            features.Add(HalfFeature(guide.Id.Replace("pin", "bore", StringComparison.Ordinal), "GuideBores", fixedHalf, guide.Base, guide.BoreEnd, guide.BoreDiameterMm / 2, roundEnds: false, adds: false, v, 0));
            features.Add(HalfFeature(guide.Id, "GuidePins", moving, guide.Base, guide.Tip, guide.DiameterMm / 2, roundEnds: false, adds: true, v, 0));
        }

        return features;
    }

    /// <summary>
    /// Sprue, runner and gate for <see cref="GateRunnerSprueGenerator"/>. The gate enters the cavity by design, so its
    /// cavity clearance is given as the required one when its inner end lies in the cavity and it does not meet the
    /// cavity again beyond its entry, and as 0 otherwise.
    /// </summary>
    public static GateRunnerSprueGenerationInput FeedInput(GeneratorPipelineInput input, MoldLayoutPlan? layout)
    {
        ArgumentNullException.ThrowIfNull(input);
        var required = (decimal)(layout?.Options.FeedCavityClearanceMm ?? new MoldLayoutOptions().FeedCavityClearanceMm);
        var segments = new List<GateRunnerSprueSegment>();
        var feed = layout?.Feed;

        if (feed is not null)
        {
            foreach (var type in new[] { GateSystemFeatureType.Sprue, GateSystemFeatureType.Runner, GateSystemFeatureType.Gate })
            {
                var segment = feed.Segments.First(s => s.Type == type);
                var diameter = Round(segment.DiameterMm);
                var cavity = type == GateSystemFeatureType.Gate
                    ? feed.GateReachesCavity && feed.GateClearBeyondEntry ? required : 0m
                    : Clearance(segment.CavityClearanceMm);

                segments.Add(new GateRunnerSprueSegment(
                    FeatureId: "generator-" + type.ToString().ToLowerInvariant() + "-1",
                    FeatureType: type,
                    Start: new GateSystemPoint(Round(segment.Start.X), Round(segment.Start.Y), Round(segment.Start.Z)),
                    End: new GateSystemPoint(Round(segment.End.X), Round(segment.End.Y), Round(segment.End.Z)),
                    HydraulicDiameterMm: diameter,
                    FlowAreaMm2: CircleArea(diameter),
                    MinimumCavityClearanceMm: cavity,
                    MinimumMoldEdgeClearanceMm: Clearance(segment.MoldEdgeClearanceMm),
                    IsCriticalToQuality: type != GateSystemFeatureType.Runner));
            }
        }

        var block = input.BooleanMoldBlockBounds;

        return new GateRunnerSprueGenerationInput(
            MoldBounds: new GateSystemBounds(block.MinXmm, block.MinYmm, block.MinZmm, block.MaxXmm, block.MaxYmm, block.MaxZmm),
            Segments: segments,
            RequiredCavityClearanceMm: required,
            RequiredMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm);
    }

    /// <summary>The placed ejector pins for <see cref="EjectorCandidateGenerator"/>; each tip lies whole on its face.</summary>
    public static EjectorCandidateGenerationInput EjectorInput(GeneratorPipelineInput input, MoldLayoutPlan? layout)
    {
        ArgumentNullException.ThrowIfNull(input);
        var feature = (decimal)(layout?.Options.FeatureClearanceMm ?? new MoldLayoutOptions().FeatureClearanceMm);
        var tipArea = CircleArea(Round((double)input.MoldSystem.EjectorPinDiameterMm));
        var candidates = (layout?.Pins ?? Array.Empty<MoldEjectorPinLayout>())
            .Select(pin => new EjectorCandidate(
                CandidateId: pin.Id,
                Location: new EjectorCandidatePoint(Round(pin.Tip.X), Round(pin.Tip.Y), Round(pin.Tip.Z)),
                PinDiameterMm: Round(pin.DiameterMm),
                StrokeMm: Round(pin.StrokeMm),
                SupportedSurfaceAreaMm2: CircleArea(Round(pin.DiameterMm)),
                MinimumCoolingClearanceMm: Clearance(pin.CoolingClearanceMm),
                MinimumGateSystemClearanceMm: Clearance(pin.FeedClearanceMm),
                MinimumMoldEdgeClearanceMm: Clearance(pin.MoldEdgeClearanceMm)))
            .ToArray();
        var block = input.BooleanMoldBlockBounds;

        return new EjectorCandidateGenerationInput(
            MoldBounds: new EjectorLayoutBounds(block.MinXmm, block.MinYmm, block.MinZmm, block.MaxXmm, block.MaxYmm, block.MaxZmm),
            Candidates: candidates,
            RequiredCoolingClearanceMm: feature,
            RequiredGateSystemClearanceMm: feature,
            RequiredMoldEdgeClearanceMm: feature,
            MinimumSupportedSurfaceAreaMm2: tipArea);
    }

    /// <summary>The placed cooling channels for <see cref="CoolingChannelSubtractor"/>.</summary>
    public static CoolingChannelSubtractionInput CoolingInput(GeneratorPipelineInput input, MoldLayoutPlan? layout)
    {
        ArgumentNullException.ThrowIfNull(input);
        var channels = (layout?.Channels ?? Array.Empty<MoldCoolingChannelLayout>())
            .Select(channel => new PicoMoldForge.Core.Engineering.CoolingGeometry.CoolingChannelSegment(
                ChannelId: channel.Id,
                Start: new CoolingChannelPoint(Round(channel.Start.X), Round(channel.Start.Y), Round(channel.Start.Z)),
                End: new CoolingChannelPoint(Round(channel.End.X), Round(channel.End.Y), Round(channel.End.Z)),
                DiameterMm: Round(channel.DiameterMm),
                MinimumCavityClearanceMm: Clearance(channel.CavityClearanceMm),
                MinimumMoldEdgeClearanceMm: Clearance(channel.MoldEdgeClearanceMm)))
            .ToArray();
        var block = input.BooleanMoldBlockBounds;

        return new CoolingChannelSubtractionInput(
            MoldBounds: new CoolingMoldBounds(block.MinXmm, block.MinYmm, block.MinZmm, block.MaxXmm, block.MaxYmm, block.MaxZmm),
            Channels: channels,
            RequiredCavityClearanceMm: input.Cooling.MinimumClearanceMm,
            RequiredMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm);
    }

    /// <summary>Every placed feature for <see cref="ClearanceCollisionMatrix"/>; sprue, runner and gate form one group.</summary>
    public static ClearanceCollisionMatrixInput ClearanceInput(MoldLayoutPlan? layout)
    {
        var feature = (decimal)(layout?.Options.FeatureClearanceMm ?? new MoldLayoutOptions().FeatureClearanceMm);
        var features = new List<ClearanceFeature>();

        if (layout?.Feed is not null)
        {
            features.AddRange(layout.Feed.Segments.Select(s => Feature(s.Id, ClearanceFeatureKind.GateSystem, s.Start, s.End, s.DiameterMm, feature, "feed")));
        }

        if (layout is not null)
        {
            features.AddRange(layout.Pins.Select(p => Feature(p.Id, ClearanceFeatureKind.EjectorPin, p.Tip, p.End, p.DiameterMm, feature, null)));
            features.AddRange(layout.Channels.Select(c => Feature(c.Id, ClearanceFeatureKind.CoolingChannel, c.Start, c.End, c.DiameterMm, feature, null)));
        }

        return new ClearanceCollisionMatrixInput(features, feature);
    }

    /// <summary>The placed cooling channels as the plan the cooling diagnostic exporter draws.</summary>
    public static CoolingChannelPlan CoolingPlan(MoldLayoutPlan? layout)
    {
        var channels = layout?.Channels ?? Array.Empty<MoldCoolingChannelLayout>();
        var segments = channels
            .Select(channel => new PicoMoldForge.Core.Cooling.CoolingChannelSegment(
                Id: channel.Id,
                StartXmm: Round(channel.Start.X),
                StartYmm: Round(channel.Start.Y),
                StartZmm: Round(channel.Start.Z),
                EndXmm: Round(channel.End.X),
                EndYmm: Round(channel.End.Y),
                EndZmm: Round(channel.End.Z),
                DiameterMm: Round(channel.DiameterMm),
                Description: Invariant($"Straight channel drilled through the block in the {(channel.Half > 0 ? "+" : "-")} half, {channel.CavityClearanceMm:0.##} mm of steel from the cavity.")))
            .ToArray();

        var warnings = new[]
        {
            "Cooling channels are straight drilled channels placed around the cavity (MoldLayoutPlanner); see the cooling note.",
            "This plan does not evaluate thermal performance, pressure drop, or conformal cooling feasibility."
        };

        return new CoolingChannelPlan(IsSuccessful: true, Segments: segments, Warnings: warnings);
    }

    /// <summary>The placed ejector pins as the vertical pins the mold-system diagnostic draws; empty unless the split is across Z.</summary>
    public static IReadOnlyList<EjectorPin> VerticalPins(MoldLayoutPlan? layout)
    {
        if (layout is null || layout.Axis != SeparationPartingAxis.Z)
        {
            return Array.Empty<EjectorPin>();
        }

        return layout.Pins
            .Select(pin => new EjectorPin(
                Id: pin.Id,
                CenterXmm: Round(pin.Tip.X),
                CenterYmm: Round(pin.Tip.Y),
                StartZmm: Round(Math.Min(pin.Tip.Z, pin.End.Z)),
                EndZmm: Round(Math.Max(pin.Tip.Z, pin.End.Z)),
                DiameterMm: Round(pin.DiameterMm)))
            .ToArray();
    }

    /// <summary>
    /// The layout notes, with the file of each half: the splitter writes the - side of the split as
    /// BooleanCoreSide.stl and the + side as BooleanCavitySide.stl.
    /// </summary>
    public static IEnumerable<string> Notes(MoldLayoutPlan? layout)
    {
        if (layout is null)
        {
            yield return "Mold layout skipped: the cavity does not fit inside the mold block, so no feed, ejector pins or cooling were placed.";
            yield break;
        }

        var moving = layout.Halves.MovingHalf > 0 ? "BooleanCavitySide.stl" : "BooleanCoreSide.stl";
        var fixedHalf = layout.Halves.MovingHalf > 0 ? "BooleanCoreSide.stl" : "BooleanCavitySide.stl";
        yield return Invariant($"Mold halves: the moving half is {moving} (the {(layout.Halves.MovingHalf > 0 ? "+" : "-")} side of the split), the fixed half {fixedHalf}.");

        foreach (var note in layout.Notes)
        {
            yield return note;
        }
    }

    /// <summary>A feature from start to end, lengthened by <paramref name="beforeStart"/> and <paramref name="afterEnd"/>.</summary>
    private static MoldHalfFeature HalfFeature(string id, string system, int half, MoldLayoutPoint start, MoldLayoutPoint end, double radius, bool roundEnds, bool adds, double beforeStart, double afterEnd)
    {
        var (dx, dy, dz) = (end.X - start.X, end.Y - start.Y, end.Z - start.Z);
        var length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        var (ux, uy, uz) = length > 0 ? (dx / length, dy / length, dz / length) : (0.0, 0.0, 0.0);

        return new MoldHalfFeature(
            id,
            system,
            half,
            start.X - (ux * beforeStart),
            start.Y - (uy * beforeStart),
            start.Z - (uz * beforeStart),
            end.X + (ux * afterEnd),
            end.Y + (uy * afterEnd),
            end.Z + (uz * afterEnd),
            radius,
            roundEnds,
            adds);
    }

    private static ClearanceFeature Feature(string id, ClearanceFeatureKind kind, MoldLayoutPoint start, MoldLayoutPoint end, double diameter, decimal required, string? group) =>
        new(
            FeatureId: id,
            Kind: kind,
            Start: new ClearancePoint(Round(start.X), Round(start.Y), Round(start.Z)),
            End: new ClearancePoint(Round(end.X), Round(end.Y), Round(end.Z)),
            RadiusMm: Round(diameter) / 2m,
            RequiredClearanceMm: required,
            GroupId: group);

    private static decimal Clearance(double value) =>
        double.IsPositiveInfinity(value) ? UnboundedClearanceMm : double.IsFinite(value) ? Round(value) : 0m;

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private static decimal CircleArea(decimal diameterMm)
    {
        var radius = Math.Max(0m, diameterMm) / 2m;
        return 3.1415926535897932384626433833m * radius * radius;
    }

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
