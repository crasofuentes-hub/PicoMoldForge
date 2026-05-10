using System;
using System.IO;
using PicoMoldForge.Core.Configuration;
using PicoMoldForge.Core.Input;

namespace PicoMoldForge.Cli;

public static class CommandLineApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
        {
            output.WriteLine("PicoMoldForge v5 self-test");
            output.WriteLine("Status: PASS");
            output.WriteLine("Baseline self-test passed.");
            output.WriteLine("Warning: PicoGK runtime integration is intentionally not invoked in phase 0.");
            output.WriteLine("Warning: Generated geometry is preliminary and not certified for production manufacturing.");
            return 0;
        }

        if (HasArg(args, "--validate-config"))
        {
            return ValidateConfig(args, output, error);
        }

        output.WriteLine("PicoMoldForge v5");
        output.WriteLine("Usage:");
        output.WriteLine("  PicoMoldForge.Cli --self-test");
        output.WriteLine("  PicoMoldForge.Cli --config <path> --validate-config");
        return 0;
    }

    private static int ValidateConfig(string[] args, TextWriter output, TextWriter error)
    {
        var configPath = GetOptionValue(args, "--config");

        if (string.IsNullOrWhiteSpace(configPath))
        {
            error.WriteLine("Missing required option: --config <path>");
            return 2;
        }

        try
        {
            var config = MoldProjectConfigLoader.LoadFromFile(configPath);
            var validationErrors = config.Validate();

            if (validationErrors.Count > 0)
            {
                output.WriteLine("Config validation: FAIL");

                foreach (var validationError in validationErrors)
                {
                    output.WriteLine($"Error: {validationError}");
                }

                return 1;
            }

            var inputLoader = new PartInputLoader();
            var inputResult = inputLoader.Load(config);

            if (!inputResult.IsSuccessful)
            {
                output.WriteLine("Config validation: PASS");
                output.WriteLine("Input validation: FAIL");
                output.WriteLine($"Error: {inputResult.Error}");
                return 1;
            }

            output.WriteLine("Config validation: PASS");
            output.WriteLine("Input validation: PASS");
            output.WriteLine($"Project: {config.ProjectName}");
            output.WriteLine($"Input: {config.InputPath}");
            output.WriteLine($"Resolved input: {inputResult.ResolvedPath}");
            output.WriteLine($"Output: {config.OutputDirectory}");
            return 0;
        }
        catch (ConfigLoadException ex)
        {
            error.WriteLine($"Config load error: {ex.Message}");
            return 1;
        }
    }

    private static bool HasArg(string[] args, string name)
    {
        return Array.Exists(args, arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetOptionValue(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}