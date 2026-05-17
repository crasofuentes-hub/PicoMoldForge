namespace PicoMoldForge.Core.Engineering.EjectionGeometry;

public sealed record EjectorCandidateGenerationSummary(
    int CandidateCount,
    int AcceptedCandidateCount,
    int BlockedCandidateCount,
    int CosmeticCandidateCount,
    int CriticalCandidateCount,
    decimal TotalAcceptedPinAreaMm2);