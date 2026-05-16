namespace PicoMoldForge.Core.Engineering.Cooling;

public sealed record CoolingRuleRange(
    decimal MinimumValue,
    decimal RecommendedMinimumValue,
    decimal RecommendedMaximumValue,
    decimal WarningMaximumValue,
    decimal FailMaximumValue,
    string Unit,
    string Notes);