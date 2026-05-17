using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.DraftAnalysis;

public sealed record DraftBasicGeometryAnalysisResult(
    IReadOnlyList<DraftFaceAnalysisResult> Faces,
    DraftBasicGeometryAnalysisSummary Summary,
    EngineeringRuleResult RuleResult);