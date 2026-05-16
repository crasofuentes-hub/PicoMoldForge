namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record CoreCavitySeparationInput(
    PartingAxis PartingAxis,
    decimal PartingOffsetMm,
    long CoreVoxelCount,
    long CavityVoxelCount,
    long OverlapVoxelCount,
    long GapVoxelCount,
    bool HasCoreSideArtifact,
    bool HasCavitySideArtifact,
    bool HasPartingMetadata,
    bool HasShutoffStrategy,
    bool HasEngineerOverride = false);