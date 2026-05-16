using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Separation;

public sealed class CoreCavitySeparationValidator
{
    public const string RulePackVersion = "picomoldforge.core-cavity-separation.v1";

    public EngineeringRuleResult Validate(CoreCavitySeparationInput input)
    {
        var issues = new List<EngineeringIssue>();
        var summary = Summarize(input);

        if (input.HasEngineerOverride)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "separation.override",
                category: "CoreCavitySeparation",
                message: "Core/cavity separation has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved separation override rationale.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoreCavity",
                material: null,
                actualValue: summary.QualityScore,
                requiredValue: 0.85m,
                recommendedValue: 0.95m,
                unit: "score"));
        }

        AddArtifactIssues(input, issues);
        AddVoxelCountIssues(input, issues);

        if (input.HasCoreSideArtifact &&
            input.HasCavitySideArtifact &&
            input.CoreVoxelCount > 0 &&
            input.CavityVoxelCount > 0)
        {
            AddPartingMetadataIssues(input, issues);
            AddShutoffStrategyIssues(input, issues);
            AddOverlapIssues(input, summary, issues);
            AddGapIssues(input, summary, issues);
            AddBalanceIssues(summary, issues);
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "separation.quality.pass",
                category: "CoreCavitySeparation",
                message: "Core/cavity separation satisfies the preliminary quality validator.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoreCavity",
                material: null,
                actualValue: summary.QualityScore,
                requiredValue: 0.85m,
                recommendedValue: 0.95m,
                unit: "score"));
        }

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "CoreCavitySeparation",
            Issues: issues);
    }

    public CoreCavitySeparationSummary Summarize(CoreCavitySeparationInput input)
    {
        var core = Math.Max(0, input.CoreVoxelCount);
        var cavity = Math.Max(0, input.CavityVoxelCount);
        var total = core + cavity;

        var overlap = Math.Max(0, input.OverlapVoxelCount);
        var gap = Math.Max(0, input.GapVoxelCount);

        var overlapRatio = total <= 0 ? 0m : overlap / (decimal)total;
        var gapRatio = total <= 0 ? 0m : gap / (decimal)total;

        var largerHalf = Math.Max(core, cavity);
        var smallerHalf = Math.Min(core, cavity);
        var balanceRatio = largerHalf <= 0 ? 0m : smallerHalf / (decimal)largerHalf;

        var qualityPenalty =
            (overlapRatio * 4.0m) +
            (gapRatio * 4.0m) +
            ((1.0m - balanceRatio) * 0.25m);

        var qualityScore = Clamp(1.0m - qualityPenalty, 0m, 1m);

        return new CoreCavitySeparationSummary(
            TotalHalfVoxelCount: total,
            OverlapRatio: overlapRatio,
            GapRatio: gapRatio,
            BalanceRatio: balanceRatio,
            QualityScore: qualityScore);
    }

    private static void AddArtifactIssues(
        CoreCavitySeparationInput input,
        List<EngineeringIssue> issues)
    {
        if (!input.HasCoreSideArtifact)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "separation.core-artifact.missing",
                category: "CoreCavitySeparation",
                message: "Core-side artifact is missing.",
                correctiveAction: "Generate and verify the core-side mold half before reporting separation quality.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoreSide",
                material: null));
        }

        if (!input.HasCavitySideArtifact)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "separation.cavity-artifact.missing",
                category: "CoreCavitySeparation",
                message: "Cavity-side artifact is missing.",
                correctiveAction: "Generate and verify the cavity-side mold half before reporting separation quality.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CavitySide",
                material: null));
        }
    }

    private static void AddVoxelCountIssues(
        CoreCavitySeparationInput input,
        List<EngineeringIssue> issues)
    {
        if (input.CoreVoxelCount <= 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "separation.core-voxels.invalid",
                category: "CoreCavitySeparation",
                message: "Core-side voxel count is zero or negative.",
                correctiveAction: "Regenerate the core-side geometry and confirm the split produced non-empty geometry.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoreSide",
                material: null,
                actualValue: input.CoreVoxelCount,
                requiredValue: 1m,
                recommendedValue: null,
                unit: "count"));
        }

        if (input.CavityVoxelCount <= 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "separation.cavity-voxels.invalid",
                category: "CoreCavitySeparation",
                message: "Cavity-side voxel count is zero or negative.",
                correctiveAction: "Regenerate the cavity-side geometry and confirm the split produced non-empty geometry.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CavitySide",
                material: null,
                actualValue: input.CavityVoxelCount,
                requiredValue: 1m,
                recommendedValue: null,
                unit: "count"));
        }

        if (input.OverlapVoxelCount < 0 || input.GapVoxelCount < 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "separation.metrics.negative",
                category: "CoreCavitySeparation",
                message: "Separation overlap or gap metric is negative.",
                correctiveAction: "Provide non-negative separation quality metrics.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoreCavity",
                material: null));
        }
    }

    private static void AddPartingMetadataIssues(
        CoreCavitySeparationInput input,
        List<EngineeringIssue> issues)
    {
        if (!input.HasPartingMetadata)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: "separation.parting-metadata.missing",
                category: "CoreCavitySeparation",
                message: "Parting metadata is missing from the separation output.",
                correctiveAction: "Record parting axis, offset, and selection rationale in the output report.",
                sourceRulePackVersion: RulePackVersion,
                featureType: input.PartingAxis.ToString(),
                material: null,
                actualValue: input.PartingOffsetMm,
                requiredValue: null,
                recommendedValue: null,
                unit: "mm",
                requiresEngineerReview: true));
        }
    }

    private static void AddShutoffStrategyIssues(
        CoreCavitySeparationInput input,
        List<EngineeringIssue> issues)
    {
        if (!input.HasShutoffStrategy)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: "separation.shutoff-strategy.missing",
                category: "CoreCavitySeparation",
                message: "No shutoff strategy is recorded for the core/cavity separation.",
                correctiveAction: "Add a shutoff strategy contract or mark this separation for mold-engineer review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Shutoff",
                material: null,
                requiresEngineerReview: true));
        }
    }

    private static void AddOverlapIssues(
        CoreCavitySeparationInput input,
        CoreCavitySeparationSummary summary,
        List<EngineeringIssue> issues)
    {
        if (summary.OverlapRatio > 0.05m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "separation.overlap.fail",
                category: "CoreCavitySeparation",
                message: "Core/cavity overlap ratio exceeds the preliminary fail threshold.",
                correctiveAction: "Rescore the parting plane, repair split logic, or request mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Overlap",
                material: null,
                actualValue: summary.OverlapRatio,
                requiredValue: 0.05m,
                recommendedValue: 0.01m,
                unit: "ratio"));
            return;
        }

        if (summary.OverlapRatio > 0.01m)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: "separation.overlap.warning",
                category: "CoreCavitySeparation",
                message: "Core/cavity overlap ratio is above the recommended preliminary threshold.",
                correctiveAction: "Inspect split surfaces for overlaps before treating the mold halves as usable.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Overlap",
                material: null,
                actualValue: summary.OverlapRatio,
                requiredValue: 0.01m,
                recommendedValue: 0m,
                unit: "ratio",
                requiresEngineerReview: input.HasShutoffStrategy is false));
        }
    }

    private static void AddGapIssues(
        CoreCavitySeparationInput input,
        CoreCavitySeparationSummary summary,
        List<EngineeringIssue> issues)
    {
        if (summary.GapRatio > 0.05m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "separation.gap.fail",
                category: "CoreCavitySeparation",
                message: "Core/cavity gap ratio exceeds the preliminary fail threshold.",
                correctiveAction: "Repair the split, add shutoff strategy, or request mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Gap",
                material: null,
                actualValue: summary.GapRatio,
                requiredValue: 0.05m,
                recommendedValue: 0.01m,
                unit: "ratio"));
            return;
        }

        if (summary.GapRatio > 0.01m)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: "separation.gap.warning",
                category: "CoreCavitySeparation",
                message: "Core/cavity gap ratio is above the recommended preliminary threshold.",
                correctiveAction: "Inspect split surfaces for gaps before treating the mold halves as usable.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Gap",
                material: null,
                actualValue: summary.GapRatio,
                requiredValue: 0.01m,
                recommendedValue: 0m,
                unit: "ratio",
                requiresEngineerReview: input.HasShutoffStrategy is false));
        }
    }

    private static void AddBalanceIssues(
        CoreCavitySeparationSummary summary,
        List<EngineeringIssue> issues)
    {
        if (summary.BalanceRatio < 0.25m)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: "separation.balance.warning",
                category: "CoreCavitySeparation",
                message: "Core/cavity split is highly imbalanced by voxel count.",
                correctiveAction: "Review parting plane selection and confirm the imbalance is intentional.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "PartingPlane",
                material: null,
                actualValue: summary.BalanceRatio,
                requiredValue: 0.25m,
                recommendedValue: 0.50m,
                unit: "ratio",
                requiresEngineerReview: true));
        }
    }

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        if (value > maximum)
        {
            return maximum;
        }

        return value;
    }
}