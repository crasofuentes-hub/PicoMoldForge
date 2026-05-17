using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.WallThickness;

public sealed record VoxelWallThicknessAnalysisResult(
    IReadOnlyList<VoxelWallThicknessSampleResult> Samples,
    VoxelWallThicknessAnalysisSummary Summary,
    EngineeringRuleResult RuleResult);