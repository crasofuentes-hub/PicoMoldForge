namespace PicoMoldForge.Core.DfAM;

public sealed record DfAMReport(
    bool IsSuccessful,
    IReadOnlyList<DfAMCheckResult> Checks,
    IReadOnlyList<string> Warnings);