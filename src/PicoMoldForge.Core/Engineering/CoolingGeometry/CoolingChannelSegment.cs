namespace PicoMoldForge.Core.Engineering.CoolingGeometry;

public sealed record CoolingChannelSegment(
    string ChannelId,
    CoolingChannelPoint Start,
    CoolingChannelPoint End,
    decimal DiameterMm,
    decimal MinimumCavityClearanceMm,
    decimal MinimumMoldEdgeClearanceMm,
    bool IsCriticalToQuality = false);