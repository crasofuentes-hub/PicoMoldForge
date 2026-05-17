namespace PicoMoldForge.Core.Engineering.WallThickness;

public sealed record VoxelWallThicknessAnalysisInput(
    decimal MinimumThicknessMm,
    decimal MaximumThicknessMm,
    decimal AbruptChangeWarningRatio,
    IReadOnlyList<VoxelWallThicknessSample> Samples);