namespace PicoMoldForge.Generator;

public sealed record GeneratorDfamConfig(
    decimal MinimumWallThicknessMm,
    decimal RecommendedMinimumWallThicknessMm,
    bool UsesPreliminaryGeometry)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (MinimumWallThicknessMm <= 0)
        {
            errors.Add("dfam.minimumWallThicknessMm must be greater than zero.");
        }

        if (RecommendedMinimumWallThicknessMm <= 0)
        {
            errors.Add("dfam.recommendedMinimumWallThicknessMm must be greater than zero.");
        }

        if (MinimumWallThicknessMm < RecommendedMinimumWallThicknessMm)
        {
            errors.Add("dfam.minimumWallThicknessMm should be greater than or equal to dfam.recommendedMinimumWallThicknessMm.");
        }

        return errors;
    }
}