namespace PicoMoldForge.Core.DfAM;

public sealed record DfAMRule(
    string Code,
    DfAMRuleSeverity Severity,
    string Description);