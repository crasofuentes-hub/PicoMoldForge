namespace PicoMoldForge.Core.DfAM;

public sealed record DfAMCheckResult(
    DfAMRule Rule,
    bool IsPassed,
    string Message);