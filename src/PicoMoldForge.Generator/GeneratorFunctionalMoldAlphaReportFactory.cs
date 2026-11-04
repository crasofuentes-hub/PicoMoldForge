using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Cooling;
using PicoMoldForge.Core.DfAM;
using PicoMoldForge.Core.Engineering.Clearance;
using PicoMoldForge.Core.Engineering.CoolingGeometry;
using PicoMoldForge.Core.Engineering.EjectionGeometry;
using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.MoldLayout;
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
            "Gate/runner/sprue, ejector, cooling and clearance summaries are computed from the feed, ejector pins and cooling channels placed around the cavity (see the Feed, Ejector pins and Cooling notes and MoldLayout); they are not yet cut into the mold halves.",
            "Clearances are measured between the straight centrelines of the features and from them to the cavity triangles."
        };

        warnings.AddRange(plan.Notes);

        var triangles = plan.Triangles;

        // Wall thickness measured on the input STL (inscribed-ball method, no PicoGK runtime) by the mold plan and
        // judged against the DfAM minimum.
        var wallThickness = plan.WallThickness.Analysis.Summary;
        warnings.AddRange(plan.WallThickness.Notes);
        var moldAccess = CreateMoldAccess(triangles, splitAxis, partingOffsetMm, warnings);
        warnings.AddRange(plan.LayoutNotes);

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
            CoolingChannels: new CoolingChannelSubtractor().PlanSubtraction(GeneratorMoldLayout.CoolingInput(input, plan.Layout)).Summary,
            GateRunnerSprue: new GateRunnerSprueGenerator().Plan(GeneratorMoldLayout.FeedInput(input, plan.Layout)).Summary,
            EjectorCandidates: new EjectorCandidateGenerator().Plan(GeneratorMoldLayout.EjectorInput(input, plan.Layout)).Summary,
            ClearanceMatrix: new ClearanceCollisionMatrix().Evaluate(GeneratorMoldLayout.ClearanceInput(plan.Layout)).Summary,
            PartingPlane: partingPlaneScoring.BestScore,
            MoldBlockFit: plan.BlockFit,
            Shrinkage: plan.ShrinkageSummary,
            MoldLayout: plan.Layout is null ? null : MoldLayoutSummary.From(plan.Layout));
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