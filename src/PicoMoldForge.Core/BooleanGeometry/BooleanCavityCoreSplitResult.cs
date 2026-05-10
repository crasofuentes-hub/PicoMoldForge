using PicoMoldForge.Core.Parting;

namespace PicoMoldForge.Core.BooleanGeometry;

public sealed record BooleanCavityCoreSplitResult(
    bool IsSuccessful,
    PartingAxis SplitAxis,
    decimal PartingPlaneOffsetMm,
    BooleanMoldHalfGenerationResult CoreSide,
    BooleanMoldHalfGenerationResult CavitySide,
    IReadOnlyList<string> Warnings);