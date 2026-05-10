namespace PicoMoldForge.Generator;

public sealed record GeneratorRunManifest(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string ProjectName,
    string ConfigPath,
    string OutputDirectory,
    string FinalReportPath,
    bool CleanOutput,
    bool UsedOutputOverride,
    string? OutputOverridePath,
    IReadOnlyList<GeneratorRunManifestArtifact> Artifacts,
    IReadOnlyList<string> Warnings);