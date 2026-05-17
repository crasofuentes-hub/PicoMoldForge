namespace PicoMoldForge.Core.Engineering.GateSystem;

public sealed record GateRunnerSprueSegment(
    string FeatureId,
    GateSystemFeatureType FeatureType,
    GateSystemPoint Start,
    GateSystemPoint End,
    decimal HydraulicDiameterMm,
    decimal FlowAreaMm2,
    decimal MinimumCavityClearanceMm,
    decimal MinimumMoldEdgeClearanceMm,
    bool IsCosmeticCritical = false,
    bool IsCriticalToQuality = false);