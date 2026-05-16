namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record PartingPlaneScoringInput(
    MoldSeparationBounds MoldBlockBounds,
    MoldSeparationBounds PartBounds,
    decimal VoxelResolutionMm,
    IReadOnlyList<PartingPlaneCandidate> Candidates,
    bool HasShutoffStrategy = true);