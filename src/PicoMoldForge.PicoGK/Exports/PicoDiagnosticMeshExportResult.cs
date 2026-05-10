namespace PicoMoldForge.PicoGK.Exports;

public sealed record PicoDiagnosticMeshExportResult(
    string SourcePath,
    string OutputPath,
    long OutputFileSizeBytes,
    int DiagnosticTriangleCount,
    int DiagnosticVertexCount);