using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.EjectionGeometry;

public sealed class EjectorCandidateGenerator
{
    public const string RulePackVersion = "picomoldforge.ejector-candidate-generator.v1";

    public EjectorCandidateGenerationResult Plan(EjectorCandidateGenerationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateInput(input);

        var candidateResults = input.Candidates
            .Select(candidate => EvaluateCandidate(candidate, input))
            .ToArray();

        var summary = new EjectorCandidateGenerationSummary(
            CandidateCount: candidateResults.Length,
            AcceptedCandidateCount: candidateResults.Count(candidate => candidate.IsAccepted),
            BlockedCandidateCount: candidateResults.Count(candidate => !candidate.IsAccepted),
            CosmeticCandidateCount: candidateResults.Count(candidate => candidate.IsCosmeticSurface),
            CriticalCandidateCount: candidateResults.Count(candidate => candidate.IsCriticalToQuality),
            TotalAcceptedPinAreaMm2: Math.Round(
                candidateResults
                    .Where(candidate => candidate.IsAccepted)
                    .Sum(candidate => PinArea(candidate.PinDiameterMm)),
                6));

        var ruleResult = BuildRuleResult(candidateResults, summary, input);

        return new EjectorCandidateGenerationResult(candidateResults, summary, ruleResult);
    }

    private static void ValidateInput(EjectorCandidateGenerationInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Candidates);

        if (input.MoldBounds.SizeXmm <= 0m ||
            input.MoldBounds.SizeYmm <= 0m ||
            input.MoldBounds.SizeZmm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.MoldBounds), "Mold bounds must have positive X, Y, and Z sizes.");
        }

        if (input.RequiredCoolingClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RequiredCoolingClearanceMm), "Required cooling clearance cannot be negative.");
        }

        if (input.RequiredGateSystemClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RequiredGateSystemClearanceMm), "Required gate-system clearance cannot be negative.");
        }

        if (input.RequiredMoldEdgeClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RequiredMoldEdgeClearanceMm), "Required mold-edge clearance cannot be negative.");
        }

        if (input.MinimumSupportedSurfaceAreaMm2 < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.MinimumSupportedSurfaceAreaMm2), "Minimum supported surface area cannot be negative.");
        }
    }

    private static EjectorCandidateResult EvaluateCandidate(
        EjectorCandidate candidate,
        EjectorCandidateGenerationInput input)
    {
        var isInsideMoldBounds = IsPointInsideBounds(candidate.Location, input.MoldBounds);
        var hasCoolingClearance = candidate.MinimumCoolingClearanceMm >= input.RequiredCoolingClearanceMm;
        var hasGateClearance = candidate.MinimumGateSystemClearanceMm >= input.RequiredGateSystemClearanceMm;
        var hasMoldEdgeClearance = candidate.MinimumMoldEdgeClearanceMm >= input.RequiredMoldEdgeClearanceMm;
        var hasSurfaceSupport = candidate.SupportedSurfaceAreaMm2 >= input.MinimumSupportedSurfaceAreaMm2;

        var isAccepted =
            !string.IsNullOrWhiteSpace(candidate.CandidateId) &&
            candidate.PinDiameterMm > 0m &&
            candidate.StrokeMm > 0m &&
            isInsideMoldBounds &&
            hasCoolingClearance &&
            hasGateClearance &&
            hasMoldEdgeClearance &&
            hasSurfaceSupport &&
            !candidate.IsCosmeticSurface;

        return new EjectorCandidateResult(
            CandidateId: string.IsNullOrWhiteSpace(candidate.CandidateId) ? "missing" : candidate.CandidateId,
            PinDiameterMm: Math.Max(0m, candidate.PinDiameterMm),
            StrokeMm: Math.Max(0m, candidate.StrokeMm),
            SupportedSurfaceAreaMm2: Math.Max(0m, candidate.SupportedSurfaceAreaMm2),
            IsInsideMoldBounds: isInsideMoldBounds,
            HasRequiredCoolingClearance: hasCoolingClearance,
            HasRequiredGateSystemClearance: hasGateClearance,
            HasRequiredMoldEdgeClearance: hasMoldEdgeClearance,
            HasRequiredSurfaceSupport: hasSurfaceSupport,
            IsCosmeticSurface: candidate.IsCosmeticSurface,
            IsCriticalToQuality: candidate.IsCriticalToQuality,
            IsAccepted: isAccepted);
    }

    private static EngineeringRuleResult BuildRuleResult(
        IReadOnlyList<EjectorCandidateResult> candidates,
        EjectorCandidateGenerationSummary summary,
        EjectorCandidateGenerationInput input)
    {
        var issues = new List<EngineeringIssue>();

        if (input.HasGlobalEngineerOverride)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "ejector-candidates.override",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate generation has a global engineer override and requires documented review.",
                correctiveAction: "Document the engineer-approved ejector candidate strategy.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "EjectorCandidates",
                material: null));
        }

        if (candidates.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "ejector-candidates.missing",
                category: "EjectorCandidateGeneration",
                message: "No ejector candidates were provided.",
                correctiveAction: "Generate or define ejector pin candidates before treating the mold alpha as ejection-capable.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "EjectorCandidates",
                material: null));

            return new EngineeringRuleResult(RulePackVersion, "EjectorCandidateGeneration", issues);
        }

        foreach (var candidate in candidates)
        {
            AddCandidateIssues(candidate, input, issues);
        }

        if (summary.AcceptedCandidateCount == 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "ejector-candidates.none-accepted",
                category: "EjectorCandidateGeneration",
                message: "No ejector candidate satisfies the preliminary acceptance contract.",
                correctiveAction: "Add valid ejector candidates with adequate clearance, support, and non-cosmetic placement.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "EjectorCandidates",
                material: null));
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "ejector-candidates.pass",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidates satisfy the preliminary generation contract.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "EjectorCandidates",
                material: null,
                actualValue: summary.AcceptedCandidateCount,
                requiredValue: 1m,
                recommendedValue: null,
                unit: "count"));
        }

        return new EngineeringRuleResult(RulePackVersion, "EjectorCandidateGeneration", issues);
    }

    private static void AddCandidateIssues(
        EjectorCandidateResult candidate,
        EjectorCandidateGenerationInput input,
        List<EngineeringIssue> issues)
    {
        var ruleIdBase = $"ejector-candidates.{NormalizeRuleId(candidate.CandidateId)}";

        if (candidate.CandidateId == "missing")
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.id-missing",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate is missing a stable CandidateId.",
                correctiveAction: "Assign a stable CandidateId before layout planning.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "EjectorCandidate",
                material: null));
        }

        if (candidate.PinDiameterMm <= 0m || candidate.StrokeMm <= 0m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.invalid-geometry",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate has invalid pin diameter or stroke.",
                correctiveAction: "Provide positive pin diameter and positive ejection stroke.",
                sourceRulePackVersion: RulePackVersion,
                featureType: candidate.CandidateId,
                material: null,
                actualValue: candidate.PinDiameterMm,
                requiredValue: 0.01m,
                recommendedValue: null,
                unit: "mm"));
        }

        if (!candidate.IsInsideMoldBounds)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.outside-mold-bounds",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate is outside mold bounds.",
                correctiveAction: "Move ejector candidate inside the mold block.",
                sourceRulePackVersion: RulePackVersion,
                featureType: candidate.CandidateId,
                material: null));
        }

        if (!candidate.HasRequiredCoolingClearance)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.cooling-clearance",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate does not satisfy required cooling-channel clearance.",
                correctiveAction: "Move ejector candidate away from cooling channels or revise cooling layout.",
                sourceRulePackVersion: RulePackVersion,
                featureType: candidate.CandidateId,
                material: null,
                requiredValue: input.RequiredCoolingClearanceMm,
                recommendedValue: input.RequiredCoolingClearanceMm,
                unit: "mm"));
        }

        if (!candidate.HasRequiredGateSystemClearance)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.gate-system-clearance",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate does not satisfy required gate-system clearance.",
                correctiveAction: "Move ejector candidate away from sprue, runner, or gate geometry.",
                sourceRulePackVersion: RulePackVersion,
                featureType: candidate.CandidateId,
                material: null,
                requiredValue: input.RequiredGateSystemClearanceMm,
                recommendedValue: input.RequiredGateSystemClearanceMm,
                unit: "mm"));
        }

        if (!candidate.HasRequiredMoldEdgeClearance)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.mold-edge-clearance",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate does not satisfy required mold-edge clearance.",
                correctiveAction: "Move ejector candidate farther from mold edges or request mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: candidate.CandidateId,
                material: null,
                requiredValue: input.RequiredMoldEdgeClearanceMm,
                recommendedValue: input.RequiredMoldEdgeClearanceMm,
                unit: "mm",
                requiresEngineerReview: true));
        }

        if (!candidate.HasRequiredSurfaceSupport)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.surface-support",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate has insufficient supported surface area.",
                correctiveAction: "Move candidate to a stiffer/larger supported region or increase support.",
                sourceRulePackVersion: RulePackVersion,
                featureType: candidate.CandidateId,
                material: null,
                actualValue: candidate.SupportedSurfaceAreaMm2,
                requiredValue: input.MinimumSupportedSurfaceAreaMm2,
                recommendedValue: input.MinimumSupportedSurfaceAreaMm2,
                unit: "mm2",
                requiresEngineerReview: true));
        }

        if (candidate.IsCosmeticSurface)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.cosmetic-surface",
                category: "EjectorCandidateGeneration",
                message: "Ejector candidate is placed on a cosmetic surface.",
                correctiveAction: "Move ejector candidate to a non-cosmetic supported region or document customer-approved ejector mark strategy.",
                sourceRulePackVersion: RulePackVersion,
                featureType: candidate.CandidateId,
                material: null,
                requiresEngineerReview: true));
        }
    }

    private static bool IsPointInsideBounds(EjectorCandidatePoint point, EjectorLayoutBounds bounds)
    {
        return point.Xmm >= bounds.MinXmm &&
            point.Xmm <= bounds.MaxXmm &&
            point.Ymm >= bounds.MinYmm &&
            point.Ymm <= bounds.MaxYmm &&
            point.Zmm >= bounds.MinZmm &&
            point.Zmm <= bounds.MaxZmm;
    }

    private static decimal PinArea(decimal diameterMm)
    {
        var radius = diameterMm / 2m;

        return 3.1415926535897932384626433833m * radius * radius;
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