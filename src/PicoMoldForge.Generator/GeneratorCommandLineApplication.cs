using PicoMoldForge.Core.Configuration;

namespace PicoMoldForge.Generator;

public static class GeneratorCommandLineApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || HasArg(args, "--help") || HasArg(args, "-h"))
        {
            WriteHelp(output);
            return 0;
        }

        if (args.Length == 1 && HasArg(args, "--self-test"))
        {
            output.WriteLine("PicoMoldForge Generator v5");
            output.WriteLine("Status: PASS");
            output.WriteLine("Generator executable pipeline is ready.");
            output.WriteLine("Warning: Generated geometry remains preliminary and not certified for production manufacturing.");
            return 0;
        }

        if (HasArg(args, "--generate-all"))
        {
            return RunGenerateAll(args, output, error);
        }

        error.WriteLine("Unknown command.");
        error.WriteLine("Run with --help for usage.");
        return 2;
    }

    private static int RunGenerateAll(string[] args, TextWriter output, TextWriter error)
    {
        var configPath = GetOptionValue(args, "--config");

        if (string.IsNullOrWhiteSpace(configPath))
        {
            error.WriteLine("Missing required option: --config <path>");
            return 2;
        }

        try
        {
            var validator = new GeneratorPipelineInputValidator();
            var input = validator.Validate(configPath);

            output.WriteLine("PicoMoldForge Generator v5");
            output.WriteLine("Generation input validation: PASS");
            output.WriteLine($"Project: {input.Config.ProjectName}");
            output.WriteLine($"Config: {input.ConfigPath}");
            output.WriteLine($"Input: {input.ResolvedInputPath}");
            output.WriteLine($"Output: {input.ResolvedOutputDirectory}");

            var runner = new GeneratorPipelineRunner();
            var result = runner.Run(input);

            output.WriteLine("Generation pipeline: PASS");
            output.WriteLine($"Final report: {result.FinalReportPath}");
            output.WriteLine($"Artifacts generated: {result.GeneratedArtifacts.Count}");

            foreach (var artifact in result.GeneratedArtifacts)
            {
                output.WriteLine($"Artifact: {artifact}");
            }

            foreach (var warning in result.Warnings)
            {
                output.WriteLine($"Warning: {warning}");
            }

            return 0;
        }
        catch (Exception ex) when (
            ex is ArgumentException ||
            ex is FileNotFoundException ||
            ex is InvalidOperationException ||
            ex is ConfigLoadException)
        {
            error.WriteLine("Generation pipeline: FAIL");
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("PicoMoldForge Generator v5");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  PicoMoldForge.Generator --self-test");
        output.WriteLine("  PicoMoldForge.Generator --help");
        output.WriteLine("  PicoMoldForge.Generator --config <path> --generate-all");
        output.WriteLine();
        output.WriteLine("Current phase:");
        output.WriteLine("  Phase 11C generates the full preliminary output package.");
        output.WriteLine("  Generated geometry remains preliminary and not certified for production manufacturing.");
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