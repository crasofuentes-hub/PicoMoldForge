namespace PicoMoldForge.Core.Engineering.EjectionGeometry;

public sealed record EjectorCandidateResult(
    string CandidateId,
    decimal PinDiameterMm,
    decimal StrokeMm,
    decimal SupportedSurfaceAreaMm2,
    bool IsInsideMoldBounds,
    bool HasRequiredCoolingClearance,
    bool HasRequiredGateSystemClearance,
    bool HasRequiredMoldEdgeClearance,
    bool HasRequiredSurfaceSupport,
    bool IsCosmeticSurface,
    bool IsCriticalToQuality,
    bool IsAccepted);