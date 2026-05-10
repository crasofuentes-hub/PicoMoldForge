namespace PicoMoldForge.Core.MoldSystems;

public sealed record MoldSystemPlan(
    bool IsSuccessful,
    MoldBaseEnvelope MoldBase,
    EjectorPinPlan Ejectors,
    VentPlan Vents,
    InsertPlan Inserts,
    IReadOnlyList<string> Warnings);