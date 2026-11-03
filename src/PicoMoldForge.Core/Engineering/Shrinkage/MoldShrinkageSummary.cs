using PicoMoldForge.Core.Engineering.Separation;

namespace PicoMoldForge.Core.Engineering.Shrinkage;

/// <summary>
/// How the cavity was sized: the shrinkage rate and where it came from, the scale factor 1 + rate applied about the
/// part centre, the resulting cavity bounds and the split height in the mold.
/// </summary>
public sealed record MoldShrinkageSummary(
    decimal ShrinkageRate,
    decimal ScaleFactor,
    string Source,
    string? ResinName,
    MoldSeparationBounds CavityBounds,
    decimal MoldPartingOffsetMm);
