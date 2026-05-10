namespace PicoMoldForge.Core.Analysis;

public sealed record PartAnalysisWarning(
    string Code,
    string Severity,
    string Message);