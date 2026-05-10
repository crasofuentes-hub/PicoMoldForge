using PicoMoldForge.Core.Analysis;

namespace PicoMoldForge.Core.Parting;

public sealed record PartingPlaneResult(
    PartingPlaneMode Mode,
    PartingAxis Axis,
    OpeningDirection3 OpeningDirection,
    float PlaneOffsetMm,
    string Method,
    IReadOnlyList<string> Warnings);