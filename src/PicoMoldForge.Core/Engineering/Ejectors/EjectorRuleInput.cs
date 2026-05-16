namespace PicoMoldForge.Core.Engineering.Ejectors;

public sealed record EjectorRuleInput(
    EjectorCheckType CheckType,
    decimal ActualValue,
    bool IsCosmeticSurface = false,
    bool IsThinWall = false,
    bool IsCriticalToQuality = false,
    bool HasEngineerOverride = false);