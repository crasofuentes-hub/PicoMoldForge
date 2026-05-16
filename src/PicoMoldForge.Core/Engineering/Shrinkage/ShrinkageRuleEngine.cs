using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Shrinkage;

public sealed class ShrinkageRuleEngine
{
    public const string RulePackVersion = "expert-injection-mold-rules.v1";

    public EngineeringRuleResult Evaluate(ShrinkageRuleInput input)
    {
        var range = ResolveRange(input.Material);
        var issue = EvaluateIssue(input, range);

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "Shrinkage",
            Issues: new[] { issue });
    }

    public decimal CalculateCavityDimension(decimal nominalDimensionMm, decimal shrinkageRate)
    {
        if (nominalDimensionMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalDimensionMm), "Nominal dimension must be greater than zero.");
        }

        if (shrinkageRate < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(shrinkageRate), "Shrinkage rate cannot be negative.");
        }

        return nominalDimensionMm * (1m + shrinkageRate);
    }

    public ShrinkageRuleRange ResolveRange(ShrinkageMaterial material)
    {
        return material switch
        {
            ShrinkageMaterial.Abs => new ShrinkageRuleRange(
                material,
                MinimumRate: 0.004m,
                MaximumRate: 0.007m,
                RecommendedRate: 0.005m,
                Notes: "ABS typical injection molding shrinkage range: 0.4% to 0.7%."),

            ShrinkageMaterial.Pp => new ShrinkageRuleRange(
                material,
                MinimumRate: 0.010m,
                MaximumRate: 0.025m,
                RecommendedRate: 0.018m,
                Notes: "PP typical injection molding shrinkage range: 1.0% to 2.5%."),

            ShrinkageMaterial.Pc => new ShrinkageRuleRange(
                material,
                MinimumRate: 0.005m,
                MaximumRate: 0.007m,
                RecommendedRate: 0.006m,
                Notes: "PC typical injection molding shrinkage range: 0.5% to 0.7%."),

            ShrinkageMaterial.NylonPa => new ShrinkageRuleRange(
                material,
                MinimumRate: 0.010m,
                MaximumRate: 0.020m,
                RecommendedRate: 0.015m,
                Notes: "PA/Nylon dry typical range: 1.0% to 2.0%; moisture and fiber content may change this."),

            ShrinkageMaterial.Pom => new ShrinkageRuleRange(
                material,
                MinimumRate: 0.015m,
                MaximumRate: 0.025m,
                RecommendedRate: 0.020m,
                Notes: "POM typical injection molding shrinkage range: 1.5% to 2.5%."),

            ShrinkageMaterial.Pe => new ShrinkageRuleRange(
                material,
                MinimumRate: 0.015m,
                MaximumRate: 0.030m,
                RecommendedRate: 0.022m,
                Notes: "PE typical injection molding shrinkage range: 1.5% to 3.0%."),

            _ => new ShrinkageRuleRange(
                material,
                MinimumRate: 0.000m,
                MaximumRate: 0.000m,
                RecommendedRate: 0.000m,
                Notes: "General material requires datasheet-provided shrinkage.")
        };
    }

    private static EngineeringIssue EvaluateIssue(
        ShrinkageRuleInput input,
        ShrinkageRuleRange range)
    {
        var ruleId = BuildRuleId(input.Material);
        var material = input.Material.ToString();

        if (input.HasEngineerOverride)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.override",
                category: "Shrinkage",
                message: "Shrinkage rule has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved shrinkage override rationale.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Dimension",
                material: material,
                actualValue: input.ActualShrinkageRate,
                requiredValue: range.MinimumRate,
                recommendedValue: range.RecommendedRate,
                unit: "ratio");
        }

        if (input.ActualShrinkageRate is null)
        {
            if (input.IsCriticalDimension)
            {
                return EngineeringIssueFactory.Fail(
                    ruleId: $"{ruleId}.missing-critical",
                    category: "Shrinkage",
                    message: "Critical dimension has no shrinkage model.",
                    correctiveAction: "Provide resin datasheet shrinkage or engineer-approved shrinkage compensation.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: "CriticalDimension",
                    material: material,
                    actualValue: null,
                    requiredValue: range.MinimumRate,
                    recommendedValue: range.RecommendedRate,
                    unit: "ratio");
            }

            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.missing",
                category: "Shrinkage",
                message: "No shrinkage allowance was provided.",
                correctiveAction: "Provide resin datasheet shrinkage before tooling decisions.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Dimension",
                material: material,
                actualValue: null,
                requiredValue: range.MinimumRate,
                recommendedValue: range.RecommendedRate,
                unit: "ratio",
                requiresEngineerReview: true);
        }

        if (input.ActualShrinkageRate <= 0m)
        {
            if (input.IsCriticalDimension)
            {
                return EngineeringIssueFactory.Fail(
                    ruleId: $"{ruleId}.zero-critical",
                    category: "Shrinkage",
                    message: "Critical dimension has zero or negative shrinkage allowance.",
                    correctiveAction: "Apply shrinkage compensation from resin data or document an engineer-approved exception.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: "CriticalDimension",
                    material: material,
                    actualValue: input.ActualShrinkageRate,
                    requiredValue: range.MinimumRate,
                    recommendedValue: range.RecommendedRate,
                    unit: "ratio");
            }

            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.zero",
                category: "Shrinkage",
                message: "Shrinkage allowance is zero or negative.",
                correctiveAction: "Apply preliminary shrinkage compensation before mold generation.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Dimension",
                material: material,
                actualValue: input.ActualShrinkageRate,
                requiredValue: range.MinimumRate,
                recommendedValue: range.RecommendedRate,
                unit: "ratio",
                requiresEngineerReview: true);
        }

        if (!input.UsesDatasheetValue)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.not-datasheet",
                category: "Shrinkage",
                message: "Shrinkage allowance is not marked as a resin datasheet value.",
                correctiveAction: "Use resin datasheet shrinkage first, then tune by mold trials.",
                sourceRulePackVersion: RulePackVersion,
                featureType: input.IsCriticalDimension ? "CriticalDimension" : "Dimension",
                material: material,
                actualValue: input.ActualShrinkageRate,
                requiredValue: range.MinimumRate,
                recommendedValue: range.RecommendedRate,
                unit: "ratio",
                requiresEngineerReview: input.IsCriticalDimension);
        }

        if (range.MinimumRate > 0m &&
            input.ActualShrinkageRate < range.MinimumRate)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.below-range",
                category: "Shrinkage",
                message: "Shrinkage allowance is below the expert typical range for this material.",
                correctiveAction: "Verify resin grade, filler, conditioning, and datasheet shrinkage before tooling.",
                sourceRulePackVersion: RulePackVersion,
                featureType: input.IsCriticalDimension ? "CriticalDimension" : "Dimension",
                material: material,
                actualValue: input.ActualShrinkageRate,
                requiredValue: range.MinimumRate,
                recommendedValue: range.RecommendedRate,
                unit: "ratio",
                requiresEngineerReview: input.IsCriticalDimension);
        }

        if (range.MaximumRate > 0m &&
            input.ActualShrinkageRate > range.MaximumRate)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.above-range",
                category: "Shrinkage",
                message: "Shrinkage allowance is above the expert typical range for this material.",
                correctiveAction: "Verify resin grade, filler, moisture conditioning, and process assumptions.",
                sourceRulePackVersion: RulePackVersion,
                featureType: input.IsCriticalDimension ? "CriticalDimension" : "Dimension",
                material: material,
                actualValue: input.ActualShrinkageRate,
                requiredValue: range.MaximumRate,
                recommendedValue: range.RecommendedRate,
                unit: "ratio",
                requiresEngineerReview: true);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "Shrinkage",
            message: "Shrinkage allowance satisfies the expert preliminary rule.",
            correctiveAction: "No action required beyond normal resin datasheet and trial validation.",
            sourceRulePackVersion: RulePackVersion,
            featureType: input.IsCriticalDimension ? "CriticalDimension" : "Dimension",
            material: material,
            actualValue: input.ActualShrinkageRate,
            requiredValue: range.MinimumRate,
            recommendedValue: range.RecommendedRate,
            unit: "ratio");
    }

    private static string BuildRuleId(ShrinkageMaterial material)
    {
        return $"shrinkage.{material}".ToLowerInvariant();
    }
}