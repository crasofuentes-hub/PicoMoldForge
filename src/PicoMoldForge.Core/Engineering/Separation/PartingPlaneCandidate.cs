namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record PartingPlaneCandidate(
    PartingAxis Axis,
    decimal OffsetMm,
    string Source = "Auto");