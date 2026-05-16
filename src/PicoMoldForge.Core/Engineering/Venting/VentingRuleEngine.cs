using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Venting;

public sealed class VentingRuleEngine
{
    public const string RulePackVersion = "expert-injection-mold-rules.v1";

    public EngineeringRuleResult Evaluate(VentingRuleInput input)
    {
        var range = ResolveRange(input.CheckType);
        var issue = EvaluateIssue(input, range);

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "Venting",
            Issues: new[] { issue });
    }

    public VentingRuleRange ResolveRange(VentingCheckType checkType)
    {
        return checkType switch
        {
            VentingCheckType.VentDepthMm => new VentingRuleRange(
                MinimumValue: 0.02m,
                RecommendedMinimumValue: 0.02m,
                RecommendedMaximumValue: 0.05m,
                WarningMaximumValue: 0.05m,
                FailMaximumValue: 0.08m,
                FailBelowValue: 0.01m,
                Unit: "mm",
                Notes: "Vent depth is commonly 0.02 mm to 0.05 mm. Too shallow may trap gas; too deep may flash."),

            VentingCheckType.VentWidthMm => new VentingRuleRange(
                MinimumValue: 0.20m,
                RecommendedMinimumValue: 0.50m,
                RecommendedMaximumValue: 5.00m,
                WarningMaximumValue: 5.00m,
                FailMaximumValue: 10.00m,
                FailBelowValue: 0.10m,
                Unit: "mm",
                Notes: "Vent width is resin/tool dependent. This preliminary range catches obviously too-narrow or excessive vents."),

            VentingCheckType.VentLandLengthMm => new VentingRuleRange(
                MinimumValue: 0.50m,
                RecommendedMinimumValue: 0.80m,
                RecommendedMaximumValue: 2.00m,
                WarningMaximumValue: 2.00m,
                FailMaximumValue: 5.00m,
                FailBelowValue: 0.20m,
                Unit: "mm",
                Notes: "Vent land length should be sufficient to control flash while allowing gas escape."),

            VentingCheckType.EndOfFillVentRiskScore => new VentingRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.30m,
                WarningMaximumValue: 0.30m,
                FailMaximumValue: 0.70m,
                FailBelowValue: null,
                Unit: "score",
                Notes: "Vents should exist at end-of-fill and likely trapped-air areas."),

            VentingCheckType.GasTrapRiskScore => new VentingRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.30m,
                WarningMaximumValue: 0.30m,
                FailMaximumValue: 0.70m,
                FailBelowValue: null,
                Unit: "score",
                Notes: "Gas trap risk increases around bosses, ribs, shutoff corners, inserts, and long flow-path endpoints."),

            _ => throw new ArgumentOutOfRangeException(nameof(checkType), checkType, "Unsupported venting check type.")
        };
    }

    private static EngineeringIssue EvaluateIssue(
        VentingRuleInput input,
        VentingRuleRange range)
    {
        var ruleId = BuildRuleId(input.CheckType);
        var featureType = input.CheckType.ToString();

        if (input.HasEngineerOverride)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.override",
                category: "Venting",
                message: "Venting rule has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved venting override rationale.",
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
                category: "Venting",
                message: "Venting validation value cannot be negative.",
                correctiveAction: "Provide a non-negative measured or configured venting value.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit);
        }

        if (range.FailBelowValue is not null &&
            input.ActualValue < range.FailBelowValue.Value &&
            (input.IsCriticalToQuality || input.IsLongFlowPath))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.below-fail-minimum",
                category: "Venting",
                message: "Venting value is below the fail threshold for a critical or long-flow condition.",
                correctiveAction: "Increase vent capability or request qualified mold engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.FailBelowValue.Value,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit);
        }

        if (input.ActualValue < range.MinimumValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.below-minimum",
                category: "Venting",
                message: "Venting value is below the expert preliminary minimum.",
                correctiveAction: "Review gas evacuation, burn risk, short-shot risk, and end-of-fill venting.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsLongFlowPath);
        }

        if (input.ActualValue > range.FailMaximumValue &&
            (input.IsCriticalToQuality || input.IsFlashSensitive))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.above-fail-maximum",
                category: "Venting",
                message: "Venting value exceeds the expert fail threshold for a flash-sensitive or critical condition.",
                correctiveAction: "Reduce vent dimension/risk or redesign venting strategy before tooling.",
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
                category: "Venting",
                message: "Venting value exceeds the expert recommended range.",
                correctiveAction: "Review flash risk, gas evacuation, vent land, and process sensitivity.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.WarningMaximumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsFlashSensitive);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "Venting",
            message: "Venting value satisfies the expert preliminary rule.",
            correctiveAction: "No action required beyond normal engineering review.",
            sourceRulePackVersion: RulePackVersion,
            featureType: featureType,
            material: null,
            actualValue: input.ActualValue,
            requiredValue: range.MinimumValue,
            recommendedValue: range.RecommendedMaximumValue,
            unit: range.Unit);
    }

    private static string BuildRuleId(VentingCheckType checkType)
    {
        return $"venting.{checkType}".ToLowerInvariant();
    }
}