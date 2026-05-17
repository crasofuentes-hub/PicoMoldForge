namespace PicoMoldForge.Core.Engineering.DraftAnalysis;

public sealed record DraftBasicGeometryAnalysisInput(
    decimal PullDirectionX,
    decimal PullDirectionY,
    decimal PullDirectionZ,
    decimal MinimumRequiredDraftDeg,
    IReadOnlyList<DraftFaceSample> Faces);