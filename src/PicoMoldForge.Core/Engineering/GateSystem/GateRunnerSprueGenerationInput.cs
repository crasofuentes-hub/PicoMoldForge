namespace PicoMoldForge.Core.Engineering.GateSystem;

public sealed record GateRunnerSprueGenerationInput(
    GateSystemBounds MoldBounds,
    IReadOnlyList<GateRunnerSprueSegment> Segments,
    decimal RequiredCavityClearanceMm,
    decimal RequiredMoldEdgeClearanceMm,
    bool HasEngineerOverride = false);