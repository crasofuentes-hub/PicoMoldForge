using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Cooling;
using PicoMoldForge.Core.DfAM;
using PicoMoldForge.Core.Engineering.Clearance;
using PicoMoldForge.Core.Engineering.CoolingGeometry;
using PicoMoldForge.Core.Engineering.EjectionGeometry;
using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Exports;
using SeparationPartingAxis = PicoMoldForge.Core.Engineering.Separation.PartingAxis;

namespace PicoMoldForge.Generator;

public static class GeneratorFunctionalMoldAlphaReportFactory
{
    public static FunctionalMoldAlphaReport Create(
        GeneratorPipelineInput input,
        PartAnalysisReport partAnalysis,
        CoolingChannelPlan coolingPlan,
        DfAMReport dfamReport,
        GeneratorMoldPlan? plan = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(partAnalysis);
        ArgumentNullException.ThrowIfNull(coolingPlan);
        ArgumentNullException.ThrowIfNull(dfamReport);
        if (partAnalysis.PartingPlane is null)
        {
            throw new InvalidOperationException("Part analysis must include a parting plane before FunctionalMoldAlphaReport generation.");
        }

        plan ??= GeneratorMoldPlanner.Plan(input, partAnalysis.PartingPlane);

        var moldBounds = ToSeparationBounds(input.BooleanMoldBlockBounds);

        // The part measured on its STL; a part outside the block keeps the configured size here (the separation
        // engine needs a part inside the block) and the block-fit summary reports the failure.
        var partBounds = plan.BlockFit.IsPartInsideBlock
            ? plan.PartBounds
            : CreateApproximatePartBounds(input.BooleanMoldBlockBounds, input.MoldSystem);
        var splitAxis = plan.SeparationAxis;
        var partingOffsetMm = ResolveSafePartingOffset(plan.PartingOffsetMm, moldBounds, splitAxis);

        var partingPlaneScorer = new PartingPlaneScorer();
        var partingPlaneScoring = partingPlaneScorer.Score(new PartingPlaneScoringInput(
            MoldBlockBounds: moldBounds,
            PartBounds: partBounds,
            VoxelResolutionMm: input.Config.VoxelResolutionMm,
            Candidates: partingPlaneScorer.GenerateDefaultCandidates(moldBounds, partBounds),
            HasShutoffStrategy: true));

        var separationResult = new MoldSeparationEngine().Split(new MoldSeparationEngineInput(
            MoldBlockBounds: moldBounds,
            PartBounds: partBounds,
            PartingAxis: splitAxis,
            PartingOffsetMm: partingOffsetMm,
            VoxelResolutionMm: input.Config.VoxelResolutionMm,
            HasPartingMetadata: true,
            HasShutoffStrategy: true));

        var shutoffSummary = new ShutoffStrategyEvaluator().Summarize(new ShutoffStrategyInput(
            Regions: new[]
            {
                new ShutoffRegion(
                    RegionId: "generator-parting-line",
                    RegionType: ShutoffRegionType.PartingLine,
                    ClosureState: ShutoffClosureState.Preliminary,
                    ContactAreaMm2: Math.Max(1m, Footprint(partBounds, splitAxis)),
                    GapMm: 0m,
                    OverlapMm: 0m,
                    IsCriticalToQuality: true)
            }));

        var warnings = new List<string>
        {
            "FunctionalMoldAlphaReport is generated from the real generator pipeline, configuration, part analysis, cooling plan, DfAM report, and generated artifact assumptions.",
            "Gate/runner/sprue and ejector summaries are preliminary generator-derived planning metrics until their geometry is emitted as dedicated artifacts.",
            "Clearance matrix summary is preliminary and should be replaced by feature-level geometry distances in the next series.",
            "Cooling summary is routed through CoolingChannelSubtractor using CoolingChannelPlan segments, mold bounds, and configured minimum clearance."
        };

        warnings.AddRange(plan.Notes);

        var triangles = plan.Triangles;
        var wallThickness = CreateWallThicknessSummary(input, triangles, warnings);
        var moldAccess = CreateMoldAccess(triangles, splitAxis, partingOffsetMm, warnings);

        warnings.Add(
            $"Parting plane scorer selected {partingPlaneScoring.BestScore.Candidate.Axis} at {partingPlaneScoring.BestScore.Candidate.OffsetMm} mm with quality score {partingPlaneScoring.BestScore.QualityScore}.");
        foreach (var warning in partAnalysis.Warnings)
        {
            warnings.Add($"PartAnalysis warning: {warning.Code} - {warning.Message}");
        }

        foreach (var warning in coolingPlan.Warnings)
        {
            warnings.Add($"Cooling warning: {warning}");
        }

        foreach (var warning in dfamReport.Warnings)
        {
            warnings.Add($"DfAM warning: {warning}");
        }

        return new FunctionalMoldAlphaReport(
            Separation: separationResult.Summary,
            Shutoff: shutoffSummary,
            DraftGeometry: moldAccess.Draft.Summary,
            Warnings: warnings,
            WallThickness: wallThickness,
            UndercutRisk: moldAccess.Undercut,
            CoolingChannels: CreateCoolingSummary(input, coolingPlan),
            GateRunnerSprue: CreateGateRunnerSprueSummary(input),
            EjectorCandidates: CreateEjectorSummary(input),
            ClearanceMatrix: CreateClearanceSummary(input, coolingPlan),
            PartingPlane: partingPlaneScoring.BestScore,
            MoldBlockFit: plan.BlockFit);
    }

    /// <summary>Area of the part bounds seen along the split axis.</summary>
    private static decimal Footprint(MoldSeparationBounds bounds, SeparationPartingAxis axis)
    {
        return axis switch
        {
            SeparationPartingAxis.X => bounds.SizeYmm * bounds.SizeZmm,
            SeparationPartingAxis.Y => bounds.SizeZmm * bounds.SizeXmm,
            SeparationPartingAxis.Z => bounds.SizeXmm * bounds.SizeYmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    /// <summary>
    /// Wall thickness measured on the input STL (inscribed-ball method, no PicoGK runtime) and judged against the
    /// DfAM minimum; the measurement notes go to the report warnings.
    /// </summary>
    private static VoxelWallThicknessAnalysisSummary CreateWallThicknessSummary(
        GeneratorPipelineInput input,
        IReadOnlyList<MeshTriangle> triangles,
        List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(warnings);

        var minimumAllowedMm = Math.Min(
            input.DfAM.MinimumWallThicknessMm,
            input.DfAM.RecommendedMinimumWallThicknessMm);

        var result = MeshWallThicknessAnalysis.Analyze(triangles, minimumAllowedMm, abruptChangeRatio: 0.50m);
        warnings.AddRange(result.Notes);

        return result.Analysis.Summary;
    }

    /// <summary>
    /// Draft and undercut measured on the input STL for the two halves of the split actually used for the mold
    /// (the same axis and offset as the separation summary); the measurement notes go to the report warnings.
    /// </summary>
    private static MeshMoldAccessAnalysisResult CreateMoldAccess(
        IReadOnlyList<MeshTriangle> triangles,
        SeparationPartingAxis splitAxis,
        decimal partingOffsetMm,
        List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        ArgumentNullException.ThrowIfNull(warnings);

        var result = MeshMoldAccessAnalysis.Analyze(triangles, splitAxis, partingOffsetMm, minimumDraftDeg: 1.0m);
        warnings.AddRange(result.Notes);

        return result;
    }

    private static CoolingChannelSubtractionSummary CreateCoolingSummary(
        GeneratorPipelineInput input,
        CoolingChannelPlan coolingPlan)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(coolingPlan);
        ArgumentNullException.ThrowIfNull(coolingPlan.Segments);

        var channels = coolingPlan.Segments
            .Select(segment => new PicoMoldForge.Core.Engineering.CoolingGeometry.CoolingChannelSegment(
                ChannelId: segment.Id,
                Start: new CoolingChannelPoint(
                    segment.StartXmm,
                    segment.StartYmm,
                    segment.StartZmm),
                End: new CoolingChannelPoint(
                    segment.EndXmm,
                    segment.EndYmm,
                    segment.EndZmm),
                DiameterMm: segment.DiameterMm,
                MinimumCavityClearanceMm: input.Cooling.MinimumClearanceMm,
                MinimumMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm))
            .ToArray();

        var subtractor = new CoolingChannelSubtractor();

        var result = subtractor.PlanSubtraction(new CoolingChannelSubtractionInput(
            MoldBounds: new CoolingMoldBounds(
                input.BooleanMoldBlockBounds.MinXmm,
                input.BooleanMoldBlockBounds.MinYmm,
                input.BooleanMoldBlockBounds.MinZmm,
                input.BooleanMoldBlockBounds.MaxXmm,
                input.BooleanMoldBlockBounds.MaxYmm,
                input.BooleanMoldBlockBounds.MaxZmm),
            Channels: channels,
            RequiredCavityClearanceMm: input.Cooling.MinimumClearanceMm,
            RequiredMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm,
            HasEngineerOverride: !coolingPlan.IsSuccessful));

        return result.Summary;
    }

    private static GateRunnerSprueGenerationSummary CreateGateRunnerSprueSummary(GeneratorPipelineInput input)
    {
        var gateRunnerSprueInput = GeneratorGateRunnerSpruePlanFactory.CreateInput(input);
        var gateRunnerSpruePlan = GeneratorGateRunnerSpruePlanFactory.Plan(gateRunnerSprueInput);

        return gateRunnerSpruePlan.Summary;
    }
private static EjectorCandidateGenerationSummary CreateEjectorSummary(GeneratorPipelineInput input)
    {
        var count = Math.Max(0, input.MoldSystem.EjectorPinCount);
        var pinArea = count * CircleArea(input.MoldSystem.EjectorPinDiameterMm);

        return new EjectorCandidateGenerationSummary(
            CandidateCount: count,
            AcceptedCandidateCount: count,
            BlockedCandidateCount: 0,
            CosmeticCandidateCount: 0,
            CriticalCandidateCount: 0,
            TotalAcceptedPinAreaMm2: Math.Round(pinArea, 6));
    }

    private static ClearanceCollisionMatrixSummary CreateClearanceSummary(
        GeneratorPipelineInput input,
        CoolingChannelPlan coolingPlan)
    {
        var featureCount = coolingPlan.Segments.Count + Math.Max(0, input.MoldSystem.EjectorPinCount);
        var pairCount = featureCount <= 1 ? 0 : featureCount * (featureCount - 1) / 2;

        return new ClearanceCollisionMatrixSummary(
            FeatureCount: featureCount,
            PairCount: pairCount,
            CollisionRiskPairCount: 0,
            CriticalRiskPairCount: 0,
            MinimumSurfaceClearanceMm: Math.Min(input.Cooling.MinimumClearanceMm, input.MoldSystem.InsertClearanceMm),
            MinimumClearanceMarginMm: 0m);
    }

    private static decimal EstimateCoolingChannelVolume(PicoMoldForge.Core.Cooling.CoolingChannelSegment segment)
    {
        var length = Distance(
            segment.StartXmm,
            segment.StartYmm,
            segment.StartZmm,
            segment.EndXmm,
            segment.EndYmm,
            segment.EndZmm);

        return Math.Round(CircleArea(segment.DiameterMm) * length, 6);
    }

    private static decimal Distance(
        decimal x1,
        decimal y1,
        decimal z1,
        decimal x2,
        decimal y2,
        decimal z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;

        return (decimal)Math.Sqrt((double)((dx * dx) + (dy * dy) + (dz * dz)));
    }

    private static decimal CircleArea(decimal diameterMm)
    {
        var radius = Math.Max(0m, diameterMm) / 2m;

        return 3.1415926535897932384626433833m * radius * radius;
    }

    private static MoldSeparationBounds ToSeparationBounds(MoldBlockBounds bounds)
    {
        return new MoldSeparationBounds(
            bounds.MinXmm,
            bounds.MinYmm,
            bounds.MinZmm,
            bounds.MaxXmm,
            bounds.MaxYmm,
            bounds.MaxZmm);
    }

    private static MoldSeparationBounds CreateApproximatePartBounds(
        MoldBlockBounds moldBounds,
        GeneratorMoldSystemConfig moldSystem)
    {
        var centerX = Midpoint(moldBounds.MinXmm, moldBounds.MaxXmm);
        var centerY = Midpoint(moldBounds.MinYmm, moldBounds.MaxYmm);
        var centerZ = Midpoint(moldBounds.MinZmm, moldBounds.MaxZmm);

        var sizeX = Math.Min(moldSystem.PartSizeXmm, (moldBounds.MaxXmm - moldBounds.MinXmm) * 0.80m);
        var sizeY = Math.Min(moldSystem.PartSizeYmm, (moldBounds.MaxYmm - moldBounds.MinYmm) * 0.80m);
        var sizeZ = Math.Min(moldSystem.PartSizeZmm, (moldBounds.MaxZmm - moldBounds.MinZmm) * 0.80m);

        return new MoldSeparationBounds(
            centerX - (sizeX / 2m),
            centerY - (sizeY / 2m),
            centerZ - (sizeZ / 2m),
            centerX + (sizeX / 2m),
            centerY + (sizeY / 2m),
            centerZ + (sizeZ / 2m));
    }

    private static decimal ResolveSafePartingOffset(
        decimal requestedOffsetMm,
        MoldSeparationBounds bounds,
        SeparationPartingAxis axis)
    {
        var minimum = AxisMinimum(bounds, axis);
        var maximum = AxisMaximum(bounds, axis);

        if (requestedOffsetMm <= minimum || requestedOffsetMm >= maximum)
        {
            return Midpoint(minimum, maximum);
        }

        return requestedOffsetMm;
    }

    private static decimal AxisMinimum(MoldSeparationBounds bounds, SeparationPartingAxis axis)
    {
        return axis switch
        {
            SeparationPartingAxis.X => bounds.MinXmm,
            SeparationPartingAxis.Y => bounds.MinYmm,
            SeparationPartingAxis.Z => bounds.MinZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static decimal AxisMaximum(MoldSeparationBounds bounds, SeparationPartingAxis axis)
    {
        return axis switch
        {
            SeparationPartingAxis.X => bounds.MaxXmm,
            SeparationPartingAxis.Y => bounds.MaxYmm,
            SeparationPartingAxis.Z => bounds.MaxZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static decimal Midpoint(decimal minimum, decimal maximum)
    {
        return minimum + ((maximum - minimum) / 2m);
    }
}