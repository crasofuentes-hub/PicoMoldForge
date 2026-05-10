namespace PicoMoldForge.PicoGK.MoldSystems;

public sealed record PicoMoldSystemDiagnosticExportResult(
    string OutputPath,
    long OutputFileSizeBytes,
    int EjectorPinCount,
    int VentChannelCount,
    int InsertPocketCount,
    int DiagnosticTriangleCount,
    int DiagnosticVertexCount,
    IReadOnlyList<string> Warnings);