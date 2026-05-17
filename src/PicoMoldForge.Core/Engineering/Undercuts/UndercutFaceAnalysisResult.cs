namespace PicoMoldForge.Core.Engineering.Undercuts;

public sealed record UndercutFaceAnalysisResult(
    string FaceId,
    UndercutFaceClassification Classification,
    decimal PullDot,
    decimal SurfaceAreaMm2,
    decimal TrapDepthMm,
    bool IsCriticalToQuality,
    bool IsCosmeticCritical);