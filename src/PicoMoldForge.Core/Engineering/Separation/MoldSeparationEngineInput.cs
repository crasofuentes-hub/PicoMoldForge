namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record MoldSeparationEngineInput(
    MoldSeparationBounds MoldBlockBounds,
    MoldSeparationBounds PartBounds,
    PartingAxis PartingAxis,
    decimal PartingOffsetMm,
    decimal VoxelResolutionMm,
    bool HasPartingMetadata = true,
    bool HasShutoffStrategy = false,
    bool HasEngineerOverride = false);