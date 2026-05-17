namespace PicoMoldForge.Core.Engineering.WallThickness;

public sealed record VoxelWallThicknessAnalysisSummary(
    int SampleCount,
    int NominalCount,
    int ThinCount,
    int ThickCount,
    int AbruptChangeCount,
    int InvalidCount,
    decimal MinimumObservedThicknessMm,
    decimal MaximumObservedThicknessMm,
    decimal RiskySurfaceAreaMm2);