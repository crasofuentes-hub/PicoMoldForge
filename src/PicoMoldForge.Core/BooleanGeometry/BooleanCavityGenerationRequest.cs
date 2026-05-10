namespace PicoMoldForge.Core.BooleanGeometry;

public sealed record BooleanCavityGenerationRequest(
    string SourcePath,
    string OutputDirectory,
    MoldBlockBounds BlockBounds,
    float VoxelSizeMm = 1.0f)
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

        if (VoxelSizeMm <= 0)
        {
            errors.Add("VoxelSizeMm must be greater than zero.");
        }

        foreach (var blockError in BlockBounds.Validate())
        {
            errors.Add($"BlockBounds: {blockError}");
        }

        return errors;
    }
}