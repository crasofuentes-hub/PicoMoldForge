namespace PicoMoldForge.Core.CavityCore;

public sealed record CavityCoreArtifact(
    CavityCoreArtifactKind Kind,
    string Path,
    string Description);