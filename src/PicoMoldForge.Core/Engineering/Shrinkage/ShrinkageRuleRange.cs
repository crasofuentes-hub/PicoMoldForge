namespace PicoMoldForge.Core.Engineering.Shrinkage;

public sealed record ShrinkageRuleRange(
    ShrinkageMaterial Material,
    decimal MinimumRate,
    decimal MaximumRate,
    decimal RecommendedRate,
    string Notes);