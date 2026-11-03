namespace PicoMoldForge.Core.Engineering.Separation;

/// <summary>
/// Whether the part, measured on its mesh, sits inside the mold block with the required steel margin.
/// <paramref name="MinimumMarginMm"/> is the smallest distance from the part bounds to a block face (negative when
/// the part sticks out) and <paramref name="MinimumMarginSide"/> the block face where it occurs (-X, +X, -Y, +Y,
/// -Z, +Z; the first one in that order on ties).
/// </summary>
public sealed record MoldBlockFitSummary(
    MoldSeparationBounds PartBounds,
    MoldSeparationBounds MoldBlockBounds,
    decimal RequiredMarginMm,
    decimal MinimumMarginMm,
    string MinimumMarginSide,
    bool IsPartInsideBlock,
    bool MeetsRequiredMargin);

public static class MoldBlockFitChecker
{
    public static MoldBlockFitSummary Check(
        MoldSeparationBounds moldBlockBounds,
        MoldSeparationBounds partBounds,
        decimal requiredMarginMm)
    {
        ArgumentNullException.ThrowIfNull(moldBlockBounds);
        ArgumentNullException.ThrowIfNull(partBounds);

        if (requiredMarginMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredMarginMm), "Required margin cannot be negative.");
        }

        var margins = new (string Side, decimal Margin)[]
        {
            ("-X", partBounds.MinXmm - moldBlockBounds.MinXmm),
            ("+X", moldBlockBounds.MaxXmm - partBounds.MaxXmm),
            ("-Y", partBounds.MinYmm - moldBlockBounds.MinYmm),
            ("+Y", moldBlockBounds.MaxYmm - partBounds.MaxYmm),
            ("-Z", partBounds.MinZmm - moldBlockBounds.MinZmm),
            ("+Z", moldBlockBounds.MaxZmm - partBounds.MaxZmm)
        };

        var smallest = margins[0];

        foreach (var margin in margins)
        {
            if (margin.Margin < smallest.Margin)
            {
                smallest = margin;
            }
        }

        // Strictly inside: a part touching a block face would leave the cavity open there.
        var inside = smallest.Margin > 0m;

        return new MoldBlockFitSummary(
            PartBounds: partBounds,
            MoldBlockBounds: moldBlockBounds,
            RequiredMarginMm: requiredMarginMm,
            MinimumMarginMm: smallest.Margin,
            MinimumMarginSide: smallest.Side,
            IsPartInsideBlock: inside,
            MeetsRequiredMargin: inside && smallest.Margin >= requiredMarginMm);
    }
}
