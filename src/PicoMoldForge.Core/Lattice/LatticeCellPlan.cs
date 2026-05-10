namespace PicoMoldForge.Core.Lattice;

public sealed record LatticeCellPlan(
    bool IsSuccessful,
    string RegionName,
    int XNodeCount,
    int YNodeCount,
    int ZNodeCount,
    IReadOnlyList<LatticeBeamSegment> Beams,
    IReadOnlyList<string> Warnings);