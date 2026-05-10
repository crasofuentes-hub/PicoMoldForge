namespace PicoMoldForge.PicoGK.Cooling;

public sealed record PicoCoolingDiagnosticExportResult(
    string OutputPath,
    long OutputFileSizeBytes,
    int SegmentCount,
    int DiagnosticTriangleCount,
    int DiagnosticVertexCount,
    IReadOnlyList<string> Warnings);