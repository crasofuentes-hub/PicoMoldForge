namespace PicoMoldForge.Core.CavityCore;

public sealed record CavityCoreGenerationResult(
    bool IsSuccessful,
    decimal ShrinkageScaleFactor,
    IReadOnlyList<CavityCoreArtifact> Artifacts,
    IReadOnlyList<string> Warnings);