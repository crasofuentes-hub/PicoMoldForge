using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.WallFeatures;

public sealed class WallFeatureRuleEngine
{
    public const string RulePackVersion = "expert-injection-mold-rules.v1";

    public EngineeringRuleResult Evaluate(WallFeatureRuleInput input)
    {
        var range = ResolveRange(input.Material, input.CheckType);
        var issue = EvaluateIssue(input, range);

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "WallFeature",
            Issues: new[] { issue });
    }

    public WallFeatureRuleRange ResolveRange(
        WallFeatureMaterial material,
        WallFeatureCheckType checkType)
    {
        return checkType switch
        {
            WallFeatureCheckType.NominalWallThickness => ResolveNominalWallRange(material),

            WallFeatureCheckType.RibThicknessRatio => new WallFeatureRuleRange(
                MinimumValue: 0.40m,
                RecommendedMinimumValue: 0.40m,
                RecommendedMaximumValue: 0.60m,
                WarningMaximumValue: 0.60m,
                FailMaximumValue: 0.70m,
                Unit: "ratio",
                Notes: "Rib thickness should typically be 40% to 60% of parent wall. Over 70% is high sink risk on cosmetic areas."),

            WallFeatureCheckType.RibHeightRatio => new WallFeatureRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 3.0m,
                WarningMaximumValue: 3.0m,
                FailMaximumValue: 4.0m,
                Unit: "ratio",
                Notes: "Rib height should typically be about 3x wall thickness. Over 4x requires draft/radius review."),

            WallFeatureCheckType.BossWallThicknessRatio => new WallFeatureRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0.40m,
                RecommendedMaximumValue: 0.60m,
                WarningMaximumValue: 0.60m,
                FailMaximumValue: 0.75m,
                Unit: "ratio",
                Notes: "Boss wall thickness should be about 60% of nominal wall to reduce sink and void risk."),

            WallFeatureCheckType.AbruptThicknessJumpRatio => new WallFeatureRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.30m,
                WarningMaximumValue: 0.30m,
                FailMaximumValue: 0.50m,
                Unit: "ratio",
                Notes: "Local thickness jumps above 30% should warn; above 50% is high sink/warp risk."),

            WallFeatureCheckType.InternalRadiusRatio => new WallFeatureRuleRange(
                MinimumValue: 0.50m,
                RecommendedMinimumValue: 0.50m,
                RecommendedMaximumValue: 1.00m,
                WarningMaximumValue: 1.00m,
                FailMaximumValue: 0.25m,
                Unit: "ratio",
                Notes: "Internal radius should generally be at least 50% of wall thickness."),

            _ => throw new ArgumentOutOfRangeException(nameof(checkType), checkType, "Unsupported wall feature check type.")
        };
    }

    private static WallFeatureRuleRange ResolveNominalWallRange(WallFeatureMaterial material)
    {
        return material switch
        {
            WallFeatureMaterial.Abs => new WallFeatureRuleRange(
                MinimumValue: 2.0m,
                RecommendedMinimumValue: 2.0m,
                RecommendedMaximumValue: 3.0m,
                WarningMaximumValue: 3.0m,
                FailMaximumValue: 5.0m,
                Unit: "mm",
                Notes: "ABS nominal wall is commonly 2.0 mm to 3.0 mm."),

            WallFeatureMaterial.Pc => new WallFeatureRuleRange(
                MinimumValue: 2.0m,
                RecommendedMinimumValue: 2.0m,
                RecommendedMaximumValue: 3.0m,
                WarningMaximumValue: 3.0m,
                FailMaximumValue: 5.0m,
                Unit: "mm",
                Notes: "PC nominal wall is commonly 2.0 mm to 3.0 mm."),

            WallFeatureMaterial.Pp => new WallFeatureRuleRange(
                MinimumValue: 1.0m,
                RecommendedMinimumValue: 1.0m,
                RecommendedMaximumValue: 2.5m,
                WarningMaximumValue: 2.5m,
                FailMaximumValue: 4.0m,
                Unit: "mm",
                Notes: "PP nominal wall is commonly 1.0 mm to 2.5 mm."),

            WallFeatureMaterial.NylonPa => new WallFeatureRuleRange(
                MinimumValue: 1.0m,
                RecommendedMinimumValue: 1.0m,
                RecommendedMaximumValue: 2.5m,
                WarningMaximumValue: 2.5m,
                FailMaximumValue: 4.0m,
                Unit: "mm",
                Notes: "PA/Nylon nominal wall is commonly 1.0 mm to 2.5 mm."),

            _ => new WallFeatureRuleRange(
                MinimumValue: 1.0m,
                RecommendedMinimumValue: 1.5m,
                RecommendedMaximumValue: 3.0m,
                WarningMaximumValue: 3.0m,
                FailMaximumValue: 5.0m,
                Unit: "mm",
                Notes: "General preliminary wall range; use material-specific rules when available.")
        };
    }

    private static EngineeringIssue EvaluateIssue(
        WallFeatureRuleInput input,
        WallFeatureRuleRange range)
    {
        var ruleId = BuildRuleId(input);
        var material = input.Material.ToString();
        var featureType = input.CheckType.ToString();

        if (input.HasEngineerOverride)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.override",
                category: "WallFeature",
                message: "Wall/feature rule has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved wall/feature override rationale.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit);
        }

        if (input.ActualValue < 0m)
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.negative",
                category: "WallFeature",
                message: "Wall/feature value cannot be negative.",
                correctiveAction: "Provide a non-negative measured or configured value.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit);
        }

        if (input.CheckType == WallFeatureCheckType.InternalRadiusRatio)
        {
            return EvaluateInternalRadius(input, range, ruleId, material, featureType);
        }

        if (input.ActualValue < range.MinimumValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.below-minimum",
                category: "WallFeature",
                message: "Wall/feature value is below the expert preliminary minimum.",
                correctiveAction: "Increase the value or request engineer review for a material/process-specific exception.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality);
        }

        if (input.ActualValue > range.FailMaximumValue &&
            (input.IsCosmeticCritical || input.IsCriticalToQuality))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.above-fail-maximum",
                category: "WallFeature",
                message: "Wall/feature value exceeds the expert fail threshold for a cosmetic or critical condition.",
                correctiveAction: "Reduce the value, core out the feature, or redesign transitions before tooling.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualValue,
                requiredValue: range.FailMaximumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit);
        }

        if (input.ActualValue > range.WarningMaximumValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.above-recommended",
                category: "WallFeature",
                message: "Wall/feature value exceeds the expert recommended range.",
                correctiveAction: "Review sink, warp, cooling time, and transition risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualValue,
                requiredValue: range.WarningMaximumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCosmeticCritical || input.IsCriticalToQuality);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "WallFeature",
            message: "Wall/feature value satisfies the expert preliminary rule.",
            correctiveAction: "No action required beyond normal engineering review.",
            sourceRulePackVersion: RulePackVersion,
            featureType: featureType,
            material: material,
            actualValue: input.ActualValue,
            requiredValue: range.MinimumValue,
            recommendedValue: range.RecommendedMaximumValue,
            unit: range.Unit);
    }

    private static EngineeringIssue EvaluateInternalRadius(
        WallFeatureRuleInput input,
        WallFeatureRuleRange range,
        string ruleId,
        string material,
        string featureType)
    {
        if (input.ActualValue < 0.25m &&
            input.IsCriticalToQuality)
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.sharp-critical",
                category: "WallFeature",
                message: "Internal radius ratio is too low for a critical feature.",
                correctiveAction: "Increase internal radius to at least 50% of wall thickness or document a qualified exception.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit);
        }

        if (input.ActualValue < range.MinimumValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.below-radius-rule",
                category: "WallFeature",
                message: "Internal radius ratio is below the expert recommended minimum.",
                correctiveAction: "Increase internal radius to at least 50% of wall thickness where feasible.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCosmeticCritical || input.IsCriticalToQuality);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "WallFeature",
            message: "Internal radius ratio satisfies the expert preliminary rule.",
            correctiveAction: "No action required beyond normal engineering review.",
            sourceRulePackVersion: RulePackVersion,
            featureType: featureType,
            material: material,
            actualValue: input.ActualValue,
            requiredValue: range.MinimumValue,
            recommendedValue: range.RecommendedMinimumValue,
            unit: range.Unit);
    }

    private static string BuildRuleId(WallFeatureRuleInput input)
    {
        return $"wallfeature.{input.Material}.{input.CheckType}".ToLowerInvariant();
    }
}