namespace PicoMoldForge.Core.MoldSystems;

public sealed record EjectorPinPlan(
    bool IsSuccessful,
    IReadOnlyList<EjectorPin> Pins,
    IReadOnlyList<string> Warnings);