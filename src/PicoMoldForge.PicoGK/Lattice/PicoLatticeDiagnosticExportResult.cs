namespace PicoMoldForge.PicoGK.Lattice;

public sealed record PicoLatticeDiagnosticExportResult(
    string OutputPath,
    long OutputFileSizeBytes,
    string RegionName,
    int BeamCount,
    int DiagnosticTriangleCount,
    int DiagnosticVertexCount,
    IReadOnlyList<string> Warnings);