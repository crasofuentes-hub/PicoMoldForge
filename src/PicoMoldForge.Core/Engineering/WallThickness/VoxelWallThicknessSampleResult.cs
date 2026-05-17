namespace PicoMoldForge.Core.Engineering.WallThickness;

public sealed record VoxelWallThicknessSampleResult(
    string RegionId,
    VoxelWallThicknessClassification Classification,
    decimal ThicknessMm,
    decimal NominalThicknessMm,
    decimal ThicknessDeltaRatio,
    decimal SurfaceAreaMm2,
    bool IsCosmeticCritical,
    bool IsCriticalToQuality);