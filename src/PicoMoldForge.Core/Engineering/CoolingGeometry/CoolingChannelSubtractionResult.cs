using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.CoolingGeometry;

public sealed record CoolingChannelSubtractionResult(
    IReadOnlyList<CoolingChannelSubtractionChannelResult> Channels,
    CoolingChannelSubtractionSummary Summary,
    EngineeringRuleResult RuleResult);