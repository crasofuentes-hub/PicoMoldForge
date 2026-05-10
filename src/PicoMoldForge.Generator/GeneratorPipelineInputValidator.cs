using PicoMoldForge.Core.Configuration;
using PicoMoldForge.Core.Input;

namespace PicoMoldForge.Generator;

public sealed class GeneratorPipelineInputValidator
{
    public GeneratorPipelineInput Validate(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Config path is required.", nameof(configPath));
        }

        var resolvedConfigPath = Path.GetFullPath(configPath);

        if (!File.Exists(resolvedConfigPath))
        {
            throw new FileNotFoundException("Config file was not found.", resolvedConfigPath);
        }

        var config = MoldProjectConfigLoader.LoadFromFile(resolvedConfigPath);
        var validationErrors = config.Validate();

        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Config validation failed: " + string.Join(" ", validationErrors));
        }

        var configDirectory = Path.GetDirectoryName(resolvedConfigPath) ?? Directory.GetCurrentDirectory();

        var resolvedInputPath = Path.IsPathRooted(config.InputPath)
            ? Path.GetFullPath(config.InputPath)
            : Path.GetFullPath(Path.Combine(configDirectory, config.InputPath));

        var resolvedOutputDirectory = Path.IsPathRooted(config.OutputDirectory)
            ? Path.GetFullPath(config.OutputDirectory)
            : Path.GetFullPath(Path.Combine(configDirectory, config.OutputDirectory));

        var loader = new PartInputLoader();
        var inputResult = loader.Load(config with
        {
            InputPath = resolvedInputPath,
            OutputDirectory = resolvedOutputDirectory
        });

        if (!inputResult.IsSuccessful)
        {
            throw new InvalidOperationException(
                "Input validation failed: " + inputResult.Error);
        }

        if (!BinaryStlProbe.IsBinaryStl(resolvedInputPath))
        {
            throw new InvalidOperationException(
                "PicoMoldForge Generator requires binary STL input for the PicoGK pipeline. ASCII STL is not supported by the current generator path.");
        }

        return new GeneratorPipelineInput(
            config,
            resolvedConfigPath,
            resolvedInputPath,
            resolvedOutputDirectory);
    }
}