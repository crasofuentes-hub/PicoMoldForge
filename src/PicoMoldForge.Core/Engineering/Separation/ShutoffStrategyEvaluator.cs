using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Separation;

public sealed class ShutoffStrategyEvaluator
{
    public const string RulePackVersion = "picomoldforge.shutoff-strategy.v1";

    public EngineeringRuleResult Evaluate(ShutoffStrategyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var issues = new List<EngineeringIssue>();
        var summary = Summarize(input);

        if (input.HasGlobalEngineerOverride)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "shutoff.strategy.override",
                category: "ShutoffStrategy",
                message: "Shutoff strategy has a global engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved shutoff strategy override.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "ShutoffStrategy",
                material: null,
                actualValue: summary.QualityScore,
                requiredValue: 0.85m,
                recommendedValue: 0.95m,
                unit: "score"));
        }

        if (input.Regions.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "shutoff.strategy.missing",
                category: "ShutoffStrategy",
                message: "No shutoff regions are defined for the core/cavity separation.",
                correctiveAction: "Define shutoff regions for parting line, holes, inserts, and side-action boundaries before treating the split as functional.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "ShutoffStrategy",
                material: null));
        }

        foreach (var region in input.Regions)
        {
            EvaluateRegion(region, issues);
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "shutoff.strategy.pass",
                category: "ShutoffStrategy",
                message: "Shutoff strategy satisfies the preliminary contract.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "ShutoffStrategy",
                material: null,
                actualValue: summary.QualityScore,
                requiredValue: 0.85m,
                recommendedValue: 0.95m,
                unit: "score"));
        }

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "ShutoffStrategy",
            Issues: issues);
    }

    public ShutoffStrategySummary Summarize(ShutoffStrategyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var regionCount = input.Regions.Count;
        var undefinedCount = input.Regions.Count(region =>
            region.ClosureState == ShutoffClosureState.NotDefined);

        var criticalCount = input.Regions.Count(region =>
            region.IsCriticalToQuality);

        var maxGap = regionCount == 0
            ? 0m
            : input.Regions.Max(region => Math.Max(0m, region.GapMm));

        var maxOverlap = regionCount == 0
            ? 0m
            : input.Regions.Max(region => Math.Max(0m, region.OverlapMm));

        var undefinedPenalty = regionCount == 0
            ? 0.40m
            : undefinedCount / (decimal)regionCount * 0.35m;

        var gapPenalty = maxGap * 8.0m;
        var overlapPenalty = maxOverlap * 8.0m;

        var qualityScore = Clamp(
            1.0m - undefinedPenalty - gapPenalty - overlapPenalty,
            0m,
            1m);

        return new ShutoffStrategySummary(
            RegionCount: regionCount,
            UndefinedRegionCount: undefinedCount,
            CriticalRegionCount: criticalCount,
            MaximumGapMm: maxGap,
            MaximumOverlapMm: maxOverlap,
            QualityScore: qualityScore);
    }

    private static void EvaluateRegion(
        ShutoffRegion region,
        List<EngineeringIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(region.RegionId))
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "shutoff.region.id.missing",
                category: "ShutoffStrategy",
                message: "Shutoff region is missing a stable RegionId.",
                correctiveAction: "Assign a stable RegionId before using the shutoff strategy in reports or automation.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null));
        }

        if (region.HasEngineerOverride)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.override",
                category: "ShutoffStrategy",
                message: "Shutoff region has an engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved shutoff override for this region.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                actualValue: region.ContactAreaMm2,
                requiredValue: 0m,
                recommendedValue: null,
                unit: "mm2"));
        }

        if (region.ContactAreaMm2 < 0m ||
            region.GapMm < 0m ||
            region.OverlapMm < 0m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.negative-metric",
                category: "ShutoffStrategy",
                message: "Shutoff region contains a negative contact, gap, or overlap metric.",
                correctiveAction: "Provide non-negative shutoff metrics.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null));
        }

        if (region.ClosureState == ShutoffClosureState.NotDefined)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.undefined",
                category: "ShutoffStrategy",
                message: "Shutoff region closure state is not defined.",
                correctiveAction: "Define whether the shutoff is preliminary, verified, or requires engineer review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                requiresEngineerReview: true));
        }

        if (region.ClosureState == ShutoffClosureState.NeedsEngineerReview)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.review",
                category: "ShutoffStrategy",
                message: "Shutoff region is explicitly marked for engineer review.",
                correctiveAction: "Review shutoff contact, wear risk, flash risk, and closure behavior.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                actualValue: region.ContactAreaMm2,
                requiredValue: 0m,
                recommendedValue: null,
                unit: "mm2"));
        }

        if (region.ContactAreaMm2 == 0m &&
            (region.IsCriticalToQuality || region.RegionType == ShutoffRegionType.PartingLine))
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.missing-contact",
                category: "ShutoffStrategy",
                message: "Critical shutoff region has no contact area.",
                correctiveAction: "Add shutoff contact or mark the region for qualified mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                actualValue: region.ContactAreaMm2,
                requiredValue: 0.01m,
                recommendedValue: null,
                unit: "mm2"));
        }

        if (region.GapMm > 0.05m &&
            (region.IsCriticalToQuality || region.IsCosmeticBoundary))
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.gap-fail",
                category: "ShutoffStrategy",
                message: "Shutoff region gap exceeds the fail threshold for a critical or cosmetic boundary.",
                correctiveAction: "Repair shutoff closure or request qualified mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                actualValue: region.GapMm,
                requiredValue: 0.05m,
                recommendedValue: 0.02m,
                unit: "mm"));
        }
        else if (region.GapMm > 0.02m)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.gap-warning",
                category: "ShutoffStrategy",
                message: "Shutoff region gap exceeds the recommended preliminary threshold.",
                correctiveAction: "Inspect shutoff closure for flash, mismatch, or leak risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                actualValue: region.GapMm,
                requiredValue: 0.02m,
                recommendedValue: 0m,
                unit: "mm",
                requiresEngineerReview: region.IsCriticalToQuality || region.IsCosmeticBoundary));
        }

        if (region.OverlapMm > 0.05m &&
            (region.IsCriticalToQuality || region.IsCosmeticBoundary))
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.overlap-fail",
                category: "ShutoffStrategy",
                message: "Shutoff region overlap exceeds the fail threshold for a critical or cosmetic boundary.",
                correctiveAction: "Repair the shutoff surface relationship before tooling.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                actualValue: region.OverlapMm,
                requiredValue: 0.05m,
                recommendedValue: 0.02m,
                unit: "mm"));
        }
        else if (region.OverlapMm > 0.02m)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"shutoff.region.{NormalizeRuleId(region.RegionId)}.overlap-warning",
                category: "ShutoffStrategy",
                message: "Shutoff region overlap exceeds the recommended preliminary threshold.",
                correctiveAction: "Inspect shutoff closure for interference and rework risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: region.RegionType.ToString(),
                material: null,
                actualValue: region.OverlapMm,
                requiredValue: 0.02m,
                recommendedValue: 0m,
                unit: "mm",
                requiresEngineerReview: region.IsCriticalToQuality || region.IsCosmeticBoundary));
        }
    }

    private static string NormalizeRuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "missing";
        }

        return value
            .Trim()
            .Replace(" ", "-", StringComparison.Ordinal)
            .ToLowerInvariant();
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