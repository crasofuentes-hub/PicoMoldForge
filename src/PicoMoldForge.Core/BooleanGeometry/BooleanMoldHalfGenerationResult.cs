namespace PicoMoldForge.Core.BooleanGeometry;

public sealed record BooleanMoldHalfGenerationResult(
    MoldHalfKind Kind,
    string OutputPath,
    long OutputFileSizeBytes,
    MoldBlockBounds Bounds,
    float RemainingVolumeCubicMm,
    int DiagnosticTriangleCount,
    int DiagnosticVertexCount);