namespace PicoMoldForge.Generator;

public sealed record GeneratorLatticeConfig(
    string RegionName,
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
            errors.Add("lattice.regionName is required.");
        }

        if (MaxXmm <= MinXmm)
        {
            errors.Add("lattice X size must be greater than zero.");
        }

        if (MaxYmm <= MinYmm)
        {
            errors.Add("lattice Y size must be greater than zero.");
        }

        if (MaxZmm <= MinZmm)
        {
            errors.Add("lattice Z size must be greater than zero.");
        }

        if (CellSizeMm <= 0)
        {
            errors.Add("lattice.cellSizeMm must be greater than zero.");
        }

        if (BeamRadiusMm <= 0)
        {
            errors.Add("lattice.beamRadiusMm must be greater than zero.");
        }

        if (TargetRelativeDensity <= 0 || TargetRelativeDensity > 1)
        {
            errors.Add("lattice.targetRelativeDensity must be greater than zero and less than or equal to one.");
        }

        return errors;
    }
}