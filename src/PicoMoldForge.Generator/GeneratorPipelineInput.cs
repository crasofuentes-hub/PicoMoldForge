using PicoMoldForge.Core.Domain;

namespace PicoMoldForge.Generator;

public sealed record GeneratorPipelineInput(
    MoldProjectConfig Config,
    string ConfigPath,
    string ResolvedInputPath,
    string ResolvedOutputDirectory);