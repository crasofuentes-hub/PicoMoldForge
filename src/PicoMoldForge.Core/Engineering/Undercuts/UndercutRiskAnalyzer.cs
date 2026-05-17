using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Undercuts;

public sealed class UndercutRiskAnalyzer
{
    public const string RulePackVersion = "picomoldforge.undercut-risk.v1";

    public UndercutRiskAnalysisResult Analyze(UndercutRiskAnalysisInput analysisInput)
    {
        ArgumentNullException.ThrowIfNull(analysisInput);

        ValidateInput(analysisInput);

        var pull = Normalize(
            analysisInput.PullDirectionX,
            analysisInput.PullDirectionY,
            analysisInput.PullDirectionZ,
            "Pull direction");

        var faceResults = analysisInput.Faces
            .Select(face => AnalyzeFace(face, pull, analysisInput))
            .ToArray();

        var summary = Summarize(faceResults);
        var ruleResult = BuildRuleResult(faceResults, summary, analysisInput);

        return new UndercutRiskAnalysisResult(faceResults, summary, ruleResult);
    }

    private static void ValidateInput(UndercutRiskAnalysisInput analysisInput)
    {
        ArgumentNullException.ThrowIfNull(analysisInput.Faces);

        if (analysisInput.LowPullDotThreshold < 0m || analysisInput.LowPullDotThreshold > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(analysisInput.LowPullDotThreshold), "LowPullDotThreshold must be between 0 and 1.");
        }

        if (analysisInput.SideActionDotThreshold < -1m || analysisInput.SideActionDotThreshold > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(analysisInput.SideActionDotThreshold), "SideActionDotThreshold must be between -1 and 1.");
        }

        if (analysisInput.CriticalTrapDepthMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(analysisInput.CriticalTrapDepthMm), "CriticalTrapDepthMm cannot be negative.");
        }
    }

    private static UndercutFaceAnalysisResult AnalyzeFace(
        UndercutFaceSample face,
        UnitVector pull,
        UndercutRiskAnalysisInput analysisInput)
    {
        if (string.IsNullOrWhiteSpace(face.FaceId) ||
            face.SurfaceAreaMm2 < 0m ||
            face.TrapDepthMm < 0m ||
            !TryNormalize(face.NormalX, face.NormalY, face.NormalZ, out var normal))
        {
            return new UndercutFaceAnalysisResult(
                FaceId: string.IsNullOrWhiteSpace(face.FaceId) ? "missing" : face.FaceId,
                Classification: UndercutFaceClassification.InvalidNormal,
                PullDot: 0m,
                SurfaceAreaMm2: Math.Max(0m, face.SurfaceAreaMm2),
                TrapDepthMm: Math.Max(0m, face.TrapDepthMm),
                IsCriticalToQuality: face.IsCriticalToQuality,
                IsCosmeticCritical: face.IsCosmeticCritical);
        }

        var pullDot = Math.Round(Dot(normal, pull), 6);

        var classification =
            pullDot < analysisInput.SideActionDotThreshold
                ? UndercutFaceClassification.Undercut
                : pullDot < 0m
                    ? UndercutFaceClassification.SideActionCandidate
                    : pullDot < analysisInput.LowPullDotThreshold
                        ? UndercutFaceClassification.LowPullClearance
                        : UndercutFaceClassification.ClearPull;

        return new UndercutFaceAnalysisResult(
            FaceId: face.FaceId,
            Classification: classification,
            PullDot: pullDot,
            SurfaceAreaMm2: face.SurfaceAreaMm2,
            TrapDepthMm: face.TrapDepthMm,
            IsCriticalToQuality: face.IsCriticalToQuality,
            IsCosmeticCritical: face.IsCosmeticCritical);
    }

    private static UndercutRiskAnalysisSummary Summarize(
        IReadOnlyList<UndercutFaceAnalysisResult> faceResults)
    {
        var riskyArea = faceResults
            .Where(face => face.Classification != UndercutFaceClassification.ClearPull)
            .Sum(face => face.SurfaceAreaMm2);

        var maximumTrapDepth = faceResults.Count == 0
            ? 0m
            : faceResults.Max(face => face.TrapDepthMm);

        return new UndercutRiskAnalysisSummary(
            FaceCount: faceResults.Count,
            ClearPullCount: faceResults.Count(face => face.Classification == UndercutFaceClassification.ClearPull),
            LowPullClearanceCount: faceResults.Count(face => face.Classification == UndercutFaceClassification.LowPullClearance),
            SideActionCandidateCount: faceResults.Count(face => face.Classification == UndercutFaceClassification.SideActionCandidate),
            UndercutCount: faceResults.Count(face => face.Classification == UndercutFaceClassification.Undercut),
            InvalidNormalCount: faceResults.Count(face => face.Classification == UndercutFaceClassification.InvalidNormal),
            RiskySurfaceAreaMm2: riskyArea,
            MaximumTrapDepthMm: maximumTrapDepth);
    }

    private static EngineeringRuleResult BuildRuleResult(
        IReadOnlyList<UndercutFaceAnalysisResult> faceResults,
        UndercutRiskAnalysisSummary summary,
        UndercutRiskAnalysisInput analysisInput)
    {
        var issues = new List<EngineeringIssue>();

        if (summary.FaceCount == 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "undercut.faces.missing",
                category: "UndercutRisk",
                message: "No face samples were provided for undercut analysis.",
                correctiveAction: "Provide mesh face normals before reporting undercut risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Mesh",
                material: null));

            return new EngineeringRuleResult(RulePackVersion, "UndercutRisk", issues);
        }

        foreach (var face in faceResults)
        {
            AddFaceIssue(face, analysisInput, issues);
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "undercut.pass",
                category: "UndercutRisk",
                message: "All sampled faces satisfy the preliminary undercut risk rule.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Mesh",
                material: null,
                actualValue: summary.RiskySurfaceAreaMm2,
                requiredValue: 0m,
                recommendedValue: 0m,
                unit: "mm2"));
        }

        return new EngineeringRuleResult(RulePackVersion, "UndercutRisk", issues);
    }

    private static void AddFaceIssue(
        UndercutFaceAnalysisResult face,
        UndercutRiskAnalysisInput analysisInput,
        List<EngineeringIssue> issues)
    {
        var ruleIdBase = $"undercut.{NormalizeRuleId(face.FaceId)}";

        if (face.Classification == UndercutFaceClassification.InvalidNormal)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.invalid",
                category: "UndercutRisk",
                message: "Face has invalid normal, surface area, trap depth, or region id.",
                correctiveAction: "Repair mesh sampling before using undercut analysis.",
                sourceRulePackVersion: RulePackVersion,
                featureType: face.FaceId,
                material: null));
            return;
        }

        if (face.Classification == UndercutFaceClassification.Undercut)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.undercut",
                category: "UndercutRisk",
                message: "Face indicates undercut risk against the configured pull direction.",
                correctiveAction: "Change parting direction, add side action/lifter, or revise geometry.",
                sourceRulePackVersion: RulePackVersion,
                featureType: face.FaceId,
                material: null,
                actualValue: face.PullDot,
                requiredValue: analysisInput.SideActionDotThreshold,
                recommendedValue: analysisInput.LowPullDotThreshold,
                unit: "dot"));
            return;
        }

        if (face.Classification == UndercutFaceClassification.SideActionCandidate)
        {
            var severe = face.IsCriticalToQuality ||
                face.IsCosmeticCritical ||
                face.TrapDepthMm >= analysisInput.CriticalTrapDepthMm;

            if (severe)
            {
                issues.Add(EngineeringIssueFactory.Fail(
                    ruleId: $"{ruleIdBase}.side-action-critical",
                    category: "UndercutRisk",
                    message: "Face is a side-action candidate with critical, cosmetic, or deep trap risk.",
                    correctiveAction: "Add side action/lifter strategy or revise the geometry before treating the mold as functional.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: face.FaceId,
                    material: null,
                    actualValue: face.TrapDepthMm,
                    requiredValue: analysisInput.CriticalTrapDepthMm,
                    recommendedValue: 0m,
                    unit: "mm"));
                return;
            }

            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.side-action",
                category: "UndercutRisk",
                message: "Face may require side action or mold-engineering review.",
                correctiveAction: "Review pull direction, parting line, side action, or lifter need.",
                sourceRulePackVersion: RulePackVersion,
                featureType: face.FaceId,
                material: null,
                actualValue: face.PullDot,
                requiredValue: 0m,
                recommendedValue: analysisInput.LowPullDotThreshold,
                unit: "dot",
                requiresEngineerReview: true));
            return;
        }

        if (face.Classification == UndercutFaceClassification.LowPullClearance)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.low-clearance",
                category: "UndercutRisk",
                message: "Face has low pull-direction clearance.",
                correctiveAction: "Review draft, surface finish, pull direction, and release risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: face.FaceId,
                material: null,
                actualValue: face.PullDot,
                requiredValue: analysisInput.LowPullDotThreshold,
                recommendedValue: 1m,
                unit: "dot",
                requiresEngineerReview: face.IsCriticalToQuality || face.IsCosmeticCritical));
        }
    }

    private static UnitVector Normalize(decimal x, decimal y, decimal z, string argumentName)
    {
        if (!TryNormalize(x, y, z, out var vector))
        {
            throw new ArgumentOutOfRangeException(argumentName, "Vector magnitude must be greater than zero.");
        }

        return vector;
    }

    private static bool TryNormalize(decimal x, decimal y, decimal z, out UnitVector vector)
    {
        var magnitude = (decimal)Math.Sqrt((double)((x * x) + (y * y) + (z * z)));

        if (magnitude <= 0m)
        {
            vector = new UnitVector(0m, 0m, 0m);
            return false;
        }

        vector = new UnitVector(x / magnitude, y / magnitude, z / magnitude);
        return true;
    }

    private static decimal Dot(UnitVector a, UnitVector b)
    {
        return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
    }

    private static string NormalizeRuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "missing";
        }

        return value.Trim().Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
    }

    private sealed record UnitVector(decimal X, decimal Y, decimal Z);
}