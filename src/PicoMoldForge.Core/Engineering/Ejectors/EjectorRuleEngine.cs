using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Ejectors;

public sealed class EjectorRuleEngine
{
    public const string RulePackVersion = "expert-injection-mold-rules.v1";

    public EngineeringRuleResult Evaluate(EjectorRuleInput input)
    {
        var range = ResolveRange(input.CheckType);
        var issue = EvaluateIssue(input, range);

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "Ejector",
            Issues: new[] { issue });
    }

    public EjectorRuleRange ResolveRange(EjectorCheckType checkType)
    {
        return checkType switch
        {
            EjectorCheckType.PinLandClearanceMm => new EjectorRuleRange(
                MinimumValue: 0.02m,
                RecommendedMinimumValue: 0.02m,
                RecommendedMaximumValue: 0.05m,
                WarningMaximumValue: 0.05m,
                FailMaximumValue: 0.10m,
                FailBelowValue: 0.01m,
                Unit: "mm",
                Notes: "Ejector pin land clearance is commonly 0.02 mm to 0.05 mm. Too tight risks galling; too loose risks flash."),

            EjectorCheckType.SurfacePlacementRiskScore => new EjectorRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.30m,
                WarningMaximumValue: 0.30m,
                FailMaximumValue: 0.70m,
                FailBelowValue: null,
                Unit: "score",
                Notes: "Prefer ejectors on stiff, non-cosmetic, thick-supported areas. Avoid cosmetic faces, thin walls, and fragile surfaces."),

            EjectorCheckType.DraftAtEjectorLocationDeg => new EjectorRuleRange(
                MinimumValue: 0.50m,
                RecommendedMinimumValue: 0.50m,
                RecommendedMaximumValue: 1.00m,
                WarningMaximumValue: 1.00m,
                FailMaximumValue: 99m,
                FailBelowValue: 0.25m,
                Unit: "deg",
                Notes: "Ejection risk increases when local draft is low. Deep, textured, or low-draft parts require more ejection review."),

            EjectorCheckType.EjectorConcentrationRatio => new EjectorRuleRange(
                MinimumValue: 0m,
                RecommendedMinimumValue: 0m,
                RecommendedMaximumValue: 0.50m,
                WarningMaximumValue: 0.50m,
                FailMaximumValue: 0.75m,
                FailBelowValue: null,
                Unit: "ratio",
                Notes: "High ejector concentration can mark, deform, or locally overstress the part."),

            _ => throw new ArgumentOutOfRangeException(nameof(checkType), checkType, "Unsupported ejector check type.")
        };
    }

    private static EngineeringIssue EvaluateIssue(
        EjectorRuleInput input,
        EjectorRuleRange range)
    {
        var ruleId = BuildRuleId(input.CheckType);
        var featureType = input.CheckType.ToString();

        if (input.HasEngineerOverride)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.override",
                category: "Ejector",
                message: "Ejector rule has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved ejector override rationale.",
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
                category: "Ejector",
                message: "Ejector validation value cannot be negative.",
                correctiveAction: "Provide a non-negative measured or configured ejector value.",
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
            (input.IsCriticalToQuality || input.IsThinWall || input.IsCosmeticSurface))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.below-fail-minimum",
                category: "Ejector",
                message: "Ejector value is below the fail threshold for a cosmetic, thin-wall, or critical condition.",
                correctiveAction: "Increase clearance/draft or redesign the ejection strategy before tooling.",
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
                category: "Ejector",
                message: "Ejector value is below the expert preliminary minimum.",
                correctiveAction: "Review ejector clearance, local draft, and part-release risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.MinimumValue,
                recommendedValue: range.RecommendedMinimumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsThinWall || input.IsCosmeticSurface);
        }

        if (input.ActualValue > range.FailMaximumValue &&
            (input.IsCriticalToQuality || input.IsThinWall || input.IsCosmeticSurface))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.above-fail-maximum",
                category: "Ejector",
                message: "Ejector value exceeds the expert fail threshold for a cosmetic, thin-wall, or critical condition.",
                correctiveAction: "Move, resize, redistribute, or redesign ejector features before tooling.",
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
                category: "Ejector",
                message: "Ejector value exceeds the expert recommended range.",
                correctiveAction: "Review ejector marks, deformation risk, pin concentration, and local support.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: null,
                actualValue: input.ActualValue,
                requiredValue: range.WarningMaximumValue,
                recommendedValue: range.RecommendedMaximumValue,
                unit: range.Unit,
                requiresEngineerReview: input.IsCriticalToQuality || input.IsThinWall || input.IsCosmeticSurface);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "Ejector",
            message: "Ejector value satisfies the expert preliminary rule.",
            correctiveAction: "No action required beyond normal engineering review.",
            sourceRulePackVersion: RulePackVersion,
            featureType: featureType,
            material: null,
            actualValue: input.ActualValue,
            requiredValue: range.MinimumValue,
            recommendedValue: range.RecommendedMaximumValue,
            unit: range.Unit);
    }

    private static string BuildRuleId(EjectorCheckType checkType)
    {
        return $"ejector.{checkType}".ToLowerInvariant();
    }
}