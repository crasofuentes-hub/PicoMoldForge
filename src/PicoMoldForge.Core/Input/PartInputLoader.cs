using System;
using System.IO;
using PicoMoldForge.Core.Domain;

namespace PicoMoldForge.Core.Input;

public sealed class PartInputLoader
{
    private readonly IPartInputConverter? stepConverter;

    public PartInputLoader(IPartInputConverter? stepConverter = null)
    {
        this.stepConverter = stepConverter;
    }

    public PartInputLoadResult Load(MoldProjectConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.InputPath))
        {
            return PartInputLoadResult.Failure(
                PartInputFormat.Unknown,
                config.InputPath,
                "InputPath is required.");
        }

        var format = DetectFormat(config.InputPath);

        if (format == PartInputFormat.Unknown)
        {
            return PartInputLoadResult.Failure(
                PartInputFormat.Unknown,
                config.InputPath,
                "Unsupported part input format. Supported input formats are STL and STEP with an external converter.");
        }

        if (format == PartInputFormat.Step)
        {
            return LoadStep(config);
        }

        return LoadStl(config.InputPath);
    }

    public static PartInputFormat DetectFormat(string inputPath)
    {
        var extension = Path.GetExtension(inputPath);

        if (extension.Equals(".stl", StringComparison.OrdinalIgnoreCase))
        {
            return PartInputFormat.Stl;
        }

        if (extension.Equals(".step", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".stp", StringComparison.OrdinalIgnoreCase))
        {
            return PartInputFormat.Step;
        }

        return PartInputFormat.Unknown;
    }

    private static PartInputLoadResult LoadStl(string inputPath)
    {
        var resolvedPath = Path.GetFullPath(inputPath);

        if (!File.Exists(resolvedPath))
        {
            return PartInputLoadResult.Failure(
                PartInputFormat.Stl,
                inputPath,
                $"STL input file was not found: {resolvedPath}");
        }

        return PartInputLoadResult.Success(PartInputFormat.Stl, inputPath, resolvedPath);
    }

    private PartInputLoadResult LoadStep(MoldProjectConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.StepConverterPath) || stepConverter is null)
        {
            return PartInputLoadResult.Failure(
                PartInputFormat.Step,
                config.InputPath,
                "STEP input requires a configured external converter. Native STEP import is not enabled.");
        }

        var resolvedPath = Path.GetFullPath(config.InputPath);

        if (!File.Exists(resolvedPath))
        {
            return PartInputLoadResult.Failure(
                PartInputFormat.Step,
                config.InputPath,
                $"STEP input file was not found: {resolvedPath}");
        }

        return stepConverter.ConvertToStl(resolvedPath, config.OutputDirectory);
    }
}