namespace PicoMoldForge.Core.Engineering.DraftAnalysis;

public sealed record DraftFaceSample(
    string FaceId,
    decimal NormalX,
    decimal NormalY,
    decimal NormalZ,
    decimal SurfaceAreaMm2,
    bool IsCosmeticCritical = false,
    bool IsCriticalToQuality = false);