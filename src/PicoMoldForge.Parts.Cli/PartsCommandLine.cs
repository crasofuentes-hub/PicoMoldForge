using System.Globalization;
using System.Text;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Meshing;
using PicoMoldForge.Parts.Pipeline;
using PicoMoldForge.Parts.PicoGK;
using PicoMoldForge.Parts.Recipes;
using PicoMoldForge.Parts.Templates;

namespace PicoMoldForge.Parts.Cli;

public sealed class UsageException : Exception
{
    public UsageException(string message)
        : base(message)
    {
    }
}

/// <summary>Command line entry point. Exit codes: 0 PASS, 1 checks failed, 2 usage or recipe error, 3 unexpected error.</summary>
public static class PartsCommandLine
{
    public const string DefaultOutput = "parts-output";
    public const int DefaultPort = 8765;

    public static string HelpText => string.Join(Environment.NewLine, new[]
    {
        "PicoMoldForge Parts - piezas mecanicas 3D (CAD) desde recetas, ejes ISO 841 (X Y Z lineales; A B C giran sobre X Y Z).",
        "",
        "Uso: PicoMoldForge.Parts.Cli <comando> [opciones]",
        "",
        "Comandos:",
        "  demo         Genera y verifica las piezas de ejemplo (engrane recto, helicoidal, piston, monoblock, brida).",
        "  generate     Genera y verifica una pieza desde una receta: --recipe <archivo.json> o --sample <nombre>.",
        "  view         Abre el visor 'modo malla' de una carpeta de salida en el navegador (http://127.0.0.1).",
        "  picogk-view  Abre la pieza en el visor nativo de PicoGK (Windows x64 / macOS arm64).",
        "  samples      Lista las recetas de ejemplo; --export <carpeta> las copia para editarlas.",
        "  selftest     Prueba interna rapida del motor.",
        "",
        "Opciones:",
        "  --output <carpeta>        Carpeta de salida (por defecto: parts-output).",
        "  --backend <modo>          reference | picogk | both (por defecto: reference). both compara PicoGK con la referencia.",
        "  --resolution <mm>         Resolucion de malla para todas las piezas (sustituye la de la receta).",
        "  --quick                   Resolucion x2 (mas rapido, menos detalle).",
        "  --voxel <mm>              Tamano de voxel para PicoGK (por defecto: automatico).",
        "  --only <id,id>            demo: solo esas piezas.",
        "  --clean-output            Borra lo que generaron corridas anteriores en esa carpeta (solo archivos propios).",
        "  --port <n>                view: puerto local (por defecto: 8765; 0 = libre).",
        "  --no-browser              view: no abrir el navegador.",
        "",
        "Salidas por pieza: <id>.stl (mm), part-report.json, preview.bin, features.json, lattice.bin, recipe.json.",
        "En la raiz: index.html (visor), parts.json, summary.txt y RunManifest.json (SHA-256 de cada archivo).",
        "La carpeta de salida debe ser nueva, estar vacia o ser una salida anterior de PicoMoldForge Parts.",
        "",
        "Fuera de alcance: trayectorias de herramienta / G-code (CAM) y exportacion STEP.",
        "Codigos de salida: 0 PASS, 1 alguna verificacion fallo, 2 uso o receta invalida, 3 error inesperado."
    });

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return Dispatch(args, output, error);
        }
        catch (UsageException ex)
        {
            error.WriteLine("ERROR: " + ex.Message);
            error.WriteLine("Usa --help para ver los comandos.");
            return 2;
        }
        catch (RecipeException ex)
        {
            error.WriteLine("ERROR: " + ex.Message);
            return 2;
        }
        catch (PartDefinitionException ex)
        {
            error.WriteLine("ERROR: " + ex.Message);
            return 2;
        }
        catch (FileNotFoundException ex)
        {
            error.WriteLine("ERROR: " + ex.Message + (ex.FileName is null ? string.Empty : " (" + ex.FileName + ")"));
            return 2;
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Refusing unsafe output directory", StringComparison.Ordinal) || ex.Message.StartsWith("Output directory", StringComparison.Ordinal))
        {
            error.WriteLine("ERROR: " + ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            error.WriteLine("ERROR inesperado: " + ex);
            return 3;
        }
    }

    private static int Dispatch(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            output.WriteLine(HelpText);
            return 0;
        }

        if (args[0] is "--version")
        {
            output.WriteLine("PicoMoldForge.Parts " + (typeof(PartsCommandLine).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"));
            return 0;
        }

        var command = args[0];
        var options = Options.Parse(args.Skip(1).ToArray());

        return command switch
        {
            "demo" => Demo(options, output),
            "generate" => Generate(options, output),
            "view" => View(options, output, error),
            "picogk-view" => PicoGkView(options, output),
            "samples" => Samples(options, output),
            "selftest" => SelfTest(options, output),
            _ => throw new UsageException($"Comando desconocido '{command}'.")
        };
    }

    private static int Demo(Options options, TextWriter output)
    {
        options.AllowOnly("output", "backend", "resolution", "quick", "voxel", "only", "clean-output");
        var only = options.Value("only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var samples = EmbeddedAssets.SampleRecipes();

        if (only is not null)
        {
            var unknown = only.Where(id => samples.All(s => s.Name != id)).ToArray();

            if (unknown.Length > 0)
            {
                throw new UsageException("Pieza de ejemplo desconocida: " + string.Join(", ", unknown) + ". Disponibles: " + string.Join(", ", samples.Select(s => s.Name)));
            }
        }

        var jobs = samples
            .Where(s => only is null || only.Contains(s.Name))
            .Select(s => new PartJob(RecipeLoader.Parse(s.Json, s.Name + ".json"), s.Name + ".json", s.Json))
            .ToArray();

        return RunJobs("demo", jobs, options, output);
    }

    private static int Generate(Options options, TextWriter output)
    {
        options.AllowOnly("recipe", "sample", "output", "backend", "resolution", "quick", "voxel", "clean-output");
        var job = LoadJob(options);
        return RunJobs("generate " + job.RecipeName, new[] { job }, options, output);
    }

    private static int RunJobs(string command, IReadOnlyList<PartJob> jobs, Options options, TextWriter output)
    {
        var backend = ParseBackend(options.Value("backend"));
        var resolution = options.Double("resolution");
        var voxel = options.Double("voxel");
        var quick = options.Flag("quick");

        if (resolution is not null && quick)
        {
            throw new UsageException("Usa --resolution o --quick, no ambos.");
        }

        if (resolution is not null && !(resolution >= 0.02 && resolution <= 10))
        {
            throw new UsageException("--resolution debe estar entre 0.02 y 10 mm.");
        }

        if (voxel is not null && !(voxel >= 0.05 && voxel <= 10))
        {
            throw new UsageException("--voxel debe estar entre 0.05 y 10 mm.");
        }

        var runner = new PartsRunner(output);
        var outputRoot = options.Value("output") ?? DefaultOutput;

        if (quick)
        {
            // --quick doubles each part's own resolution.
            jobs = jobs.Select(job => job with { ResolutionOverrideMm = job.Part.Mesh.ResolutionMm * 2 }).ToArray();
            command += " --quick";
        }

        return runner.Run(command, jobs, outputRoot, backend, resolution, voxel, options.Flag("clean-output")).ExitCode;
    }

    private static int View(Options options, TextWriter output, TextWriter error)
    {
        options.AllowOnly("output", "port", "no-browser");
        var root = Path.GetFullPath(options.Value("output") ?? DefaultOutput);
        var port = options.Int("port") ?? DefaultPort;

        if (port is < 0 or > 65535)
        {
            throw new UsageException("--port debe estar entre 0 y 65535.");
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var server = ViewerServer.StartAsync(root, port).GetAwaiter().GetResult();

        try
        {
            output.WriteLine("Visor 'modo malla': " + server.Address);
            output.WriteLine("Carpeta: " + root);
            output.WriteLine("Para cerrar el visor presiona Ctrl+C en esta ventana.");

            if (!options.Flag("no-browser") && !ViewerServer.TryOpenBrowser(server.Address, out var problem))
            {
                error.WriteLine("No se pudo abrir el navegador (" + problem + "). Abre esta direccion a mano: " + server.Address);
            }

            try
            {
                server.WaitForShutdownAsync(cancellation.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }
        finally
        {
            server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        output.WriteLine("Visor cerrado.");
        return 0;
    }

    private static int PicoGkView(Options options, TextWriter output)
    {
        options.AllowOnly("recipe", "sample", "voxel");
        var job = LoadJob(options);
        var voxel = options.Double("voxel") ?? PicoGkPartRenderer.SuggestVoxelSize(job.Part);

        if (!PicoGkRuntimeAvailability.IsAvailable)
        {
            output.WriteLine("PicoGK no esta disponible en esta maquina: " + PicoGkRuntimeAvailability.Reason);
            return 1;
        }

        var log = Path.Combine(Path.GetTempPath(), "PicoMoldForge", "parts-picogk-view.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        output.WriteLine(FormattableString.Invariant($"Abriendo {job.Part.Title} en el visor de PicoGK (voxeles de {voxel} mm). Cierra la ventana para terminar."));
        PicoGkViewer.Show(job.Part, voxel, log);
        return 0;
    }

    private static int Samples(Options options, TextWriter output)
    {
        options.AllowOnly("export");
        var samples = EmbeddedAssets.SampleRecipes();
        var export = options.Value("export");

        foreach (var (name, json) in samples)
        {
            output.WriteLine(name);

            if (export is not null)
            {
                Directory.CreateDirectory(export);
                File.WriteAllText(Path.Combine(export, name + ".json"), json, new UTF8Encoding(false));
            }
        }

        if (export is not null)
        {
            output.WriteLine("Recetas copiadas en: " + Path.GetFullPath(export));
        }

        return 0;
    }

    private static int SelfTest(Options options, TextWriter output)
    {
        options.AllowOnly();
        var part = GearTemplate.Build(
            "selftest-gear",
            null,
            new GearParameters { Module = 1, Teeth = 12, FaceWidth = 4, BoreDiameter = 3, KeywayWidth = 0 },
            new MeshSettings(0.25, true, 0.02),
            helical: false);
        var mesh = SdfMesher.Mesh(part.Sdf, new MeshingOptions(part.Mesh.ResolutionMm));
        var report = MeshAnalysis.Analyze(mesh);
        var passed = report.Watertight && report.Genus == 1 && report.Components == 1;
        output.WriteLine(FormattableString.Invariant($"selftest: {mesh.TriangleCount} triangulos, cerrada={report.Closed}, manifold={report.Manifold}, orientada={report.ConsistentlyOriented}, genero={report.Genus}"));
        output.WriteLine(passed ? "SELFTEST: PASS" : "SELFTEST: FAIL");
        return passed ? 0 : 1;
    }

    private static PartJob LoadJob(Options options)
    {
        var recipe = options.Value("recipe");
        var sample = options.Value("sample");

        if ((recipe is null) == (sample is null))
        {
            throw new UsageException("Indica una receta con --recipe <archivo.json> o --sample <nombre>.");
        }

        if (recipe is not null)
        {
            var path = Path.GetFullPath(recipe);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("No se encontro la receta.", path);
            }

            var json = File.ReadAllText(path);
            return new PartJob(RecipeLoader.Parse(json, Path.GetFileName(path)), Path.GetFileName(path), json);
        }

        var match = EmbeddedAssets.SampleRecipes().FirstOrDefault(s => s.Name == sample);

        if (match.Json is null)
        {
            throw new UsageException($"Receta de ejemplo desconocida '{sample}'. Disponibles: " + string.Join(", ", EmbeddedAssets.SampleRecipes().Select(s => s.Name)));
        }

        return new PartJob(RecipeLoader.Parse(match.Json, match.Name + ".json"), match.Name + ".json", match.Json);
    }

    private static Backend ParseBackend(string? text) => text switch
    {
        null or "reference" => Backend.Reference,
        "picogk" => Backend.PicoGk,
        "both" => Backend.Both,
        _ => throw new UsageException("--backend debe ser reference, picogk o both.")
    };

    private sealed class Options
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

        public static Options Parse(string[] args)
        {
            var options = new Options();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length == 2)
                {
                    throw new UsageException($"Argumento inesperado '{arg}'.");
                }

                var name = arg[2..];

                if (options._values.ContainsKey(name))
                {
                    throw new UsageException($"La opcion --{name} esta repetida.");
                }

                if (IsFlag(name))
                {
                    options._values[name] = null;
                    continue;
                }

                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new UsageException($"La opcion --{name} necesita un valor.");
                }

                options._values[name] = args[++i];
            }

            return options;
        }

        public void AllowOnly(params string[] names)
        {
            var unknown = _values.Keys.Where(k => !names.Contains(k, StringComparer.Ordinal)).ToArray();

            if (unknown.Length > 0)
            {
                throw new UsageException("Opcion no valida para este comando: --" + string.Join(", --", unknown));
            }
        }

        public string? Value(string name) => _values.TryGetValue(name, out var value) ? value : null;

        public bool Flag(string name) => _values.ContainsKey(name);

        public double? Double(string name)
        {
            var text = Value(name);

            if (text is null)
            {
                return null;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            {
                throw new UsageException($"--{name} necesita un numero (usa punto decimal, por ejemplo 0.2).");
            }

            return value;
        }

        public int? Int(string name)
        {
            var text = Value(name);

            if (text is null)
            {
                return null;
            }

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new UsageException($"--{name} necesita un numero entero.");
            }

            return value;
        }

        private static bool IsFlag(string name) => name is "quick" or "clean-output" or "no-browser";
    }
}
