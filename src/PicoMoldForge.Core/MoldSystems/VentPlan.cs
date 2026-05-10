namespace PicoMoldForge.Core.MoldSystems;

public sealed record VentPlan(
    bool IsSuccessful,
    IReadOnlyList<VentChannel> Channels,
    IReadOnlyList<string> Warnings);