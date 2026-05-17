using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.GateSystem;

public sealed record GateRunnerSprueGenerationResult(
    IReadOnlyList<GateRunnerSprueSegmentResult> Segments,
    GateRunnerSprueGenerationSummary Summary,
    EngineeringRuleResult RuleResult);