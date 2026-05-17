using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.EjectionGeometry;

public sealed record EjectorCandidateGenerationResult(
    IReadOnlyList<EjectorCandidateResult> Candidates,
    EjectorCandidateGenerationSummary Summary,
    EngineeringRuleResult RuleResult);