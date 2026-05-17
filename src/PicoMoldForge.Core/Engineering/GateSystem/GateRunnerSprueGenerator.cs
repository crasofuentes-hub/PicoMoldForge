using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.GateSystem;

public sealed class GateRunnerSprueGenerator
{
    public const string RulePackVersion = "picomoldforge.gate-runner-sprue.v1";

    public GateRunnerSprueGenerationResult Plan(GateRunnerSprueGenerationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateInput(input);

        var segmentResults = input.Segments
            .Select(segment => EvaluateSegment(segment, input))
            .ToArray();

        var summary = new GateRunnerSprueGenerationSummary(
            SegmentCount: segmentResults.Length,
            SprueCount: segmentResults.Count(segment => segment.FeatureType == GateSystemFeatureType.Sprue),
            RunnerCount: segmentResults.Count(segment => segment.FeatureType == GateSystemFeatureType.Runner),
            GateCount: segmentResults.Count(segment => segment.FeatureType == GateSystemFeatureType.Gate),
            GeneratableSegmentCount: segmentResults.Count(segment => segment.IsGeneratable),
            BlockedSegmentCount: segmentResults.Count(segment => !segment.IsGeneratable),
            TotalFlowLengthMm: segmentResults.Sum(segment => segment.LengthMm),
            TotalEstimatedVolumeMm3: segmentResults.Sum(segment => segment.EstimatedVolumeMm3));

        var ruleResult = BuildRuleResult(segmentResults, summary, input);

        return new GateRunnerSprueGenerationResult(segmentResults, summary, ruleResult);
    }

    private static void ValidateInput(GateRunnerSprueGenerationInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Segments);

        if (input.MoldBounds.SizeXmm <= 0m ||
            input.MoldBounds.SizeYmm <= 0m ||
            input.MoldBounds.SizeZmm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.MoldBounds), "Mold bounds must have positive X, Y, and Z sizes.");
        }

        if (input.RequiredCavityClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RequiredCavityClearanceMm), "Required cavity clearance cannot be negative.");
        }

        if (input.RequiredMoldEdgeClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RequiredMoldEdgeClearanceMm), "Required mold-edge clearance cannot be negative.");
        }
    }

    private static GateRunnerSprueSegmentResult EvaluateSegment(
        GateRunnerSprueSegment segment,
        GateRunnerSprueGenerationInput input)
    {
        var length = Distance(segment.Start, segment.End);
        var estimatedVolume = segment.FlowAreaMm2 > 0m && length > 0m
            ? segment.FlowAreaMm2 * length
            : 0m;

        var isInsideMoldBounds =
            IsPointInsideBounds(segment.Start, input.MoldBounds) &&
            IsPointInsideBounds(segment.End, input.MoldBounds);

        var hasCavityClearance = segment.MinimumCavityClearanceMm >= input.RequiredCavityClearanceMm;
        var hasMoldEdgeClearance = segment.MinimumMoldEdgeClearanceMm >= input.RequiredMoldEdgeClearanceMm;

        var isGeneratable =
            !string.IsNullOrWhiteSpace(segment.FeatureId) &&
            length > 0m &&
            segment.HydraulicDiameterMm > 0m &&
            segment.FlowAreaMm2 > 0m &&
            isInsideMoldBounds &&
            hasCavityClearance &&
            hasMoldEdgeClearance;

        return new GateRunnerSprueSegmentResult(
            FeatureId: string.IsNullOrWhiteSpace(segment.FeatureId) ? "missing" : segment.FeatureId,
            FeatureType: segment.FeatureType,
            LengthMm: Math.Round(length, 6),
            HydraulicDiameterMm: Math.Max(0m, segment.HydraulicDiameterMm),
            FlowAreaMm2: Math.Max(0m, segment.FlowAreaMm2),
            EstimatedVolumeMm3: Math.Round(estimatedVolume, 6),
            IsInsideMoldBounds: isInsideMoldBounds,
            HasRequiredCavityClearance: hasCavityClearance,
            HasRequiredMoldEdgeClearance: hasMoldEdgeClearance,
            IsGeneratable: isGeneratable);
    }

    private static EngineeringRuleResult BuildRuleResult(
        IReadOnlyList<GateRunnerSprueSegmentResult> segments,
        GateRunnerSprueGenerationSummary summary,
        GateRunnerSprueGenerationInput input)
    {
        var issues = new List<EngineeringIssue>();

        if (input.HasEngineerOverride)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "gate-system.override",
                category: "GateRunnerSprue",
                message: "Gate/runner/sprue system has an engineer override and requires documented review.",
                correctiveAction: "Document the engineer-approved gate/runner/sprue strategy.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "GateRunnerSprue",
                material: null));
        }

        if (segments.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "gate-system.segments.missing",
                category: "GateRunnerSprue",
                message: "No gate, runner, or sprue segments were provided.",
                correctiveAction: "Define at least one sprue, runner, and gate candidate before treating the alpha mold as feed-system capable.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "GateRunnerSprue",
                material: null));

            return new EngineeringRuleResult(RulePackVersion, "GateRunnerSprue", issues);
        }

        if (summary.SprueCount == 0)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "gate-system.sprue.missing",
                category: "GateRunnerSprue",
                message: "No sprue segment is defined.",
                correctiveAction: "Add a sprue or document an alternate feed strategy.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Sprue",
                material: null));
        }

        if (summary.RunnerCount == 0)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: "gate-system.runner.missing",
                category: "GateRunnerSprue",
                message: "No runner segment is defined.",
                correctiveAction: "Add runner geometry or document a direct-gate strategy.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Runner",
                material: null,
                requiresEngineerReview: true));
        }

        if (summary.GateCount == 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "gate-system.gate.missing",
                category: "GateRunnerSprue",
                message: "No gate segment is defined.",
                correctiveAction: "Add at least one gate candidate before treating the feed system as functional.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Gate",
                material: null));
        }

        foreach (var segment in segments)
        {
            AddSegmentIssue(segment, input, issues);
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "gate-system.pass",
                category: "GateRunnerSprue",
                message: "Gate/runner/sprue system satisfies the preliminary generation contract.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "GateRunnerSprue",
                material: null,
                actualValue: summary.TotalEstimatedVolumeMm3,
                requiredValue: 0m,
                recommendedValue: null,
                unit: "mm3"));
        }

        return new EngineeringRuleResult(RulePackVersion, "GateRunnerSprue", issues);
    }

    private static void AddSegmentIssue(
        GateRunnerSprueSegmentResult segment,
        GateRunnerSprueGenerationInput input,
        List<EngineeringIssue> issues)
    {
        var ruleIdBase = $"gate-system.{NormalizeRuleId(segment.FeatureId)}";

        if (segment.FeatureId == "missing")
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.id-missing",
                category: "GateRunnerSprue",
                message: "Gate-system segment is missing a stable FeatureId.",
                correctiveAction: "Assign a stable FeatureId before generation planning.",
                sourceRulePackVersion: RulePackVersion,
                featureType: segment.FeatureType.ToString(),
                material: null));
        }

        if (segment.LengthMm <= 0m ||
            segment.HydraulicDiameterMm <= 0m ||
            segment.FlowAreaMm2 <= 0m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.invalid-geometry",
                category: "GateRunnerSprue",
                message: "Gate-system segment has invalid length, hydraulic diameter, or flow area.",
                correctiveAction: "Provide non-zero length, positive hydraulic diameter, and positive flow area.",
                sourceRulePackVersion: RulePackVersion,
                featureType: segment.FeatureType.ToString(),
                material: null,
                actualValue: segment.FlowAreaMm2,
                requiredValue: 0.01m,
                recommendedValue: null,
                unit: "mm2"));
        }

        if (!segment.IsInsideMoldBounds)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.outside-mold-bounds",
                category: "GateRunnerSprue",
                message: "Gate-system segment endpoint is outside mold bounds.",
                correctiveAction: "Move the segment inside the mold block.",
                sourceRulePackVersion: RulePackVersion,
                featureType: segment.FeatureType.ToString(),
                material: null));
        }

        if (!segment.HasRequiredCavityClearance)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.cavity-clearance",
                category: "GateRunnerSprue",
                message: "Gate-system segment does not satisfy required cavity clearance.",
                correctiveAction: "Increase feed-system clearance from cavity or redesign the segment.",
                sourceRulePackVersion: RulePackVersion,
                featureType: segment.FeatureType.ToString(),
                material: null,
                requiredValue: input.RequiredCavityClearanceMm,
                recommendedValue: input.RequiredCavityClearanceMm,
                unit: "mm"));
        }

        if (!segment.HasRequiredMoldEdgeClearance)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.mold-edge-clearance",
                category: "GateRunnerSprue",
                message: "Gate-system segment does not satisfy required mold-edge clearance.",
                correctiveAction: "Move the segment farther from mold edges or review mold strength.",
                sourceRulePackVersion: RulePackVersion,
                featureType: segment.FeatureType.ToString(),
                material: null,
                requiredValue: input.RequiredMoldEdgeClearanceMm,
                recommendedValue: input.RequiredMoldEdgeClearanceMm,
                unit: "mm",
                requiresEngineerReview: true));
        }
    }

    private static bool IsPointInsideBounds(GateSystemPoint point, GateSystemBounds bounds)
    {
        return point.Xmm >= bounds.MinXmm &&
            point.Xmm <= bounds.MaxXmm &&
            point.Ymm >= bounds.MinYmm &&
            point.Ymm <= bounds.MaxYmm &&
            point.Zmm >= bounds.MinZmm &&
            point.Zmm <= bounds.MaxZmm;
    }

    private static decimal Distance(GateSystemPoint a, GateSystemPoint b)
    {
        var dx = a.Xmm - b.Xmm;
        var dy = a.Ymm - b.Ymm;
        var dz = a.Zmm - b.Zmm;

        return (decimal)Math.Sqrt((double)((dx * dx) + (dy * dy) + (dz * dz)));
    }

    private static string NormalizeRuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "missing";
        }

        return value.Trim().Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
    }
}