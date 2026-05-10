namespace PicoMoldForge.Core.Lattice;

public sealed record LatticeBeamSegment(
    string Id,
    LatticeBeamAxis Axis,
    decimal StartXmm,
    decimal StartYmm,
    decimal StartZmm,
    decimal EndXmm,
    decimal EndYmm,
    decimal EndZmm,
    decimal BeamRadiusMm);