namespace PicoMoldForge.Core.Engineering.Undercuts;

/// <summary>
/// Undercut classification of the sampled faces. <paramref name="SampledSurfaceAreaMm2"/> is the total area of the
/// sampled faces; 0 when unknown (summaries built before it existed).
/// </summary>
public sealed record UndercutRiskAnalysisSummary(
    int FaceCount,
    int ClearPullCount,
    int LowPullClearanceCount,
    int SideActionCandidateCount,
    int UndercutCount,
    int InvalidNormalCount,
    decimal RiskySurfaceAreaMm2,
    decimal MaximumTrapDepthMm,
    decimal SampledSurfaceAreaMm2 = 0m);