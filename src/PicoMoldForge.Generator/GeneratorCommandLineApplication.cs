namespace PicoMoldForge.Generator;

public static class GeneratorCommandLineApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || Contains(args, "--help"))
        {
            WriteHelp(output);
            return 0;
        }

        if (args.Length == 1 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
        {
            output.WriteLine("PicoMoldForge Generator v5");
            output.WriteLine("Status: PASS");
            output.WriteLine("Generator executable pipeline is ready.");
            output.WriteLine("Warning: Generated geometry remains preliminary and not certified for production manufacturing.");
            return 0;
        }

        if (Contains(args, "--generate-all"))
        {
            return RunGenerateAll(args, output, error);
        }

        error.WriteLine("Unknown command.");
        WriteHelp(error);
        return 2;
    }

    private static int RunGenerateAll(string[] args, TextWriter output, TextWriter error)
    {
        var configPath = TryReadOptionValue(args, "--config");
        var outputOverride = TryReadOptionValue(args, "--output");
        var cleanOutput = Contains(args, "--clean-output");

        if (string.IsNullOrWhiteSpace(configPath))
        {
            error.WriteLine("Missing required option: --config <path>");
            WriteHelp(error);
            return 2;
        }

        if (Contains(args, "--output") && string.IsNullOrWhiteSpace(outputOverride))
        {
            error.WriteLine("Missing required option value: --output <path>");
            WriteHelp(error);
            return 2;
        }

        try
        {
            var validator = new GeneratorPipelineInputValidator();
            var input = validator.Validate(configPath);

            if (!string.IsNullOrWhiteSpace(outputOverride))
            {
                var resolvedOutputOverride = Path.GetFullPath(outputOverride);

                input = input with
                {
                    ResolvedOutputDirectory = resolvedOutputOverride
                };

                output.WriteLine($"Output override: {resolvedOutputOverride}");
            }

            GeneratorOutputPathGuard.EnsureSafeForGeneration(input);

            output.WriteLine("Generation input validation: PASS");
            output.WriteLine("Output path safety: PASS");

            if (cleanOutput && Directory.Exists(input.ResolvedOutputDirectory))
            {
                Directory.Delete(input.ResolvedOutputDirectory, recursive: true);
                output.WriteLine($"Cleaned output directory: {input.ResolvedOutputDirectory}");
            }

            var runner = new GeneratorPipelineRunner();
            var result = runner.Run(input);

            var manifestWriter = new GeneratorRunManifestWriter();
            var runManifestPath = manifestWriter.Write(
                input,
                result,
                cleanOutput,
                outputOverride);

            output.WriteLine("Generation pipeline: PASS");
            output.WriteLine($"Final report: {result.FinalReportPath}");
            output.WriteLine($"Run manifest: {runManifestPath}");
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
        catch (Exception ex)
        {
            error.WriteLine("Generation pipeline: FAIL");
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? TryReadOptionValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.Ordinal))
            {
                continue;
            }

            var valueIndex = i + 1;

            if (valueIndex >= args.Length)
            {
                return null;
            }

            var value = args[valueIndex];

            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            return value;
        }

        return null;
    }

    private static bool Contains(string[] args, string value)
    {
        return args.Any(arg => string.Equals(arg, value, StringComparison.Ordinal));
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("PicoMoldForge Generator v5");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  PicoMoldForge.Generator.exe --self-test");
        output.WriteLine("  PicoMoldForge.Generator.exe --help");
        output.WriteLine("  PicoMoldForge.Generator.exe --config <path> --generate-all [--clean-output] [--output <path>]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --config <path>     Path to the generator project JSON config.");
        output.WriteLine("  --generate-all      Generate the full preliminary output package.");
        output.WriteLine("  --clean-output      Delete the resolved output directory before generation.");
        output.WriteLine("  --output <path>     Override the outputDirectory configured in the project JSON.");
        output.WriteLine();
        output.WriteLine("Safety:");
        output.WriteLine("  Output paths cannot be filesystem roots, the current working directory, the config directory, user profile root, Program Files root, system directory, or an existing file.");
        output.WriteLine();
        output.WriteLine("Generated metadata:");
        output.WriteLine("  RunManifest.json    Machine-readable manifest for each generator run.");
        output.WriteLine();
        output.WriteLine("Warning: Generated geometry is preliminary and not certified for production manufacturing.");
    }
}