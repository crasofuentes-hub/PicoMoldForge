namespace PicoMoldForge.Core.Engineering.Venting;

public sealed record VentingRuleInput(
    VentingCheckType CheckType,
    decimal ActualValue,
    bool IsFlashSensitive = false,
    bool IsLongFlowPath = false,
    bool IsCriticalToQuality = false,
    bool HasEngineerOverride = false);