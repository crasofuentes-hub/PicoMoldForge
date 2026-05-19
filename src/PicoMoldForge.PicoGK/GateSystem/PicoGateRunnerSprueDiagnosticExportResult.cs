namespace PicoMoldForge.PicoGK.GateSystem;

public sealed record PicoGateRunnerSprueDiagnosticExportResult(
    string OutputPath,
    long OutputFileSizeBytes,
    int SegmentCount,
    int DiagnosticTriangleCount,
    int DiagnosticVertexCount,
    IReadOnlyList<string> Warnings);