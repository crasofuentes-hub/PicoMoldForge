namespace PicoMoldForge.Core.Engineering.DraftAnalysis;

public sealed record DraftFaceAnalysisResult(
    string FaceId,
    DraftFaceClassification Classification,
    decimal DraftAngleDeg,
    decimal SurfaceAreaMm2,
    bool IsCosmeticCritical,
    bool IsCriticalToQuality);