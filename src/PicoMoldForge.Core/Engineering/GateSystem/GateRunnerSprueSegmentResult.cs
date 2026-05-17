namespace PicoMoldForge.Core.Engineering.GateSystem;

public sealed record GateRunnerSprueSegmentResult(
    string FeatureId,
    GateSystemFeatureType FeatureType,
    decimal LengthMm,
    decimal HydraulicDiameterMm,
    decimal FlowAreaMm2,
    decimal EstimatedVolumeMm3,
    bool IsInsideMoldBounds,
    bool HasRequiredCavityClearance,
    bool HasRequiredMoldEdgeClearance,
    bool IsGeneratable);