namespace PicoMoldForge.Core.MoldSystems;

public sealed record InsertPlan(
    bool IsSuccessful,
    IReadOnlyList<InsertPocket> Pockets,
    IReadOnlyList<string> Warnings);