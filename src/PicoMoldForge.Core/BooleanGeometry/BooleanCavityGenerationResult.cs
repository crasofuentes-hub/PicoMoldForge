namespace PicoMoldForge.Core.BooleanGeometry;

public sealed record BooleanCavityGenerationResult(
    bool IsSuccessful,
    string OutputPath,
    long OutputFileSizeBytes,
    float BlockVolumeCubicMm,
    float CavityVolumeCubicMm,
    int DiagnosticTriangleCount,
    int DiagnosticVertexCount,
    IReadOnlyList<string> Warnings);