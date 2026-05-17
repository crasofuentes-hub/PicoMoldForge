namespace PicoMoldForge.Core.Engineering.CoolingGeometry;

public sealed record CoolingChannelSubtractionChannelResult(
    string ChannelId,
    decimal LengthMm,
    decimal DiameterMm,
    decimal EstimatedRemovedVolumeMm3,
    bool IsInsideMoldBounds,
    bool HasRequiredCavityClearance,
    bool HasRequiredMoldEdgeClearance,
    bool IsSubtractable);