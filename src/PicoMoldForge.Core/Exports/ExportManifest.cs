namespace PicoMoldForge.Core.Exports;

public sealed record ExportManifest(
    IReadOnlyList<ExportArtifact> Artifacts,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Artifacts.Count == 0)
        {
            errors.Add("Export manifest must contain at least one artifact.");
        }

        foreach (var artifact in Artifacts)
        {
            foreach (var artifactError in artifact.Validate())
            {
                errors.Add($"{artifact.Kind}: {artifactError}");
            }
        }

        return errors;
    }
}