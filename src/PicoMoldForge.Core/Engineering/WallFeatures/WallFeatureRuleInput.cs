namespace PicoMoldForge.Core.Engineering.WallFeatures;

public sealed record WallFeatureRuleInput(
    WallFeatureMaterial Material,
    WallFeatureCheckType CheckType,
    decimal ActualValue,
    bool IsCosmeticCritical = false,
    bool IsCriticalToQuality = false,
    bool HasEngineerOverride = false);