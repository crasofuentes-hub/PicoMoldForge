namespace PicoMoldForge.Core.Engineering.Undercuts;

public sealed record UndercutRiskAnalysisSummary(
    int FaceCount,
    int ClearPullCount,
    int LowPullClearanceCount,
    int SideActionCandidateCount,
    int UndercutCount,
    int InvalidNormalCount,
    decimal RiskySurfaceAreaMm2,
    decimal MaximumTrapDepthMm);