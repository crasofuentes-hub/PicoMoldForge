namespace PicoMoldForge.Generator;

public sealed record GeneratorCoolingConfig(
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

        if (PartSizeXmm <= 0)
        {
            errors.Add("cooling.partSizeXmm must be greater than zero.");
        }

        if (PartSizeYmm <= 0)
        {
            errors.Add("cooling.partSizeYmm must be greater than zero.");
        }

        if (PartSizeZmm <= 0)
        {
            errors.Add("cooling.partSizeZmm must be greater than zero.");
        }

        if (ChannelDiameterMm <= 0)
        {
            errors.Add("cooling.channelDiameterMm must be greater than zero.");
        }

        if (ChannelSpacingMm <= 0)
        {
            errors.Add("cooling.channelSpacingMm must be greater than zero.");
        }

        if (MinimumClearanceMm < 0)
        {
            errors.Add("cooling.minimumClearanceMm cannot be negative.");
        }

        if (ChannelCount <= 0)
        {
            errors.Add("cooling.channelCount must be greater than zero.");
        }

        return errors;
    }
}