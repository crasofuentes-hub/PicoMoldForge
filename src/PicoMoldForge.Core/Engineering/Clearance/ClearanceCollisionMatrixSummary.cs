namespace PicoMoldForge.Core.Engineering.Clearance;

public sealed record ClearanceCollisionMatrixSummary(
    int FeatureCount,
    int PairCount,
    int CollisionRiskPairCount,
    int CriticalRiskPairCount,
    decimal MinimumSurfaceClearanceMm,
    decimal MinimumClearanceMarginMm);