using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.WallThickness;

public sealed class VoxelWallThicknessAnalyzer
{
    public const string RulePackVersion = "picomoldforge.voxel-wall-thickness.v1";

    public VoxelWallThicknessAnalysisResult Analyze(VoxelWallThicknessAnalysisInput analysisInput)
    {
        ArgumentNullException.ThrowIfNull(analysisInput);

        ValidateInput(analysisInput);

        var sampleResults = analysisInput.Samples
            .Select(sample => AnalyzeSample(sample, analysisInput))
            .ToArray();

        var summary = Summarize(sampleResults);
        var ruleResult = BuildRuleResult(sampleResults, summary, analysisInput);

        return new VoxelWallThicknessAnalysisResult(
            Samples: sampleResults,
            Summary: summary,
            RuleResult: ruleResult);
    }

    private static void ValidateInput(VoxelWallThicknessAnalysisInput analysisInput)
    {
        ArgumentNullException.ThrowIfNull(analysisInput.Samples);

        if (analysisInput.MinimumThicknessMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(analysisInput.MinimumThicknessMm),
                "Minimum thickness must be greater than zero.");
        }

        if (analysisInput.MaximumThicknessMm < analysisInput.MinimumThicknessMm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(analysisInput.MaximumThicknessMm),
                "Maximum thickness must be greater than or equal to minimum thickness.");
        }

        if (analysisInput.AbruptChangeWarningRatio < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(analysisInput.AbruptChangeWarningRatio),
                "Abrupt change warning ratio cannot be negative.");
        }
    }

    private static VoxelWallThicknessSampleResult AnalyzeSample(
        VoxelWallThicknessSample sample,
        VoxelWallThicknessAnalysisInput analysisInput)
    {
        if (string.IsNullOrWhiteSpace(sample.RegionId) ||
            sample.ThicknessMm <= 0m ||
            sample.NominalThicknessMm <= 0m ||
            sample.SurfaceAreaMm2 < 0m)
        {
            return new VoxelWallThicknessSampleResult(
                RegionId: string.IsNullOrWhiteSpace(sample.RegionId) ? "missing" : sample.RegionId,
                Classification: VoxelWallThicknessClassification.Invalid,
                ThicknessMm: Math.Max(0m, sample.ThicknessMm),
                NominalThicknessMm: Math.Max(0m, sample.NominalThicknessMm),
                ThicknessDeltaRatio: 0m,
                SurfaceAreaMm2: Math.Max(0m, sample.SurfaceAreaMm2),
                IsCosmeticCritical: sample.IsCosmeticCritical,
                IsCriticalToQuality: sample.IsCriticalToQuality);
        }

        var deltaRatio = Math.Abs(sample.ThicknessMm - sample.NominalThicknessMm) / sample.NominalThicknessMm;

        var classification =
            sample.ThicknessMm < analysisInput.MinimumThicknessMm
                ? VoxelWallThicknessClassification.Thin
                : sample.ThicknessMm > analysisInput.MaximumThicknessMm
                    ? VoxelWallThicknessClassification.Thick
                    : deltaRatio > analysisInput.AbruptChangeWarningRatio
                        ? VoxelWallThicknessClassification.AbruptChange
                        : VoxelWallThicknessClassification.Nominal;

        return new VoxelWallThicknessSampleResult(
            RegionId: sample.RegionId,
            Classification: classification,
            ThicknessMm: sample.ThicknessMm,
            NominalThicknessMm: sample.NominalThicknessMm,
            ThicknessDeltaRatio: Math.Round(deltaRatio, 6),
            SurfaceAreaMm2: sample.SurfaceAreaMm2,
            IsCosmeticCritical: sample.IsCosmeticCritical,
            IsCriticalToQuality: sample.IsCriticalToQuality);
    }

    private static VoxelWallThicknessAnalysisSummary Summarize(
        IReadOnlyList<VoxelWallThicknessSampleResult> sampleResults)
    {
        var validThicknesses = sampleResults
            .Where(sample => sample.Classification != VoxelWallThicknessClassification.Invalid)
            .Select(sample => sample.ThicknessMm)
            .ToArray();

        var riskyArea = sampleResults
            .Where(sample => sample.Classification != VoxelWallThicknessClassification.Nominal)
            .Sum(sample => sample.SurfaceAreaMm2);

        return new VoxelWallThicknessAnalysisSummary(
            SampleCount: sampleResults.Count,
            NominalCount: sampleResults.Count(sample => sample.Classification == VoxelWallThicknessClassification.Nominal),
            ThinCount: sampleResults.Count(sample => sample.Classification == VoxelWallThicknessClassification.Thin),
            ThickCount: sampleResults.Count(sample => sample.Classification == VoxelWallThicknessClassification.Thick),
            AbruptChangeCount: sampleResults.Count(sample => sample.Classification == VoxelWallThicknessClassification.AbruptChange),
            InvalidCount: sampleResults.Count(sample => sample.Classification == VoxelWallThicknessClassification.Invalid),
            MinimumObservedThicknessMm: validThicknesses.Length == 0 ? 0m : validThicknesses.Min(),
            MaximumObservedThicknessMm: validThicknesses.Length == 0 ? 0m : validThicknesses.Max(),
            RiskySurfaceAreaMm2: riskyArea);
    }

    private static EngineeringRuleResult BuildRuleResult(
        IReadOnlyList<VoxelWallThicknessSampleResult> sampleResults,
        VoxelWallThicknessAnalysisSummary summary,
        VoxelWallThicknessAnalysisInput analysisInput)
    {
        var issues = new List<EngineeringIssue>();

        if (summary.SampleCount == 0)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: "wall-thickness.voxel.samples.missing",
                category: "VoxelWallThickness",
                message: "No voxel wall-thickness samples were provided.",
                correctiveAction: "Provide sampled wall-thickness regions before reporting wall-thickness quality.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "VoxelWallThickness",
                material: null));

            return new EngineeringRuleResult(
                RulePackVersion: RulePackVersion,
                Category: "VoxelWallThickness",
                Issues: issues);
        }

        foreach (var sample in sampleResults)
        {
            AddSampleIssue(sample, analysisInput, issues);
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "wall-thickness.voxel.pass",
                category: "VoxelWallThickness",
                message: "All sampled wall-thickness regions satisfy the preliminary voxel wall-thickness rule.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "VoxelWallThickness",
                material: null,
                actualValue: summary.MinimumObservedThicknessMm,
                requiredValue: analysisInput.MinimumThicknessMm,
                recommendedValue: analysisInput.MaximumThicknessMm,
                unit: "mm"));
        }

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "VoxelWallThickness",
            Issues: issues);
    }

    private static void AddSampleIssue(
        VoxelWallThicknessSampleResult sample,
        VoxelWallThicknessAnalysisInput analysisInput,
        List<EngineeringIssue> issues)
    {
        var ruleIdBase = $"wall-thickness.voxel.{NormalizeRuleId(sample.RegionId)}";

        if (sample.Classification == VoxelWallThicknessClassification.Invalid)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.invalid",
                category: "VoxelWallThickness",
                message: "Wall-thickness sample contains invalid region id, thickness, nominal thickness, or surface area.",
                correctiveAction: "Repair sample extraction before using wall-thickness analysis.",
                sourceRulePackVersion: RulePackVersion,
                featureType: sample.RegionId,
                material: null,
                actualValue: sample.ThicknessMm,
                requiredValue: analysisInput.MinimumThicknessMm,
                recommendedValue: analysisInput.MaximumThicknessMm,
                unit: "mm"));
            return;
        }

        if (sample.Classification == VoxelWallThicknessClassification.Thin)
        {
            if (sample.IsCriticalToQuality || sample.IsCosmeticCritical)
            {
                issues.Add(EngineeringIssueFactory.Fail(
                    ruleId: $"{ruleIdBase}.thin-critical",
                    category: "VoxelWallThickness",
                    message: "Critical or cosmetic wall-thickness sample is below the configured minimum.",
                    correctiveAction: "Increase wall thickness, revise geometry, or document qualified mold-engineering approval.",
                    sourceRulePackVersion: RulePackVersion,
                    featureType: sample.RegionId,
                    material: null,
                    actualValue: sample.ThicknessMm,
                    requiredValue: analysisInput.MinimumThicknessMm,
                    recommendedValue: analysisInput.MaximumThicknessMm,
                    unit: "mm"));
                return;
            }

            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.thin",
                category: "VoxelWallThickness",
                message: "Wall-thickness sample is below the configured minimum.",
                correctiveAction: "Increase wall thickness or mark the region for engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: sample.RegionId,
                material: null,
                actualValue: sample.ThicknessMm,
                requiredValue: analysisInput.MinimumThicknessMm,
                recommendedValue: analysisInput.MaximumThicknessMm,
                unit: "mm",
                requiresEngineerReview: true));
            return;
        }

        if (sample.Classification == VoxelWallThicknessClassification.Thick)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.thick",
                category: "VoxelWallThickness",
                message: "Wall-thickness sample exceeds the configured maximum.",
                correctiveAction: "Core out, reduce local thickness, or review sink/warp/cooling-time risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: sample.RegionId,
                material: null,
                actualValue: sample.ThicknessMm,
                requiredValue: analysisInput.MaximumThicknessMm,
                recommendedValue: analysisInput.MaximumThicknessMm,
                unit: "mm",
                requiresEngineerReview: sample.IsCriticalToQuality || sample.IsCosmeticCritical));
            return;
        }

        if (sample.Classification == VoxelWallThicknessClassification.AbruptChange)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.abrupt-change",
                category: "VoxelWallThickness",
                message: "Wall-thickness sample has an abrupt local thickness change relative to nominal thickness.",
                correctiveAction: "Add transitions, reduce thickness jump, or review sink/warp stress concentration risk.",
                sourceRulePackVersion: RulePackVersion,
                featureType: sample.RegionId,
                material: null,
                actualValue: sample.ThicknessDeltaRatio,
                requiredValue: analysisInput.AbruptChangeWarningRatio,
                recommendedValue: 0m,
                unit: "ratio",
                requiresEngineerReview: sample.IsCriticalToQuality || sample.IsCosmeticCritical));
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
}