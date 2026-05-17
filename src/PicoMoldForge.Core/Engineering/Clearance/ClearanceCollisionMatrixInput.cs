namespace PicoMoldForge.Core.Engineering.Clearance;

public sealed record ClearanceCollisionMatrixInput(
    IReadOnlyList<ClearanceFeature> Features,
    decimal GlobalMinimumClearanceMm,
    bool HasEngineerOverride = false);