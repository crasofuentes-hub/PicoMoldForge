namespace PicoMoldForge.Core.Engineering.EjectionGeometry;

public sealed record EjectorCandidate(
    string CandidateId,
    EjectorCandidatePoint Location,
    decimal PinDiameterMm,
    decimal StrokeMm,
    decimal SupportedSurfaceAreaMm2,
    decimal MinimumCoolingClearanceMm,
    decimal MinimumGateSystemClearanceMm,
    decimal MinimumMoldEdgeClearanceMm,
    bool IsCosmeticSurface = false,
    bool IsCriticalToQuality = false,
    bool HasEngineerOverride = false);