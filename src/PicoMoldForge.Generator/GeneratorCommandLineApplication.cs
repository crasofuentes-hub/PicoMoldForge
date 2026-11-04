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

        if (Contains(args, "--mold-stl"))
        {
            return RunMoldStl(args, output, error);
        }

        error.WriteLine("Unknown command.");
        WriteHelp(error);
        return 2;
    }

    /// <summary>
    /// A mold for one part STL: writes the project of <see cref="GeneratorPartMoldProject"/> into the output folder and
    /// generates it into its "mold" subfolder.
    /// </summary>
    private static int RunMoldStl(string[] args, TextWriter output, TextWriter error)
    {
        var partPath = TryReadOptionValue(args, "--mold-stl");
        var outputDirectory = TryReadOptionValue(args, "--output");
        var resin = TryReadOptionValue(args, "--resin");
        var shrinkage = TryReadOptionValue(args, "--resin-shrinkage");
        var voxel = TryReadOptionValue(args, "--voxel");

        if (string.IsNullOrWhiteSpace(partPath) || string.IsNullOrWhiteSpace(outputDirectory))
        {
            error.WriteLine("Missing required option: --mold-stl <part.stl> --output <folder>");
            WriteHelp(error);
            return 2;
        }

        string projectPath;

        try
        {
            var options = new GeneratorPartMoldOptions(
                ResinName: string.Equals(resin, "none", StringComparison.OrdinalIgnoreCase) ? null : resin ?? "ABS",
                ResinShrinkageRate: shrinkage is null ? null : decimal.Parse(shrinkage, System.Globalization.CultureInfo.InvariantCulture),
                VoxelResolutionMm: voxel is null ? 1.0m : decimal.Parse(voxel, System.Globalization.CultureInfo.InvariantCulture));
            var resolvedOutput = Path.GetFullPath(outputDirectory);
            var moldDirectory = Path.Combine(resolvedOutput, GeneratorPartMoldProject.MoldDirectoryName);

            if (Contains(args, "--clean-output") && Directory.Exists(moldDirectory))
            {
                Directory.Delete(moldDirectory, recursive: true);
                output.WriteLine($"Cleaned output directory: {moldDirectory}");
            }

            projectPath = GeneratorPartMoldProject.Write(partPath, resolvedOutput, options);
            output.WriteLine($"Mold project: {projectPath}");
            output.WriteLine($"Mold project settings: {GeneratorPartMoldProject.Describe(projectPath)}");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or FormatException or UnauthorizedAccessException)
        {
            error.WriteLine("Generation pipeline: FAIL");
            error.WriteLine(ex.Message);
            return 1;
        }

        return Generate(projectPath, outputOverride: null, cleanOutput: false, output, error);
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

        return Generate(configPath, outputOverride, cleanOutput, output, error);
    }

    private static int Generate(string configPath, string? outputOverride, bool cleanOutput, TextWriter output, TextWriter error)
    {
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

            if (result.MoldVerification is not null)
            {
                output.WriteLine($"Mold verification: {result.MoldVerification}");
            }

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
        output.WriteLine("  PicoMoldForge.Generator.exe --mold-stl <part.stl> --output <folder> [--resin <name>|none] [--resin-shrinkage <rate>] [--voxel <mm>] [--clean-output]");
        output.WriteLine();
        output.WriteLine("Options:");
        output.WriteLine("  --config <path>     Path to the generator project JSON config.");
        output.WriteLine("  --generate-all      Generate the full preliminary output package.");
        output.WriteLine("  --clean-output      Delete the resolved output directory before generation.");
        output.WriteLine("  --output <path>     Override the outputDirectory configured in the project JSON.");
        output.WriteLine("  --mold-stl <path>   Write a mold project for a part STL into --output (project.json, split by geometry,");
        output.WriteLine("                      block sized for the features) and generate it into its mold subfolder.");
        output.WriteLine("  --resin <name>      Part resin for --mold-stl (default ABS; none = material.shrinkageRate 0.011).");
        output.WriteLine("  --voxel <mm>        Voxel size for --mold-stl (default 1.0).");
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