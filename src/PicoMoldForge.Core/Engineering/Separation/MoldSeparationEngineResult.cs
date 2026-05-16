using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record MoldSeparationEngineResult(
    PartingAxis PartingAxis,
    decimal PartingOffsetMm,
    long CoreVoxelCount,
    long CavityVoxelCount,
    long OverlapVoxelCount,
    long GapVoxelCount,
    decimal CoreApproxVolumeMm3,
    decimal CavityApproxVolumeMm3,
    CoreCavitySeparationSummary Summary,
    EngineeringRuleResult ValidationResult);