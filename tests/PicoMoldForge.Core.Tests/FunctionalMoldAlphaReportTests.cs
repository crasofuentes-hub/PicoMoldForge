using PicoMoldForge.Core.Engineering.Clearance;
using PicoMoldForge.Core.Engineering.CoolingGeometry;
using PicoMoldForge.Core.Engineering.DraftAnalysis;
using PicoMoldForge.Core.Engineering.EjectionGeometry;
using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.Undercuts;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Exports;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class FunctionalMoldAlphaReportTests
{
    [Fact]
    public void Validate_WithCompleteAlphaReport_ReturnsNoErrors()
    {
        var report = CreateCompleteReport();

        Assert.Empty(report.Validate());
        Assert.True(report.IsAlphaComplete);
        Assert.Empty(report.MissingMetricGroups());
        Assert.True(report.OverallReadinessScore > 0.80m);
    }

    [Fact]
    public void Validate_WithNoMetricGroups_ReturnsError()
    {
        var report = new FunctionalMoldAlphaReport(
            Separation: null,
            Shutoff: null,
            DraftGeometry: null,
            Warnings: Array.Empty<string>());

        var errors = report.Validate();

        Assert.Contains(errors, error => error.Contains("At least one Functional Mold Alpha metric group", StringComparison.Ordinal));
        Assert.False(report.IsAlphaComplete);
    }

    [Fact]
    public void MissingMetricGroups_WithPartialReport_ReturnsMissingGroups()
    {
        var report = new FunctionalMoldAlphaReport(
            Separation: CreateSeparation(),
            Shutoff: null,
            DraftGeometry: null,
            Warnings: Array.Empty<string>());

        var missing = report.MissingMetricGroups();

        Assert.DoesNotContain("Separation", missing);
        Assert.Contains("Shutoff", missing);
        Assert.Contains("WallThickness", missing);
        Assert.Contains("ClearanceMatrix", missing);
    }

    [Fact]
    public void OverallReadinessScore_WithBlockedSystems_LowersScore()
    {
        var report = CreateCompleteReport() with
        {
            CoolingChannels = new CoolingChannelSubtractionSummary(
                ChannelCount: 4,
                SubtractableChannelCount: 1,
                BlockedChannelCount: 3,
                TotalEstimatedRemovedVolumeMm3: 100m),
            EjectorCandidates = new EjectorCandidateGenerationSummary(
                CandidateCount: 4,
                AcceptedCandidateCount: 1,
                BlockedCandidateCount: 3,
                CosmeticCandidateCount: 1,
                CriticalCandidateCount: 1,
                TotalAcceptedPinAreaMm2: 12m)
        };

        Assert.True(report.OverallReadinessScore < CreateCompleteReport().OverallReadinessScore);
    }

    [Fact]
    public void Validate_WithNegativeWallThicknessRiskArea_ReturnsError()
    {
        var report = CreateCompleteReport() with
        {
            WallThickness = new VoxelWallThicknessAnalysisSummary(
                SampleCount: 1,
                NominalCount: 1,
                ThinCount: 0,
                ThickCount: 0,
                AbruptChangeCount: 0,
                InvalidCount: 0,
                MinimumObservedThicknessMm: 1m,
                MaximumObservedThicknessMm: 2m,
                RiskySurfaceAreaMm2: -1m)
        };

        Assert.Contains(report.Validate(), error => error.Contains("WallThickness RiskySurfaceAreaMm2", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadinessScore_UsesRiskyArea_WhenTheWallSummaryHasSampledArea()
    {
        // Monoblock measured on its STL: 129 regions, 71 of them not nominal, but only 24.6 % of the wall area.
        var measured = new VoxelWallThicknessAnalysisSummary(
            SampleCount: 129, NominalCount: 58, ThinCount: 3, ThickCount: 3, AbruptChangeCount: 65, InvalidCount: 0,
            MinimumObservedThicknessMm: 0.5934m, MaximumObservedThicknessMm: 17.3774m,
            RiskySurfaceAreaMm2: 23790m, SampledSurfaceAreaMm2: 96825m);
        var byCount = measured with { SampledSurfaceAreaMm2 = 0m };

        var areaScore = OnlyWallThickness(measured).OverallReadinessScore;
        var countScore = OnlyWallThickness(byCount).OverallReadinessScore;

        Assert.Equal(Math.Round(1m - (23790m / 96825m), 4), Math.Round(areaScore, 4));
        Assert.Equal(Math.Round(1m - (71m / 129m), 4), Math.Round(countScore, 4));
    }

    private static FunctionalMoldAlphaReport OnlyWallThickness(VoxelWallThicknessAnalysisSummary summary) =>
        new(
            Separation: null,
            Shutoff: null,
            DraftGeometry: null,
            Warnings: Array.Empty<string>(),
            WallThickness: summary);

    private static FunctionalMoldAlphaReport CreateCompleteReport()
    {
        return new FunctionalMoldAlphaReport(
            Separation: CreateSeparation(),
            Shutoff: new ShutoffStrategySummary(
                RegionCount: 2,
                UndefinedRegionCount: 0,
                CriticalRegionCount: 1,
                MaximumGapMm: 0.01m,
                MaximumOverlapMm: 0.01m,
                QualityScore: 0.92m),
            DraftGeometry: new DraftBasicGeometryAnalysisSummary(
                FaceCount: 10,
                PositiveDraftCount: 8,
                LowDraftCount: 1,
                ZeroDraftCount: 1,
                NegativeDraftCount: 0,
                InvalidNormalCount: 0,
                RiskySurfaceAreaMm2: 12m,
                MinimumObservedDraftDeg: 0.5m),
            Warnings: new[] { "Functional Mold Alpha metrics are preliminary." },
            WallThickness: new VoxelWallThicknessAnalysisSummary(
                SampleCount: 10,
                NominalCount: 8,
                ThinCount: 1,
                ThickCount: 1,
                AbruptChangeCount: 0,
                InvalidCount: 0,
                MinimumObservedThicknessMm: 1.0m,
                MaximumObservedThicknessMm: 3.0m,
                RiskySurfaceAreaMm2: 20m),
            UndercutRisk: new UndercutRiskAnalysisSummary(
                FaceCount: 10,
                ClearPullCount: 8,
                LowPullClearanceCount: 1,
                SideActionCandidateCount: 1,
                UndercutCount: 0,
                InvalidNormalCount: 0,
                RiskySurfaceAreaMm2: 15m,
                MaximumTrapDepthMm: 1m),
            CoolingChannels: new CoolingChannelSubtractionSummary(
                ChannelCount: 4,
                SubtractableChannelCount: 4,
                BlockedChannelCount: 0,
                TotalEstimatedRemovedVolumeMm3: 1000m),
            GateRunnerSprue: new GateRunnerSprueGenerationSummary(
                SegmentCount: 3,
                SprueCount: 1,
                RunnerCount: 1,
                GateCount: 1,
                GeneratableSegmentCount: 3,
                BlockedSegmentCount: 0,
                TotalFlowLengthMm: 120m,
                TotalEstimatedVolumeMm3: 500m),
            EjectorCandidates: new EjectorCandidateGenerationSummary(
                CandidateCount: 4,
                AcceptedCandidateCount: 4,
                BlockedCandidateCount: 0,
                CosmeticCandidateCount: 0,
                CriticalCandidateCount: 1,
                TotalAcceptedPinAreaMm2: 50m),
            ClearanceMatrix: new ClearanceCollisionMatrixSummary(
                FeatureCount: 6,
                PairCount: 15,
                CollisionRiskPairCount: 0,
                CriticalRiskPairCount: 0,
                MinimumSurfaceClearanceMm: 5m,
                MinimumClearanceMarginMm: 2m));
    }

    private static CoreCavitySeparationSummary CreateSeparation()
    {
        return new CoreCavitySeparationSummary(
            TotalHalfVoxelCount: 2000,
            OverlapRatio: 0.001m,
            GapRatio: 0.002m,
            BalanceRatio: 0.98m,
            QualityScore: 0.96m);
    }
}