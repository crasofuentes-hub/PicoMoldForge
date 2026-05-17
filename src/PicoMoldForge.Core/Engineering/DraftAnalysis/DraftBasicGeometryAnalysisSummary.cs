namespace PicoMoldForge.Core.Engineering.DraftAnalysis;

public sealed record DraftBasicGeometryAnalysisSummary(
    int FaceCount,
    int PositiveDraftCount,
    int LowDraftCount,
    int ZeroDraftCount,
    int NegativeDraftCount,
    int InvalidNormalCount,
    decimal RiskySurfaceAreaMm2,
    decimal MinimumObservedDraftDeg);