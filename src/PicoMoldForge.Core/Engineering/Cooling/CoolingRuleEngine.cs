using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Cooling;

public sealed class CoolingRuleEngine
{
    public const string RulePackVersion = "expert-injection-mold-rules.v1";

    public EngineeringRuleResult Evaluate(CoolingRuleInput input)
    {
        var range = ResolveRange(input.CheckType);
        var issue = EvaluateIssue(input, range);

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "Cooling",
            Issues: new[] { issue });
    }

    public CoolingRuleRange ResolveRange(CoolingCheckType checkType)
    {
        return checkType switch
        {
            CoolingCheckType.ChannelDistanceToCavityDiameterRatio => new CoolingRuleRange(
                MinimumValue: 1.0m,
                RecommendedMinimumValue: 1.0m,
                RecommendedMaximumValue: 1.5m,
                WarningMaximumValue: 1.5m,
                FailMaximumValue: 2.5m,
                Unit: "ratio",
                Notes: "Cooling channel distance from cavity surface should be about 1.0x to 1.5x channel diameter."),

            CoolingCheckType.LocalThicknessJumpRatio => new CoolingRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.30m,
                WarningMaximumValue: 0.30m,
                FailMaximumValue: 0.50m,
                Unit: "ratio",
                Notes: "Local thickness jumps above 30% should warn; above 50% is high sink, warp, and cooling imbalance risk."),

            CoolingCheckType.CoolingBalanceDeltaRatio => new CoolingRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.15m,
                WarningMaximumValue: 0.15m,
                FailMaximumValue: 0.30m,
                Unit: "ratio",
                Notes: "Cooling should be balanced across circuits or cavities; large relative deltas increase warp mismatch risk."),

            CoolingCheckType.RelativeCoolingTimeRatio => new CoolingRuleRange(
                MinimumValue: 1.0m,
                RecommendedMinimumValue: 1.0m,
                RecommendedMaximumValue: 2.25m,
                WarningMaximumValue: 2.25m,
                FailMaximumValue: 4.0m,
                Unit: "ratio",
                Notes: "Cooling time scales approximately with wall thickness squared. Doubling wall thickness can increase cooling time about 4x."),

            _ => throw new ArgumentOutOfRangeException(nameof(checkType), checkType, "Unsupported cooling check type.")
        };
    }

    public decimal EstimateRelativeCoolingTimeRatio(decimal localWallThicknessMm, decimal nominalWallThicknessMm)
    {
        if (localWallThicknessMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(localWallThicknessMm), "Local wall thickness must be greater than zero.");
        }

        if (nominalWallThicknessMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalWallThicknessMm), "Nominal wall thickness must be greater than zero.");
        }

        var thicknessRatio = localWallThicknessMm / nominalWallThicknessMm;

        return thicknessRatio * thicknessRatio;
    }

    private static EngineeringIssue EvaluateIssue(
        CoolingRuleInput input,
        CoolingRuleRange range)
    {
        var ruleId = BuildRuleId(input.CheckType);
        var featureType = input.CheckType.ToString();

        if (input.HasEngineerOverride)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.override",
                category: "Cooling",
                message: "Cooling rule has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved cooling override rationale.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit);
        }

        if (input.ActualValue < 0m)
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.negative",
                category: "Cooling",
                message: "Cooling validation value cannot be negative.",
                correctiveAction: "Provide a non-negative measured or configured cooling value.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit);
        }

        if (input.ActualValue < range.MinimumValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.below-minimum",
                category: "Cooling",
                message: "Cooling value is below the expert preliminary minimum.",
                correctiveAction: "Increase clearance or request engineer review for the cooling layout.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsCosmeticCritical);
        }

        if (input.ActualValue > range.FailMaximumValue &&
            (input.IsCriticalToQuality || input.IsCosmeticCritical))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.above-fail-maximum",
                category: "Cooling",
                message: "Cooling value exceeds the expert fail threshold for a cosmetic or critical condition.",
                correctiveAction: "Redesign cooling, reduce local thickness variation, or request qualified mold engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.FailMaximumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit);
        }

        if (input.ActualValue > range.WarningMaximumValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.above-recommended",
                category: "Cooling",
                message: "Cooling value exceeds the expert recommended range.",
                correctiveAction: "Review hot spots, cooling balance, warp risk, and cycle-time impact.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.WarningMaximumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsCosmeticCritical);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "Cooling",
            message: "Cooling value satisfies the expert preliminary rule.",
            correctiveAction: "No action required beyond normal engineering review.",
            sourceRulePackVersion: RulePackVersion,
            featureType: featureType,
            material: null,
            actualValue: input.ActualValue,
            requiredValue: range.MinimumValue,
            recommendedValue: range.RecommendedMaximumValue,
            unit: range.Unit);
    }

    private static string BuildRuleId(CoolingCheckType checkType)
    {
        return $"cooling.{checkType}".ToLowerInvariant();
    }
}