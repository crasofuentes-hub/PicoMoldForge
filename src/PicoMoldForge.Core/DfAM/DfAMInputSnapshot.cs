namespace PicoMoldForge.Core.DfAM;

public sealed record DfAMInputSnapshot(
    decimal MinimumWallThicknessMm,
    decimal RecommendedMinimumWallThicknessMm,
    decimal CoolingMinimumClearanceMm,
    decimal CoolingChannelDiameterMm,
    decimal LatticeBeamRadiusMm,
    decimal LatticeCellSizeMm,
    decimal EjectorPinDiameterMm,
    bool UsesPreliminaryGeometry)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (MinimumWallThicknessMm < 0)
        {
            errors.Add("MinimumWallThicknessMm cannot be negative.");
        }

        if (RecommendedMinimumWallThicknessMm <= 0)
        {
            errors.Add("RecommendedMinimumWallThicknessMm must be greater than zero.");
        }

        if (CoolingMinimumClearanceMm < 0)
        {
            errors.Add("CoolingMinimumClearanceMm cannot be negative.");
        }

        if (CoolingChannelDiameterMm <= 0)
        {
            errors.Add("CoolingChannelDiameterMm must be greater than zero.");
        }

        if (LatticeBeamRadiusMm <= 0)
        {
            errors.Add("LatticeBeamRadiusMm must be greater than zero.");
        }

        if (LatticeCellSizeMm <= 0)
        {
            errors.Add("LatticeCellSizeMm must be greater than zero.");
        }

        if (EjectorPinDiameterMm <= 0)
        {
            errors.Add("EjectorPinDiameterMm must be greater than zero.");
        }

        return errors;
    }
}