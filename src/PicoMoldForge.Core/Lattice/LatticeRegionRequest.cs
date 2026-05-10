namespace PicoMoldForge.Core.Lattice;

public sealed record LatticeRegionRequest(
    string RegionName,
    string OutputDirectory,
    decimal MinXmm,
    decimal MinYmm,
    decimal MinZmm,
    decimal MaxXmm,
    decimal MaxYmm,
    decimal MaxZmm,
    decimal CellSizeMm,
    decimal BeamRadiusMm,
    decimal TargetRelativeDensity)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(RegionName))
        {
            errors.Add("RegionName is required.");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            errors.Add("OutputDirectory is required.");
        }

        if (MaxXmm <= MinXmm)
        {
            errors.Add("Region size X must be greater than zero.");
        }

        if (MaxYmm <= MinYmm)
        {
            errors.Add("Region size Y must be greater than zero.");
        }

        if (MaxZmm <= MinZmm)
        {
            errors.Add("Region size Z must be greater than zero.");
        }

        if (CellSizeMm <= 0)
        {
            errors.Add("CellSizeMm must be greater than zero.");
        }

        if (BeamRadiusMm <= 0)
        {
            errors.Add("BeamRadiusMm must be greater than zero.");
        }

        if (CellSizeMm > 0 && BeamRadiusMm >= CellSizeMm / 2.0m)
        {
            errors.Add("BeamRadiusMm must be less than half of CellSizeMm.");
        }

        if (TargetRelativeDensity <= 0 || TargetRelativeDensity > 1)
        {
            errors.Add("TargetRelativeDensity must be greater than zero and less than or equal to one.");
        }

        return errors;
    }
}