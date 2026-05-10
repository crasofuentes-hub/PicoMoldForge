namespace PicoMoldForge.Generator;

public sealed record GeneratorMoldSystemConfig(
    decimal PartSizeXmm,
    decimal PartSizeYmm,
    decimal PartSizeZmm,
    decimal MoldMarginMm,
    decimal EjectorPinDiameterMm,
    int EjectorPinCount,
    decimal VentWidthMm,
    decimal VentDepthMm,
    decimal InsertClearanceMm)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (PartSizeXmm <= 0)
        {
            errors.Add("moldSystem.partSizeXmm must be greater than zero.");
        }

        if (PartSizeYmm <= 0)
        {
            errors.Add("moldSystem.partSizeYmm must be greater than zero.");
        }

        if (PartSizeZmm <= 0)
        {
            errors.Add("moldSystem.partSizeZmm must be greater than zero.");
        }

        if (MoldMarginMm < 0)
        {
            errors.Add("moldSystem.moldMarginMm cannot be negative.");
        }

        if (EjectorPinDiameterMm <= 0)
        {
            errors.Add("moldSystem.ejectorPinDiameterMm must be greater than zero.");
        }

        if (EjectorPinCount <= 0)
        {
            errors.Add("moldSystem.ejectorPinCount must be greater than zero.");
        }

        if (VentWidthMm <= 0)
        {
            errors.Add("moldSystem.ventWidthMm must be greater than zero.");
        }

        if (VentDepthMm <= 0)
        {
            errors.Add("moldSystem.ventDepthMm must be greater than zero.");
        }

        if (InsertClearanceMm < 0)
        {
            errors.Add("moldSystem.insertClearanceMm cannot be negative.");
        }

        return errors;
    }
}