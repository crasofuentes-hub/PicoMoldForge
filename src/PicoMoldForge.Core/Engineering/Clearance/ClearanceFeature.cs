namespace PicoMoldForge.Core.Engineering.Clearance;

/// <summary>
/// A straight feature (centreline from <paramref name="Start"/> to <paramref name="End"/>, radius
/// <paramref name="RadiusMm"/>). Features with the same non-empty <paramref name="GroupId"/> are parts of one
/// connected feature (the sprue, runner and gate of one feed path): they touch by design and are not compared with
/// each other.
/// </summary>
public sealed record ClearanceFeature(
    string FeatureId,
    ClearanceFeatureKind Kind,
    ClearancePoint Start,
    ClearancePoint End,
    decimal RadiusMm,
    decimal RequiredClearanceMm,
    bool IsCriticalToQuality = false,
    bool IsCosmeticCritical = false,
    string? GroupId = null);