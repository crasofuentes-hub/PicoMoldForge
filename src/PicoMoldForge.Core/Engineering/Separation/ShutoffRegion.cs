namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record ShutoffRegion(
    string RegionId,
    ShutoffRegionType RegionType,
    ShutoffClosureState ClosureState,
    decimal ContactAreaMm2,
    decimal GapMm,
    decimal OverlapMm,
    bool IsCriticalToQuality = false,
    bool IsCosmeticBoundary = false,
    bool HasEngineerOverride = false);