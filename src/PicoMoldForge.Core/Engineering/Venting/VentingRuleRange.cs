namespace PicoMoldForge.Core.Engineering.Venting;

public sealed record VentingRuleRange(
    decimal MinimumValue,
    decimal RecommendedMinimumValue,
    decimal RecommendedMaximumValue,
    decimal WarningMaximumValue,
    decimal FailMaximumValue,
    decimal? FailBelowValue,
    string Unit,
    string Notes);