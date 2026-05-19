using PicoMoldForge.Core.Engineering.Clearance;
using PicoMoldForge.Core.Engineering.CoolingGeometry;
using PicoMoldForge.Core.Engineering.DraftAnalysis;
using PicoMoldForge.Core.Engineering.EjectionGeometry;
using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.Undercuts;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Exports;

public sealed record FunctionalMoldAlphaReport(
    CoreCavitySeparationSummary? Separation,
    ShutoffStrategySummary? Shutoff,
    DraftBasicGeometryAnalysisSummary? DraftGeometry,
    IReadOnlyList<string> Warnings,
    VoxelWallThicknessAnalysisSummary? WallThickness = null,
    UndercutRiskAnalysisSummary? UndercutRisk = null,
    CoolingChannelSubtractionSummary? CoolingChannels = null,
    GateRunnerSprueGenerationSummary? GateRunnerSprue = null,
    EjectorCandidateGenerationSummary? EjectorCandidates = null,
    ClearanceCollisionMatrixSummary? ClearanceMatrix = null,
    PartingPlaneScore? PartingPlane = null)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Separation is null &&
            Shutoff is null &&
            DraftGeometry is null &&
            WallThickness is null &&
            UndercutRisk is null &&
            CoolingChannels is null &&
            GateRunnerSprue is null &&
            EjectorCandidates is null &&
            ClearanceMatrix is null &&
            PartingPlane is null)
        {
            errors.Add("At least one Functional Mold Alpha metric group is required.");
        }

        if (Warnings is null)
        {
            errors.Add("Warnings is required.");
        }

        ValidateSeparation(errors);
        ValidateShutoff(errors);
        ValidateDraftGeometry(errors);
        ValidateWallThickness(errors);
        ValidateUndercutRisk(errors);
        ValidateCoolingChannels(errors);
        ValidateGateRunnerSprue(errors);
        ValidateEjectorCandidates(errors);
        ValidateClearanceMatrix(errors);
        ValidatePartingPlane(errors);

        return errors;
    }

    public bool IsAlphaComplete =>
        Separation is not null &&
        Shutoff is not null &&
        DraftGeometry is not null &&
        WallThickness is not null &&
        UndercutRisk is not null &&
        CoolingChannels is not null &&
        GateRunnerSprue is not null &&
        EjectorCandidates is not null &&
        ClearanceMatrix is not null;

    public decimal OverallReadinessScore
    {
        get
        {
            var scores = new List<decimal>();

            if (Separation is not null)
            {
                scores.Add(Separation.QualityScore);
            }

            if (Shutoff is not null)
            {
                scores.Add(Shutoff.QualityScore);
            }

            if (DraftGeometry is not null)
            {
                scores.Add(ScoreFromRiskCounts(
                    DraftGeometry.FaceCount,
                    DraftGeometry.LowDraftCount + DraftGeometry.ZeroDraftCount + DraftGeometry.NegativeDraftCount + DraftGeometry.InvalidNormalCount));
            }

            if (WallThickness is not null)
            {
                scores.Add(ScoreFromRiskCounts(
                    WallThickness.SampleCount,
                    WallThickness.ThinCount + WallThickness.ThickCount + WallThickness.AbruptChangeCount + WallThickness.InvalidCount));
            }

            if (UndercutRisk is not null)
            {
                scores.Add(ScoreFromRiskCounts(
                    UndercutRisk.FaceCount,
                    UndercutRisk.LowPullClearanceCount + UndercutRisk.SideActionCandidateCount + UndercutRisk.UndercutCount + UndercutRisk.InvalidNormalCount));
            }

            if (CoolingChannels is not null)
            {
                scores.Add(ScoreFromRiskCounts(
                    CoolingChannels.ChannelCount,
                    CoolingChannels.BlockedChannelCount));
            }

            if (GateRunnerSprue is not null)
            {
                scores.Add(ScoreFromRiskCounts(
                    GateRunnerSprue.SegmentCount,
                    GateRunnerSprue.BlockedSegmentCount));
            }

            if (EjectorCandidates is not null)
            {
                scores.Add(ScoreFromRiskCounts(
                    EjectorCandidates.CandidateCount,
                    EjectorCandidates.BlockedCandidateCount));
            }

            if (ClearanceMatrix is not null)
            {
                scores.Add(ScoreFromRiskCounts(
                    ClearanceMatrix.PairCount,
                    ClearanceMatrix.CollisionRiskPairCount));
            }

            return scores.Count == 0
                ? 0m
                : Math.Round(scores.Average(), 6);
        }
    }

    public IReadOnlyList<string> MissingMetricGroups()
    {
        var missing = new List<string>();

        if (Separation is null) { missing.Add("Separation"); }
        if (Shutoff is null) { missing.Add("Shutoff"); }
        if (DraftGeometry is null) { missing.Add("DraftGeometry"); }
        if (WallThickness is null) { missing.Add("WallThickness"); }
        if (UndercutRisk is null) { missing.Add("UndercutRisk"); }
        if (CoolingChannels is null) { missing.Add("CoolingChannels"); }
        if (GateRunnerSprue is null) { missing.Add("GateRunnerSprue"); }
        if (EjectorCandidates is null) { missing.Add("EjectorCandidates"); }
        if (ClearanceMatrix is null) { missing.Add("ClearanceMatrix"); }

        return missing;
    }

    private static decimal ScoreFromRiskCounts(int totalCount, int riskyCount)
    {
        if (totalCount <= 0)
        {
            return 0m;
        }

        var score = 1m - (riskyCount / (decimal)totalCount);

        if (score < 0m)
        {
            return 0m;
        }

        if (score > 1m)
        {
            return 1m;
        }

        return Math.Round(score, 6);
    }

    private void ValidatePartingPlane(List<string> errors)
    {
        if (PartingPlane is null)
        {
            return;
        }

        if (PartingPlane.QualityScore < 0m || PartingPlane.QualityScore > 1m)
        {
            errors.Add("PartingPlane QualityScore must be between 0 and 1.");
        }

        if (PartingPlane.NormalizedPosition < 0m || PartingPlane.NormalizedPosition > 1m)
        {
            errors.Add("PartingPlane NormalizedPosition must be between 0 and 1.");
        }

        if (PartingPlane.BalanceRatio < 0m || PartingPlane.BalanceRatio > 1m)
        {
            errors.Add("PartingPlane BalanceRatio must be between 0 and 1.");
        }
    }
    private void ValidateSeparation(List<string> errors)
    {
        if (Separation is null)
        {
            return;
        }

        if (Separation.QualityScore < 0m || Separation.QualityScore > 1m)
        {
            errors.Add("Separation QualityScore must be between 0 and 1.");
        }

        if (Separation.OverlapRatio < 0m)
        {
            errors.Add("Separation OverlapRatio cannot be negative.");
        }

        if (Separation.GapRatio < 0m)
        {
            errors.Add("Separation GapRatio cannot be negative.");
        }
    }

    private void ValidateShutoff(List<string> errors)
    {
        if (Shutoff is null)
        {
            return;
        }

        if (Shutoff.QualityScore < 0m || Shutoff.QualityScore > 1m)
        {
            errors.Add("Shutoff QualityScore must be between 0 and 1.");
        }

        if (Shutoff.MaximumGapMm < 0m)
        {
            errors.Add("Shutoff MaximumGapMm cannot be negative.");
        }

        if (Shutoff.MaximumOverlapMm < 0m)
        {
            errors.Add("Shutoff MaximumOverlapMm cannot be negative.");
        }
    }

    private void ValidateDraftGeometry(List<string> errors)
    {
        if (DraftGeometry is null)
        {
            return;
        }

        if (DraftGeometry.FaceCount < 0)
        {
            errors.Add("DraftGeometry FaceCount cannot be negative.");
        }

        if (DraftGeometry.RiskySurfaceAreaMm2 < 0m)
        {
            errors.Add("DraftGeometry RiskySurfaceAreaMm2 cannot be negative.");
        }
    }

    private void ValidateWallThickness(List<string> errors)
    {
        if (WallThickness is null)
        {
            return;
        }

        if (WallThickness.SampleCount < 0)
        {
            errors.Add("WallThickness SampleCount cannot be negative.");
        }

        if (WallThickness.RiskySurfaceAreaMm2 < 0m)
        {
            errors.Add("WallThickness RiskySurfaceAreaMm2 cannot be negative.");
        }

        if (WallThickness.MinimumObservedThicknessMm < 0m ||
            WallThickness.MaximumObservedThicknessMm < 0m)
        {
            errors.Add("WallThickness observed thickness values cannot be negative.");
        }
    }

    private void ValidateUndercutRisk(List<string> errors)
    {
        if (UndercutRisk is null)
        {
            return;
        }

        if (UndercutRisk.FaceCount < 0)
        {
            errors.Add("UndercutRisk FaceCount cannot be negative.");
        }

        if (UndercutRisk.RiskySurfaceAreaMm2 < 0m)
        {
            errors.Add("UndercutRisk RiskySurfaceAreaMm2 cannot be negative.");
        }

        if (UndercutRisk.MaximumTrapDepthMm < 0m)
        {
            errors.Add("UndercutRisk MaximumTrapDepthMm cannot be negative.");
        }
    }

    private void ValidateCoolingChannels(List<string> errors)
    {
        if (CoolingChannels is null)
        {
            return;
        }

        if (CoolingChannels.ChannelCount < 0)
        {
            errors.Add("CoolingChannels ChannelCount cannot be negative.");
        }

        if (CoolingChannels.TotalEstimatedRemovedVolumeMm3 < 0m)
        {
            errors.Add("CoolingChannels TotalEstimatedRemovedVolumeMm3 cannot be negative.");
        }
    }

    private void ValidateGateRunnerSprue(List<string> errors)
    {
        if (GateRunnerSprue is null)
        {
            return;
        }

        if (GateRunnerSprue.SegmentCount < 0)
        {
            errors.Add("GateRunnerSprue SegmentCount cannot be negative.");
        }

        if (GateRunnerSprue.TotalFlowLengthMm < 0m ||
            GateRunnerSprue.TotalEstimatedVolumeMm3 < 0m)
        {
            errors.Add("GateRunnerSprue flow length and volume cannot be negative.");
        }
    }

    private void ValidateEjectorCandidates(List<string> errors)
    {
        if (EjectorCandidates is null)
        {
            return;
        }

        if (EjectorCandidates.CandidateCount < 0)
        {
            errors.Add("EjectorCandidates CandidateCount cannot be negative.");
        }

        if (EjectorCandidates.TotalAcceptedPinAreaMm2 < 0m)
        {
            errors.Add("EjectorCandidates TotalAcceptedPinAreaMm2 cannot be negative.");
        }
    }

    private void ValidateClearanceMatrix(List<string> errors)
    {
        if (ClearanceMatrix is null)
        {
            return;
        }

        if (ClearanceMatrix.FeatureCount < 0 ||
            ClearanceMatrix.PairCount < 0)
        {
            errors.Add("ClearanceMatrix FeatureCount and PairCount cannot be negative.");
        }
    }
}