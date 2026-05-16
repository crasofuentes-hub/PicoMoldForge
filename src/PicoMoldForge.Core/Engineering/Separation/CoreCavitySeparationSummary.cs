namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record CoreCavitySeparationSummary(
    long TotalHalfVoxelCount,
    decimal OverlapRatio,
    decimal GapRatio,
    decimal BalanceRatio,
    decimal QualityScore);