namespace PicoMoldForge.Core.Engineering.CoolingGeometry;

public sealed record CoolingChannelSubtractionInput(
    CoolingMoldBounds MoldBounds,
    IReadOnlyList<CoolingChannelSegment> Channels,
    decimal RequiredCavityClearanceMm,
    decimal RequiredMoldEdgeClearanceMm,
    bool HasEngineerOverride = false);