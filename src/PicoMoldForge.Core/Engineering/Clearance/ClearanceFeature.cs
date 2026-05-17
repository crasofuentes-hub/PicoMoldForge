namespace PicoMoldForge.Core.Engineering.Clearance;

public sealed record ClearanceFeature(
    string FeatureId,
    ClearanceFeatureKind Kind,
    ClearancePoint Start,
    ClearancePoint End,
    decimal RadiusMm,
    decimal RequiredClearanceMm,
    bool IsCriticalToQuality = false,
    bool IsCosmeticCritical = false);