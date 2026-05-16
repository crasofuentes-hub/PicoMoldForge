namespace PicoMoldForge.Core.Engineering.WallFeatures;

public sealed record WallFeatureRuleRange(
    decimal MinimumValue,
    decimal RecommendedMinimumValue,
    decimal RecommendedMaximumValue,
    decimal WarningMaximumValue,
    decimal FailMaximumValue,
    string Unit,
    string Notes);