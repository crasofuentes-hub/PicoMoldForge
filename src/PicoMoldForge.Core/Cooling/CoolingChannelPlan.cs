namespace PicoMoldForge.Core.Cooling;

public sealed record CoolingChannelPlan(
    bool IsSuccessful,
    IReadOnlyList<CoolingChannelSegment> Segments,
    IReadOnlyList<string> Warnings);