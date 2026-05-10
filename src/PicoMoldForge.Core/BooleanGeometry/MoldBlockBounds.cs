namespace PicoMoldForge.Core.BooleanGeometry;

public sealed record MoldBlockBounds(
    decimal MinXmm,
    decimal MinYmm,
    decimal MinZmm,
    decimal MaxXmm,
    decimal MaxYmm,
    decimal MaxZmm)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (MaxXmm <= MinXmm)
        {
            errors.Add("Block size X must be greater than zero.");
        }

        if (MaxYmm <= MinYmm)
        {
            errors.Add("Block size Y must be greater than zero.");
        }

        if (MaxZmm <= MinZmm)
        {
            errors.Add("Block size Z must be greater than zero.");
        }

        return errors;
    }
}