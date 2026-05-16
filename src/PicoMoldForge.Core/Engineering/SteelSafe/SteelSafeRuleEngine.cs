using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.SteelSafe;

public sealed class SteelSafeRuleEngine
{
    public const string RulePackVersion = "expert-injection-mold-rules.v1";

    public EngineeringRuleResult Evaluate(SteelSafeRuleInput input)
    {
        var range = ResolveRange(input.CheckType);
        var issue = EvaluateIssue(input, range);

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "SteelSafe",
            Issues: new[] { issue });
    }

    public SteelSafeRuleRange ResolveRange(SteelSafeCheckType checkType)
    {
        return checkType switch
        {
            SteelSafeCheckType.GeneralAllowanceMm => new SteelSafeRuleRange(
                MinimumValue: 0.10m,
                RecommendedMinimumValue: 0.10m,
                RecommendedMaximumValue: 0.50m,
                WarningMaximumValue: 0.50m,
                FailMaximumValue: 1.00m,
                FailBelowValue: 0.01m,
                Unit: "mm",
                Notes: "General steel-safe allowance should commonly be 0.10 mm to 0.50 mm for preliminary tuning."),

            SteelSafeCheckType.CriticalDimensionAllowanceMm => new SteelSafeRuleRange(
                MinimumValue: 0.10m,
                RecommendedMinimumValue: 0.10m,
                RecommendedMaximumValue: 0.50m,
                WarningMaximumValue: 0.50m,
                FailMaximumValue: 1.00m,
                FailBelowValue: 0.01m,
                Unit: "mm",
                Notes: "Critical-to-quality dimensions require a steel-safe tuning path."),

            SteelSafeCheckType.ShutoffAllowanceMm => new SteelSafeRuleRange(
                MinimumValue: 0.10m,
                RecommendedMinimumValue: 0.10m,
                RecommendedMaximumValue: 0.50m,
                WarningMaximumValue: 0.50m,
                FailMaximumValue: 1.00m,
                FailBelowValue: 0.01m,
                Unit: "mm",
                Notes: "Shutoff and sealing areas should preserve a tuning margin to correct flash, mismatch, or wear."),

            SteelSafeCheckType.PartingLineAllowanceMm => new SteelSafeRuleRange(
                MinimumValue: 0.10m,
                RecommendedMinimumValue: 0.10m,
                RecommendedMaximumValue: 0.50m,
                WarningMaximumValue: 0.50m,
                FailMaximumValue: 1.00m,
                FailBelowValue: 0.01m,
                Unit: "mm",
                Notes: "Parting-line and shutoff tuning commonly require steel-safe margin."),

            SteelSafeCheckType.CosmeticReworkRiskScore => new SteelSafeRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.30m,
                WarningMaximumValue: 0.30m,
                FailMaximumValue: 0.70m,
                FailBelowValue: null,
                Unit: "score",
                Notes: "Aggressive rework on cosmetic boundaries can damage appearance and requires review."),

            _ => throw new ArgumentOutOfRangeException(nameof(checkType), checkType, "Unsupported steel-safe check type.")
        };
    }

    private static EngineeringIssue EvaluateIssue(
        SteelSafeRuleInput input,
        SteelSafeRuleRange range)
    {
        var ruleId = BuildRuleId(input.CheckType);
        var featureType = input.CheckType.ToString();

        if (input.HasEngineerOverride)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.override",
                category: "SteelSafe",
                message: "Steel-safe rule has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved steel-safe override rationale.",
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
                category: "SteelSafe",
                message: "Steel-safe validation value cannot be negative.",
                correctiveAction: "Provide a non-negative steel-safe allowance or risk score.",
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
            (input.IsCriticalToQuality ||
             input.CheckType == SteelSafeCheckType.CriticalDimensionAllowanceMm ||
             input.CheckType == SteelSafeCheckType.ShutoffAllowanceMm ||
             input.CheckType == SteelSafeCheckType.PartingLineAllowanceMm))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.missing-critical",
                category: "SteelSafe",
                message: "Critical steel-safe allowance is missing or effectively zero.",
                correctiveAction: "Add steel-safe margin or document a qualified mold-engineering exception.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit);
        }

        if (input.ActualValue < range.MinimumValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.below-minimum",
                category: "SteelSafe",
                message: "Steel-safe allowance is below the expert preliminary minimum.",
                correctiveAction: "Increase steel-safe allowance or request qualified mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsCosmeticSurface);
        }

        if (input.ActualValue > range.FailMaximumValue &&
            (input.IsCriticalToQuality || input.IsCosmeticSurface))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.above-fail-maximum",
                category: "SteelSafe",
                message: "Steel-safe value exceeds the fail threshold for a cosmetic or critical condition.",
                correctiveAction: "Reduce rework risk, split the tuning strategy, or request qualified mold-engineering review.",
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
                category: "SteelSafe",
                message: "Steel-safe value exceeds the expert recommended range.",
                correctiveAction: "Review tuning allowance, rework risk, cosmetic impact, and tooling strategy.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.WarningMaximumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsCosmeticSurface);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "SteelSafe",
            message: "Steel-safe value satisfies the expert preliminary rule.",
            correctiveAction: "No action required beyond normal engineering review.",
            sourceRulePackVersion: RulePackVersion,
            featureType: featureType,
            material: null,
            actualValue: input.ActualValue,
            requiredValue: range.MinimumValue,
            recommendedValue: range.RecommendedMaximumValue,
            unit: range.Unit);
    }

    private static string BuildRuleId(SteelSafeCheckType checkType)
    {
        return $"steelsafe.{checkType}".ToLowerInvariant();
    }
}