using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.DfAM;

namespace PicoMoldForge.Core.Exports;

public sealed record FinalProjectReport(
    string ProjectName,
    DateTimeOffset GeneratedAtUtc,
    ExportManifest Manifest,
    PartAnalysisReport? PartAnalysis,
    DfAMReport? DfAM,
    BaselineStatus Baseline,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            errors.Add("ProjectName is required.");
        }

        if (GeneratedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add("GeneratedAtUtc must use UTC offset.");
        }

        foreach (var manifestError in Manifest.Validate())
        {
            errors.Add($"Manifest: {manifestError}");
        }

        foreach (var baselineError in Baseline.Validate())
        {
            errors.Add($"Baseline: {baselineError}");
        }

        return errors;
    }
}