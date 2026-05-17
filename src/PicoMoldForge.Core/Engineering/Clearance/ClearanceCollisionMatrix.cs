using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Clearance;

public sealed class ClearanceCollisionMatrix
{
    public const string RulePackVersion = "picomoldforge.clearance-collision-matrix.v1";

    public ClearanceCollisionMatrixResult Evaluate(ClearanceCollisionMatrixInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateInput(input);

        var pairs = BuildPairs(input).ToArray();
        var summary = Summarize(input.Features.Count, pairs);
        var ruleResult = BuildRuleResult(input, pairs, summary);

        return new ClearanceCollisionMatrixResult(pairs, summary, ruleResult);
    }

    private static void ValidateInput(ClearanceCollisionMatrixInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Features);

        if (input.GlobalMinimumClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.GlobalMinimumClearanceMm),
                "Global minimum clearance cannot be negative.");
        }
    }

    private static IEnumerable<ClearancePairResult> BuildPairs(ClearanceCollisionMatrixInput input)
    {
        for (var a = 0; a < input.Features.Count; a++)
        {
            for (var b = a + 1; b < input.Features.Count; b++)
            {
                yield return EvaluatePair(
                    input.Features[a],
                    input.Features[b],
                    input.GlobalMinimumClearanceMm);
            }
        }
    }

    private static ClearancePairResult EvaluatePair(
        ClearanceFeature a,
        ClearanceFeature b,
        decimal globalMinimumClearanceMm)
    {
        var centerlineDistance = DistanceBetweenSegments(a.Start, a.End, b.Start, b.End);
        var surfaceClearance = centerlineDistance - Math.Max(0m, a.RadiusMm) - Math.Max(0m, b.RadiusMm);
        var requiredClearance = Math.Max(
            globalMinimumClearanceMm,
            Math.Max(Math.Max(0m, a.RequiredClearanceMm), Math.Max(0m, b.RequiredClearanceMm)));

        var margin = surfaceClearance - requiredClearance;
        var criticalPair = a.IsCriticalToQuality || b.IsCriticalToQuality || a.IsCosmeticCritical || b.IsCosmeticCritical;

        return new ClearancePairResult(
            FeatureAId: NormalizeFeatureId(a.FeatureId),
            FeatureAKind: a.Kind,
            FeatureBId: NormalizeFeatureId(b.FeatureId),
            FeatureBKind: b.Kind,
            CenterlineDistanceMm: Math.Round(centerlineDistance, 6),
            SurfaceClearanceMm: Math.Round(surfaceClearance, 6),
            RequiredClearanceMm: requiredClearance,
            ClearanceMarginMm: Math.Round(margin, 6),
            HasCollisionRisk: margin < 0m,
            IsCriticalPair: criticalPair);
    }

    private static ClearanceCollisionMatrixSummary Summarize(
        int featureCount,
        IReadOnlyList<ClearancePairResult> pairs)
    {
        var minimumSurfaceClearance = pairs.Count == 0
            ? 0m
            : pairs.Min(pair => pair.SurfaceClearanceMm);

        var minimumMargin = pairs.Count == 0
            ? 0m
            : pairs.Min(pair => pair.ClearanceMarginMm);

        return new ClearanceCollisionMatrixSummary(
            FeatureCount: featureCount,
            PairCount: pairs.Count,
            CollisionRiskPairCount: pairs.Count(pair => pair.HasCollisionRisk),
            CriticalRiskPairCount: pairs.Count(pair => pair.HasCollisionRisk && pair.IsCriticalPair),
            MinimumSurfaceClearanceMm: minimumSurfaceClearance,
            MinimumClearanceMarginMm: minimumMargin);
    }

    private static EngineeringRuleResult BuildRuleResult(
        ClearanceCollisionMatrixInput input,
        IReadOnlyList<ClearancePairResult> pairs,
        ClearanceCollisionMatrixSummary summary)
    {
        var issues = new List<EngineeringIssue>();

        if (input.HasEngineerOverride)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "clearance-matrix.override",
                category: "ClearanceCollisionMatrix",
                message: "Clearance collision matrix has an engineer override and requires documented review.",
                correctiveAction: "Document the engineer-approved clearance override.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "ClearanceMatrix",
                material: null));
        }

        if (input.Features.Count < 2)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "clearance-matrix.features.insufficient",
                category: "ClearanceCollisionMatrix",
                message: "At least two features are required to evaluate clearance collisions.",
                correctiveAction: "Provide cooling, gate, ejector, cavity, or mold-edge features before reporting clearance matrix quality.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "ClearanceMatrix",
                material: null));

            return new EngineeringRuleResult(RulePackVersion, "ClearanceCollisionMatrix", issues);
        }

        foreach (var feature in input.Features)
        {
            AddFeatureIssues(feature, issues);
        }

        foreach (var pair in pairs)
        {
            AddPairIssues(pair, issues);
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "clearance-matrix.pass",
                category: "ClearanceCollisionMatrix",
                message: "All feature pairs satisfy the preliminary clearance matrix.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "ClearanceMatrix",
                material: null,
                actualValue: summary.MinimumClearanceMarginMm,
                requiredValue: 0m,
                recommendedValue: null,
                unit: "mm"));
        }

        return new EngineeringRuleResult(RulePackVersion, "ClearanceCollisionMatrix", issues);
    }

    private static void AddFeatureIssues(
        ClearanceFeature feature,
        List<EngineeringIssue> issues)
    {
        var featureId = NormalizeFeatureId(feature.FeatureId);
        var ruleIdBase = $"clearance-matrix.{NormalizeRuleId(featureId)}";

        if (featureId == "missing")
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.id-missing",
                category: "ClearanceCollisionMatrix",
                message: "Clearance feature is missing a stable FeatureId.",
                correctiveAction: "Assign a stable FeatureId before collision-matrix reporting.",
                sourceRulePackVersion: RulePackVersion,
                featureType: feature.Kind.ToString(),
                material: null));
        }

        if (feature.RadiusMm < 0m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.radius-negative",
                category: "ClearanceCollisionMatrix",
                message: "Clearance feature radius cannot be negative.",
                correctiveAction: "Provide a non-negative feature radius.",
                sourceRulePackVersion: RulePackVersion,
                featureType: feature.Kind.ToString(),
                material: null,
                actualValue: feature.RadiusMm,
                requiredValue: 0m,
                recommendedValue: null,
                unit: "mm"));
        }

        if (feature.RequiredClearanceMm < 0m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.required-clearance-negative",
                category: "ClearanceCollisionMatrix",
                message: "Feature required clearance cannot be negative.",
                correctiveAction: "Provide a non-negative required clearance.",
                sourceRulePackVersion: RulePackVersion,
                featureType: feature.Kind.ToString(),
                material: null,
                actualValue: feature.RequiredClearanceMm,
                requiredValue: 0m,
                recommendedValue: null,
                unit: "mm"));
        }
    }

    private static void AddPairIssues(
        ClearancePairResult pair,
        List<EngineeringIssue> issues)
    {
        if (!pair.HasCollisionRisk)
        {
            return;
        }

        var ruleId = $"clearance-matrix.{NormalizeRuleId(pair.FeatureAId)}.{NormalizeRuleId(pair.FeatureBId)}";

        if (pair.SurfaceClearanceMm < 0m || pair.IsCriticalPair)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.collision-risk",
                category: "ClearanceCollisionMatrix",
                message: "Feature pair violates required clearance and has collision/interference risk.",
                correctiveAction: "Move, resize, or reroute one of the conflicting features before treating the mold alpha as functional.",
                sourceRulePackVersion: RulePackVersion,
                featureType: $"{pair.FeatureAKind}-{pair.FeatureBKind}",
                material: null,
                actualValue: pair.SurfaceClearanceMm,
                requiredValue: pair.RequiredClearanceMm,
                recommendedValue: pair.RequiredClearanceMm,
                unit: "mm"));
            return;
        }

        issues.Add(EngineeringIssueFactory.Warning(
            ruleId: $"{ruleId}.clearance-warning",
            category: "ClearanceCollisionMatrix",
            message: "Feature pair is below the required clearance margin.",
            correctiveAction: "Review feature spacing before downstream geometry generation.",
            sourceRulePackVersion: RulePackVersion,
            featureType: $"{pair.FeatureAKind}-{pair.FeatureBKind}",
            material: null,
            actualValue: pair.SurfaceClearanceMm,
            requiredValue: pair.RequiredClearanceMm,
            recommendedValue: pair.RequiredClearanceMm,
            unit: "mm",
            requiresEngineerReview: true));
    }

    private static decimal DistanceBetweenSegments(
        ClearancePoint a0,
        ClearancePoint a1,
        ClearancePoint b0,
        ClearancePoint b1)
    {
        var distances = new[]
        {
            DistancePointToSegment(a0, b0, b1),
            DistancePointToSegment(a1, b0, b1),
            DistancePointToSegment(b0, a0, a1),
            DistancePointToSegment(b1, a0, a1)
        };

        return distances.Min();
    }

    private static decimal DistancePointToSegment(
        ClearancePoint point,
        ClearancePoint segmentStart,
        ClearancePoint segmentEnd)
    {
        var vx = segmentEnd.Xmm - segmentStart.Xmm;
        var vy = segmentEnd.Ymm - segmentStart.Ymm;
        var vz = segmentEnd.Zmm - segmentStart.Zmm;

        var wx = point.Xmm - segmentStart.Xmm;
        var wy = point.Ymm - segmentStart.Ymm;
        var wz = point.Zmm - segmentStart.Zmm;

        var segmentLengthSquared = (vx * vx) + (vy * vy) + (vz * vz);

        if (segmentLengthSquared <= 0m)
        {
            return Distance(point, segmentStart);
        }

        var projection = ((wx * vx) + (wy * vy) + (wz * vz)) / segmentLengthSquared;
        var clamped = Clamp(projection, 0m, 1m);

        var closest = new ClearancePoint(
            segmentStart.Xmm + (clamped * vx),
            segmentStart.Ymm + (clamped * vy),
            segmentStart.Zmm + (clamped * vz));

        return Distance(point, closest);
    }

    private static decimal Distance(ClearancePoint a, ClearancePoint b)
    {
        var dx = a.Xmm - b.Xmm;
        var dy = a.Ymm - b.Ymm;
        var dz = a.Zmm - b.Zmm;

        return (decimal)Math.Sqrt((double)((dx * dx) + (dy * dy) + (dz * dz)));
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

    private static string NormalizeFeatureId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "missing" : value.Trim();
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