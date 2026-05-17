namespace PicoMoldForge.Core.Engineering.EjectionGeometry;

public sealed record EjectorCandidateGenerationInput(
    EjectorLayoutBounds MoldBounds,
    IReadOnlyList<EjectorCandidate> Candidates,
    decimal RequiredCoolingClearanceMm,
    decimal RequiredGateSystemClearanceMm,
    decimal RequiredMoldEdgeClearanceMm,
    decimal MinimumSupportedSurfaceAreaMm2,
    bool HasGlobalEngineerOverride = false);