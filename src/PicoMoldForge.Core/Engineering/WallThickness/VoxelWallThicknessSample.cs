namespace PicoMoldForge.Core.Engineering.WallThickness;

public sealed record VoxelWallThicknessSample(
    string RegionId,
    decimal ThicknessMm,
    decimal NominalThicknessMm,
    decimal SurfaceAreaMm2,
    bool IsCosmeticCritical = false,
    bool IsCriticalToQuality = false);