namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record ShutoffStrategySummary(
    int RegionCount,
    int UndefinedRegionCount,
    int CriticalRegionCount,
    decimal MaximumGapMm,
    decimal MaximumOverlapMm,
    decimal QualityScore);