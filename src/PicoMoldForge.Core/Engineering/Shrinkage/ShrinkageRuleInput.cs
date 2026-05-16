namespace PicoMoldForge.Core.Engineering.Shrinkage;

public sealed record ShrinkageRuleInput(
    ShrinkageMaterial Material,
    decimal? ActualShrinkageRate,
    bool IsCriticalDimension,
    bool UsesDatasheetValue,
    bool HasEngineerOverride = false);