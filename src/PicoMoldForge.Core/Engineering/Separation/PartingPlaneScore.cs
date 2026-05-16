using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record PartingPlaneScore(
    PartingPlaneCandidate Candidate,
    decimal QualityScore,
    decimal BalanceRatio,
    decimal NormalizedPosition,
    bool IsInsideMoldBounds,
    bool IsInsidePartBounds,
    EngineeringSeverity Severity,
    IReadOnlyList<string> Reasons);