namespace PicoMoldForge.Core.Exports;

public sealed record ExportArtifact(
    ExportArtifactKind Kind,
    string Path,
    string Description,
    bool IsRequired,
    long? FileSizeBytes = null)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Kind == ExportArtifactKind.Unknown)
        {
            errors.Add("Artifact kind must be specified.");
        }

        if (string.IsNullOrWhiteSpace(Path))
        {
            errors.Add("Artifact path is required.");
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            errors.Add("Artifact description is required.");
        }

        if (FileSizeBytes.HasValue && FileSizeBytes.Value < 0)
        {
            errors.Add("Artifact file size cannot be negative.");
        }

        return errors;
    }
}