namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record PartingPlaneScoringResult(
    PartingPlaneScore BestScore,
    IReadOnlyList<PartingPlaneScore> Scores);