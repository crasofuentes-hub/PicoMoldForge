namespace PicoMoldForge.Core.Engineering.CoolingGeometry;

public sealed record CoolingChannelSubtractionSummary(
    int ChannelCount,
    int SubtractableChannelCount,
    int BlockedChannelCount,
    decimal TotalEstimatedRemovedVolumeMm3);