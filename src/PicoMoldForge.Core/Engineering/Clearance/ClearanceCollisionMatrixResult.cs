using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Clearance;

public sealed record ClearanceCollisionMatrixResult(
    IReadOnlyList<ClearancePairResult> Pairs,
    ClearanceCollisionMatrixSummary Summary,
    EngineeringRuleResult RuleResult);