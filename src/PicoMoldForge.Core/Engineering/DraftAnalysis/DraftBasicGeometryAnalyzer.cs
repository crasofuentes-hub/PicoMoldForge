using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.DraftAnalysis;

public sealed class DraftBasicGeometryAnalyzer
{
    public const string RulePackVersion = "picomoldforge.draft-basic-geometry.v1";

    public DraftBasicGeometryAnalysisResult Analyze(DraftBasicGeometryAnalysisInput analysisInput)
    {
        ArgumentNullException.ThrowIfNull(analysisInput);

        ValidateInput(analysisInput);

        var pull = Normalize(
            analysisInput.PullDirectionX,
            analysisInput.PullDirectionY,
            analysisInput.PullDirectionZ,
            "Pull direction");

        var faceResults = analysisInput.Faces
            .Select(face => AnalyzeFace(face, pull, analysisInput.MinimumRequiredDraftDeg))
            .ToArray();

        var summary = Summarize(faceResults);
        var ruleResult = BuildRuleResult(faceResults, summary, analysisInput.MinimumRequiredDraftDeg);

        return new DraftBasicGeometryAnalysisResult(
            Faces: faceResults,
            Summary: summary,
            RuleResult: ruleResult);
    }

    private static DraftFaceAnalysisResult AnalyzeFace(
        DraftFaceSample face,
        UnitVector pull,
        decimal minimumRequiredDraftDeg)
    {
        if (string.IsNullOrWhiteSpace(face.FaceId))
        {
            return new DraftFaceAnalysisResult(
                FaceId: "missing",
                Classification: DraftFaceClassification.InvalidNormal,
                DraftAngleDeg: 0m,
                SurfaceAreaMm2: Math.Max(0m, face.SurfaceAreaMm2),
                IsCosmeticCritical: face.IsCosmeticCritical,
                IsCriticalToQuality: face.IsCriticalToQuality);
        }

        if (face.SurfaceAreaMm2 < 0m)
        {
            return new DraftFaceAnalysisResult(
                FaceId: face.FaceId,
                Classification: DraftFaceClassification.InvalidNormal,
                DraftAngleDeg: 0m,
                SurfaceAreaMm2: 0m,
                IsCosmeticCritical: face.IsCosmeticCritical,
                IsCriticalToQuality: face.IsCriticalToQuality);
        }

        if (!TryNormalize(face.NormalX, face.NormalY, face.NormalZ, out var normal))
        {
            return new DraftFaceAnalysisResult(
                FaceId: face.FaceId,
                Classification: DraftFaceClassification.InvalidNormal,
                DraftAngleDeg: 0m,
                SurfaceAreaMm2: face.SurfaceAreaMm2,
                IsCosmeticCritical: face.IsCosmeticCritical,
                IsCriticalToQuality: face.IsCriticalToQuality);
        }

        var signedDot = Dot(normal, pull);
        var draftAngleDeg = ToDegrees((decimal)Math.Asin((double)Math.Min(1m, Math.Abs(signedDot))));

        var classification = Classify(
            signedDot,
            draftAngleDeg,
            minimumRequiredDraftDeg);

        return new DraftFaceAnalysisResult(
            FaceId: face.FaceId,
            Classification: classification,
            DraftAngleDeg: Math.Round(draftAngleDeg, 4),
            SurfaceAreaMm2: face.SurfaceAreaMm2,
            IsCosmeticCritical: face.IsCosmeticCritical,
            IsCriticalToQuality: face.IsCriticalToQuality);
    }

    private static DraftFaceClassification Classify(
        decimal signedDot,
        decimal draftAngleDeg,
        decimal minimumRequiredDraftDeg)
    {
        if (signedDot < -0.0001m)
        {
            return DraftFaceClassification.NegativeDraft;
        }

        if (draftAngleDeg <= 0.10m)
        {
            return DraftFaceClassification.ZeroDraft;
        }

        if (draftAngleDeg < minimumRequiredDraftDeg)
        {
            return DraftFaceClassification.LowDraft;
        }

        return DraftFaceClassification.PositiveDraft;
    }

    private static DraftBasicGeometryAnalysisSummary Summarize(
        IReadOnlyList<DraftFaceAnalysisResult> faceResults)
    {
        var riskyArea = faceResults
            .Where(face => face.Classification is
                DraftFaceClassification.LowDraft or
                DraftFaceClassification.ZeroDraft or
                DraftFaceClassification.NegativeDraft or
                DraftFaceClassification.InvalidNormal)
            .Sum(face => face.SurfaceAreaMm2);

        var validDrafts = faceResults
            .Where(face => face.Classification != DraftFaceClassification.InvalidNormal)
            .Select(face => face.DraftAngleDeg)
            .ToArray();

        var minimumObservedDraft = validDrafts.Length == 0
            ? 0m
            : validDrafts.Min();

        return new DraftBasicGeometryAnalysisSummary(
            FaceCount: faceResults.Count,
            PositiveDraftCount: faceResults.Count(face => face.Classification == DraftFaceClassification.PositiveDraft),
            LowDraftCount: faceResults.Count(face => face.Classification == DraftFaceClassification.LowDraft),
            ZeroDraftCount: faceResults.Count(face => face.Classification == DraftFaceClassification.ZeroDraft),
            NegativeDraftCount: faceResults.Count(face => face.Classification == DraftFaceClassification.NegativeDraft),
            InvalidNormalCount: faceResults.Count(face => face.Classification == DraftFaceClassification.InvalidNormal),
            RiskySurfaceAreaMm2: riskyArea,
            MinimumObservedDraftDeg: minimumObservedDraft);
    }

    private static EngineeringRuleResult BuildRuleResult(
        IReadOnlyList<DraftFaceAnalysisResult> faceResults,
        DraftBasicGeometryAnalysisSummary summary,
        decimal minimumRequiredDraftDeg)
    {
        var issues = new List<EngineeringIssue>();

        if (summary.FaceCount == 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "draft.geometry.faces.missing",
                category: "DraftGeometry",
                message: "No face samples were provided for draft analysis.",
                correctiveAction: "Provide mesh face normals before reporting geometry draft quality.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Mesh",
                material: null));

            return new EngineeringRuleResult(
                RulePackVersion: RulePackVersion,
                Category: "DraftGeometry",
                Issues: issues);
        }

        foreach (var face in faceResults)
        {
            if (face.Classification == DraftFaceClassification.InvalidNormal)
            {
                issues.Add(EngineeringIssueFactory.Fail(
                    ruleId: $"draft.geometry.{NormalizeRuleId(face.FaceId)}.invalid-normal",
                    category: "DraftGeometry",
                    message: "Face has an invalid normal or invalid surface area.",
                    correctiveAction: "Repair mesh normals and provide valid surface area before draft analysis.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: face.FaceId,
                    material: null,
                    actualValue: face.DraftAngleDeg,
                    requiredValue: minimumRequiredDraftDeg,
                    recommendedValue: minimumRequiredDraftDeg,
                    unit: "deg"));
            }
            else if (face.Classification == DraftFaceClassification.NegativeDraft)
            {
                issues.Add(EngineeringIssueFactory.Fail(
                    ruleId: $"draft.geometry.{NormalizeRuleId(face.FaceId)}.negative",
                    category: "DraftGeometry",
                    message: "Face normal indicates negative draft relative to the pull direction.",
                    correctiveAction: "Change parting direction, add side action, or revise geometry before treating the mold as functional.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: face.FaceId,
                    material: null,
                    actualValue: face.DraftAngleDeg,
                    requiredValue: minimumRequiredDraftDeg,
                    recommendedValue: minimumRequiredDraftDeg,
                    unit: "deg"));
            }
            else if (face.Classification == DraftFaceClassification.ZeroDraft)
            {
                issues.Add(EngineeringIssueFactory.Warning(
                    ruleId: $"draft.geometry.{NormalizeRuleId(face.FaceId)}.zero",
                    category: "DraftGeometry",
                    message: "Face appears to have near-zero draft relative to the pull direction.",
                    correctiveAction: "Add draft or document mold-engineering approval for zero-draft release.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: face.FaceId,
                    material: null,
                    actualValue: face.DraftAngleDeg,
                    requiredValue: minimumRequiredDraftDeg,
                    recommendedValue: minimumRequiredDraftDeg,
                    unit: "deg",
                    requiresEngineerReview: face.IsCosmeticCritical || face.IsCriticalToQuality));
            }
            else if (face.Classification == DraftFaceClassification.LowDraft)
            {
                issues.Add(EngineeringIssueFactory.Warning(
                    ruleId: $"draft.geometry.{NormalizeRuleId(face.FaceId)}.low",
                    category: "DraftGeometry",
                    message: "Face draft is below the configured minimum.",
                    correctiveAction: "Increase draft angle or document an engineer-approved exception.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: face.FaceId,
                    material: null,
                    actualValue: face.DraftAngleDeg,
                    requiredValue: minimumRequiredDraftDeg,
                    recommendedValue: minimumRequiredDraftDeg,
                    unit: "deg",
                    requiresEngineerReview: face.IsCosmeticCritical || face.IsCriticalToQuality));
            }
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "draft.geometry.pass",
                category: "DraftGeometry",
                message: "All sampled faces satisfy the preliminary draft geometry rule.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "Mesh",
                material: null,
                actualValue: summary.MinimumObservedDraftDeg,
                requiredValue: minimumRequiredDraftDeg,
                recommendedValue: minimumRequiredDraftDeg,
                unit: "deg"));
        }

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "DraftGeometry",
            Issues: issues);
    }

    private static UnitVector Normalize(
        decimal x,
        decimal y,
        decimal z,
        string argumentName)
    {
        if (!TryNormalize(x, y, z, out var vector))
        {
            throw new ArgumentOutOfRangeException(argumentName, "Vector magnitude must be greater than zero.");
        }

        return vector;
    }

    private static bool TryNormalize(
        decimal x,
        decimal y,
        decimal z,
        out UnitVector vector)
    {
        var magnitude = (decimal)Math.Sqrt((double)((x * x) + (y * y) + (z * z)));

        if (magnitude <= 0m)
        {
            vector = new UnitVector(0m, 0m, 0m);
            return false;
        }

        vector = new UnitVector(
            X: x / magnitude,
            Y: y / magnitude,
            Z: z / magnitude);

        return true;
    }

    private static decimal Dot(UnitVector a, UnitVector b)
    {
        return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
    }

    private static decimal ToDegrees(decimal radians)
    {
        return radians * 180m / 3.1415926535897932384626433833m;
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

    private sealed record UnitVector(decimal X, decimal Y, decimal Z);
}