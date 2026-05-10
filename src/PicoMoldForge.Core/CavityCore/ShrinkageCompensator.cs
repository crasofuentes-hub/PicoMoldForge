namespace PicoMoldForge.Core.CavityCore;

public sealed class ShrinkageCompensator
{
    public ShrinkageCompensationResult CalculateUniformScale(decimal shrinkageRate)
    {
        if (shrinkageRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shrinkageRate), "Shrinkage rate cannot be negative.");
        }

        return new ShrinkageCompensationResult(
            shrinkageRate,
            1.0m + shrinkageRate);
    }

    public decimal ApplyToDimension(decimal dimensionMm, decimal shrinkageRate)
    {
        if (dimensionMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensionMm), "Dimension cannot be negative.");
        }

        var compensation = CalculateUniformScale(shrinkageRate);

        return dimensionMm * compensation.ScaleFactor;
    }
}