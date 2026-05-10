namespace PicoMoldForge.Core.MoldSystems;

public sealed record InsertPocket(
    string Id,
    decimal MinXmm,
    decimal MinYmm,
    decimal MinZmm,
    decimal MaxXmm,
    decimal MaxYmm,
    decimal MaxZmm,
    decimal ClearanceMm);