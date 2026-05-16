namespace PicoMoldForge.Core.Engineering.SteelSafe;

public sealed record SteelSafeRuleRange(
    decimal MinimumValue,
    decimal RecommendedMinimumValue,
    decimal RecommendedMaximumValue,
    decimal WarningMaximumValue,
    decimal FailMaximumValue,
    decimal? FailBelowValue,
    string Unit,
    string Notes);