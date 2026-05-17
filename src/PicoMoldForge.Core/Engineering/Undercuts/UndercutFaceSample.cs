namespace PicoMoldForge.Core.Engineering.Undercuts;

public sealed record UndercutFaceSample(
    string FaceId,
    decimal NormalX,
    decimal NormalY,
    decimal NormalZ,
    decimal SurfaceAreaMm2,
    decimal TrapDepthMm = 0m,
    bool IsCriticalToQuality = false,
    bool IsCosmeticCritical = false);