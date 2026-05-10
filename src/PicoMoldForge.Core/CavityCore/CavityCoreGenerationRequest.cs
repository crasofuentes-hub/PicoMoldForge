using PicoMoldForge.Core.Parting;

namespace PicoMoldForge.Core.CavityCore;

public sealed record CavityCoreGenerationRequest(
    string SourcePath,
    string OutputDirectory,
    decimal ShrinkageRate,
    PartingPlaneResult? PartingPlane)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            errors.Add("SourcePath is required.");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            errors.Add("OutputDirectory is required.");
        }

        if (ShrinkageRate < 0)
        {
            errors.Add("ShrinkageRate cannot be negative.");
        }

        if (PartingPlane is null)
        {
            errors.Add("PartingPlane is required.");
        }

        return errors;
    }
}