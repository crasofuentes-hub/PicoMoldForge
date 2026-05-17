namespace PicoMoldForge.Core.Engineering.Clearance;

public sealed record ClearancePairResult(
    string FeatureAId,
    ClearanceFeatureKind FeatureAKind,
    string FeatureBId,
    ClearanceFeatureKind FeatureBKind,
    decimal CenterlineDistanceMm,
    decimal SurfaceClearanceMm,
    decimal RequiredClearanceMm,
    decimal ClearanceMarginMm,
    bool HasCollisionRisk,
    bool IsCriticalPair);