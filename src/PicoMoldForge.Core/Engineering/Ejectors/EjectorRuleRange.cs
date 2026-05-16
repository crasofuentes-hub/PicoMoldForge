namespace PicoMoldForge.Core.Engineering.Ejectors;

public sealed record EjectorRuleRange(
    decimal MinimumValue,
    decimal RecommendedMinimumValue,
    decimal RecommendedMaximumValue,
    decimal WarningMaximumValue,
    decimal FailMaximumValue,
    decimal? FailBelowValue,
    string Unit,
    string Notes);