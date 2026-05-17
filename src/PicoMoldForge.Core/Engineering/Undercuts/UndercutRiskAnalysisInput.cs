namespace PicoMoldForge.Core.Engineering.Undercuts;

public sealed record UndercutRiskAnalysisInput(
    decimal PullDirectionX,
    decimal PullDirectionY,
    decimal PullDirectionZ,
    decimal LowPullDotThreshold,
    decimal SideActionDotThreshold,
    decimal CriticalTrapDepthMm,
    IReadOnlyList<UndercutFaceSample> Faces);