namespace PicoMoldForge.Core.Exports;

public enum ExportArtifactKind
{
    Unknown = 0,
    DiagnosticMesh = 1,
    Cavity = 2,
    Core = 3,
    CoolingDiagnostic = 4,
    LatticeDiagnostic = 5,
    MoldSystemDiagnostic = 6,
    FinalReport = 7,
    BooleanCavity = 8,
    BooleanCoreSide = 9,
    BooleanCavitySide = 10,
    Other = 99
}