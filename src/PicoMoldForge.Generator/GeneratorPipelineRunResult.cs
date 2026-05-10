namespace PicoMoldForge.Generator;

public sealed record GeneratorPipelineRunResult(
    string ProjectName,
    string OutputDirectory,
    string FinalReportPath,
    IReadOnlyList<string> GeneratedArtifacts,
    IReadOnlyList<string> Warnings);