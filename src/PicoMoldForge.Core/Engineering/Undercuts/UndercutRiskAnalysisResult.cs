using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Undercuts;

public sealed record UndercutRiskAnalysisResult(
    IReadOnlyList<UndercutFaceAnalysisResult> Faces,
    UndercutRiskAnalysisSummary Summary,
    EngineeringRuleResult RuleResult);