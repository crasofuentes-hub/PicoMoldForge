namespace PicoMoldForge.Core.Engineering.DraftAnalysis;

/// <summary>
/// Draft classification of the sampled faces. <paramref name="SampledSurfaceAreaMm2"/> is the total area of the
/// sampled faces; 0 when unknown (summaries built before it existed).
/// </summary>
public sealed record DraftBasicGeometryAnalysisSummary(
    int FaceCount,
    int PositiveDraftCount,
    int LowDraftCount,
    int ZeroDraftCount,
    int NegativeDraftCount,
    int InvalidNormalCount,
    decimal RiskySurfaceAreaMm2,
    decimal MinimumObservedDraftDeg,
    decimal SampledSurfaceAreaMm2 = 0m);