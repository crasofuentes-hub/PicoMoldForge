namespace PicoMoldForge.Core.Engineering.SteelSafe;

public sealed record SteelSafeRuleInput(
    SteelSafeCheckType CheckType,
    decimal ActualValue,
    bool IsCriticalToQuality = false,
    bool IsCosmeticSurface = false,
    bool HasEngineerOverride = false);