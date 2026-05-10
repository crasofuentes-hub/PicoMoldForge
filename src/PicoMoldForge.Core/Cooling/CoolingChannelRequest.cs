namespace PicoMoldForge.Core.Cooling;

public sealed record CoolingChannelRequest(
    string OutputDirectory,
    decimal PartSizeXmm,
    decimal PartSizeYmm,
    decimal PartSizeZmm,
    decimal ChannelDiameterMm,
    decimal ChannelSpacingMm,
    decimal MinimumClearanceMm,
    int ChannelCount)
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

        if (ChannelDiameterMm <= 0)
        {
            errors.Add("ChannelDiameterMm must be greater than zero.");
        }

        if (ChannelSpacingMm <= ChannelDiameterMm)
        {
            errors.Add("ChannelSpacingMm must be greater than ChannelDiameterMm.");
        }

        if (MinimumClearanceMm <= ChannelDiameterMm / 2.0m)
        {
            errors.Add("MinimumClearanceMm must be greater than half of ChannelDiameterMm.");
        }

        if (ChannelCount <= 0)
        {
            errors.Add("ChannelCount must be greater than zero.");
        }

        if (ChannelCount > 64)
        {
            errors.Add("ChannelCount must not exceed 64 for the preliminary planner.");
        }

        return errors;
    }
}