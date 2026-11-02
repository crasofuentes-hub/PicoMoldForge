namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>
/// Summary of a wall-thickness analysis. <paramref name="SampledSurfaceAreaMm2"/> is the surface area of all
/// samples (0 in summaries written before it existed); when it is positive, readiness is judged by the share of
/// risky area instead of the share of risky samples.
/// </summary>
public sealed record VoxelWallThicknessAnalysisSummary(
    int SampleCount,
    int NominalCount,
    int ThinCount,
    int ThickCount,
    int AbruptChangeCount,
    int InvalidCount,
    decimal MinimumObservedThicknessMm,
    decimal MaximumObservedThicknessMm,
    decimal RiskySurfaceAreaMm2,
    decimal SampledSurfaceAreaMm2 = 0m);