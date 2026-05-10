namespace PicoMoldForge.Core.MoldSystems;

public sealed record MoldSystemRequest(
    string OutputDirectory,
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

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            errors.Add("OutputDirectory is required.");
        }

        if (PartSizeXmm <= 0)
        {
            errors.Add("PartSizeXmm must be greater than zero.");
        }

        if (PartSizeYmm <= 0)
        {
            errors.Add("PartSizeYmm must be greater than zero.");
        }

        if (PartSizeZmm <= 0)
        {
            errors.Add("PartSizeZmm must be greater than zero.");
        }

        if (MoldMarginMm <= 0)
        {
            errors.Add("MoldMarginMm must be greater than zero.");
        }

        if (EjectorPinDiameterMm <= 0)
        {
            errors.Add("EjectorPinDiameterMm must be greater than zero.");
        }

        if (EjectorPinCount <= 0)
        {
            errors.Add("EjectorPinCount must be greater than zero.");
        }

        if (EjectorPinCount > 64)
        {
            errors.Add("EjectorPinCount must not exceed 64 for the preliminary planner.");
        }

        if (VentWidthMm <= 0)
        {
            errors.Add("VentWidthMm must be greater than zero.");
        }

        if (VentDepthMm <= 0)
        {
            errors.Add("VentDepthMm must be greater than zero.");
        }

        if (InsertClearanceMm < 0)
        {
            errors.Add("InsertClearanceMm cannot be negative.");
        }

        if (MoldMarginMm > 0 && InsertClearanceMm >= MoldMarginMm)
        {
            errors.Add("InsertClearanceMm must be less than MoldMarginMm.");
        }

        return errors;
    }
}