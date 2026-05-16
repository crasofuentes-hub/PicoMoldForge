namespace PicoMoldForge.Core.Engineering.Cooling;

public sealed record CoolingRuleInput(
    CoolingCheckType CheckType,
    decimal ActualValue,
    bool IsCosmeticCritical = false,
    bool IsCriticalToQuality = false,
    bool HasEngineerOverride = false);