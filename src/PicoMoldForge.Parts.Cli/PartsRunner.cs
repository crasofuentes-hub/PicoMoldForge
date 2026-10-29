using System.Globalization;
using System.Text;
using System.Text.Json;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Pipeline;
using PicoMoldForge.Parts.PicoGK;

namespace PicoMoldForge.Parts.Cli;

public enum Backend
{
    Reference,
    PicoGk,
    Both
}

public sealed record PartJob(PartDefinition Part, string RecipeName, string RecipeJson, double? ResolutionOverrideMm = null);

public sealed record PicoGkCrossCheck(
    string Status,
    string? Reason,
    double? VoxelSizeMm,
    double? PicoGkVolumeMm3,
    double? ReferenceVolumeMm3,
    double? VolumeDifference,
    double? VolumeTolerance,
    double? BoundsDeviationMm,
    double? BoundsTolerance,
    int? Triangles,
    double? Seconds,
    string? Stl);

public sealed record RunOutcome(int ExitCode, string OutputDirectory, IReadOnlyList<(PartReport Report, PicoGkCrossCheck? PicoGk)> Parts);

/// <summary>Generates parts, verifies them, optionally cross-checks them with PicoGK and writes the viewer site.</summary>
public sealed class PartsRunner
{
    public const string IndexFileName = "parts.json";
    public const string ViewerFileName = "index.html";
    public const string SummaryFileName = "summary.txt";
    public const string CrossCheckFileName = "picogk-crosscheck.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly TextWriter _out;

    public PartsRunner(TextWriter output)
    {
        _out = output ?? throw new ArgumentNullException(nameof(output));
    }

    public RunOutcome Run(
        string command,
        IReadOnlyList<PartJob> jobs,
        string outputDirectory,
        Backend backend,
        double? resolutionOverride,
        double? voxelOverride,
        bool cleanOutput)
    {
        var root = OutputPathGuard.EnsureSafe(outputDirectory);
        EnsureEmptyOrOwnOutput(root);

        if (cleanOutput)
        {
            CleanOwnOutput(root);
        }

        Directory.CreateDirectory(root);

        // Marks the folder as a Parts output from the start, so a run that dies half way (native crash,
        // out of memory) leaves a folder the next run can reuse or clean instead of refusing it.
        RunManifestWriter.Write(root, command, Array.Empty<RunManifestPart>(), Array.Empty<string>(), RunManifestWriter.IncompleteStatus);

        var artifacts = new List<string>();
        var results = new List<(PartReport, PicoGkCrossCheck?)>();
        var generator = new PartGenerator(message => _out.WriteLine("  " + message));

        foreach (var job in jobs)
        {
            var partDirectory = Path.Combine(root, job.Part.Id);
            Directory.CreateDirectory(partDirectory);

            var recipeCopy = Path.Combine(partDirectory, "recipe.json");
            File.WriteAllText(recipeCopy, job.RecipeJson, new UTF8Encoding(false));
            artifacts.Add(recipeCopy);

            _out.WriteLine($"== {job.Part.Title} ({job.Part.Id})");
            var result = generator.Generate(job.Part, partDirectory, new PartGenerationOptions(job.ResolutionOverrideMm ?? resolutionOverride));
            artifacts.AddRange(result.ArtifactPaths);

            PicoGkCrossCheck? cross = null;

            if (backend is Backend.PicoGk or Backend.Both)
            {
                cross = CrossCheck(job.Part, result.Report, partDirectory, voxelOverride);
                var crossPath = Path.Combine(partDirectory, CrossCheckFileName);
                File.WriteAllText(crossPath, JsonSerializer.Serialize(cross, Json), new UTF8Encoding(false));
                artifacts.Add(crossPath);

                if (cross.Stl is not null)
                {
                    artifacts.Add(Path.Combine(partDirectory, cross.Stl));
                }

                _out.WriteLine($"  [{job.Part.Id}] PicoGK: {cross.Status}{(cross.Reason is null ? string.Empty : " - " + cross.Reason)}");
            }

            foreach (var check in result.Report.Checks.Where(c => !c.Passed))
            {
                _out.WriteLine($"  FAIL {check.Id}: {check.Description} | esperado {check.Expected} | obtenido {check.Actual}");
            }

            results.Add((result.Report, cross));
        }

        var viewerPath = Path.Combine(root, ViewerFileName);
        File.WriteAllText(viewerPath, EmbeddedAssets.ViewerHtml(), new UTF8Encoding(false));
        artifacts.Add(viewerPath);

        var indexPath = WriteIndex(root);
        artifacts.Add(indexPath);

        var summaryPath = WriteSummary(root, command, results);
        artifacts.Add(summaryPath);

        var manifestParts = results.Select(r => new RunManifestPart(
            r.Item1.Id,
            r.Item1.Title,
            Combine(r.Item1.Status, r.Item2?.Status),
            r.Item2 is null ? "reference" : "reference+picogk")).ToArray();
        RunManifestWriter.Write(root, command, manifestParts, artifacts);

        var allPassed = manifestParts.All(p => p.Status == "PASS");
        _out.WriteLine();
        _out.WriteLine(File.ReadAllText(summaryPath));
        _out.WriteLine(allPassed ? "PARTS: PASS" : "PARTS: FAIL");
        return new RunOutcome(allPassed ? 0 : 1, root, results);
    }

    private static string Combine(string reference, string? picogk) =>
        picogk is null || picogk == "PASS" ? reference : "FAIL";

    private static readonly string[] RootFiles = { ViewerFileName, IndexFileName, SummaryFileName, RunManifestWriter.FileName };

    /// <summary>
    /// Only writes into a new folder, an empty folder or a previous PicoMoldForge Parts output (identified by
    /// its RunManifest.json schema), so user files such as an existing index.html are never overwritten.
    /// </summary>
    public static void EnsureEmptyOrOwnOutput(string root)
    {
        if (!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any())
        {
            return;
        }

        var manifest = Path.Combine(root, RunManifestWriter.FileName);
        var own = false;

        if (File.Exists(manifest))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                own = document.RootElement.TryGetProperty("schemaVersion", out var schema) && schema.GetString() == RunManifestWriter.Schema;
            }
            catch (JsonException)
            {
                own = false;
            }
        }

        if (!own)
        {
            throw new InvalidOperationException("Output directory is not empty and is not a PicoMoldForge Parts output (no " + RunManifestWriter.FileName + " with schema " + RunManifestWriter.Schema + "): " + root + ". Use a new or empty folder.");
        }
    }

    /// <summary>
    /// Deletes what previous runs wrote (root files and the known files inside part folders that hold a parts
    /// report) and nothing else; folders are removed only when they end up empty.
    /// </summary>
    public static void CleanOwnOutput(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(root))
        {
            var reportPath = Path.Combine(directory, PartGenerator.ReportFileName);

            if (!File.Exists(reportPath))
            {
                continue;
            }

            string? id;

            try
            {
                using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
                var element = report.RootElement;

                if (!element.TryGetProperty("schema", out var schema) || schema.GetString() != PartReport.SchemaVersion)
                {
                    continue;
                }

                id = element.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            }
            catch (JsonException)
            {
                continue;
            }

            var names = new List<string>
            {
                "recipe.json",
                PartGenerator.PreviewFileName,
                PartGenerator.FeaturesFileName,
                PartGenerator.LatticeFileName,
                PartGenerator.ReportFileName,
                CrossCheckFileName
            };

            if (!string.IsNullOrEmpty(id) && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                names.Add(id + ".stl");
                names.Add(id + ".picogk.stl");
            }

            foreach (var name in names)
            {
                var file = Path.Combine(directory, name);

                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        foreach (var name in RootFiles)
        {
            var file = Path.Combine(root, name);

            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private PicoGkCrossCheck CrossCheck(PartDefinition part, PartReport reference, string partDirectory, double? voxelOverride)
    {
        if (!PicoGkRuntimeAvailability.IsAvailable)
        {
            return new PicoGkCrossCheck("FAIL", "PicoGK no disponible: " + PicoGkRuntimeAvailability.Reason, null, null, null, null, null, null, null, null, null, null);
        }

        var voxel = voxelOverride ?? PicoGkPartRenderer.SuggestVoxelSize(part);
        var stlName = part.Id + ".picogk.stl";
        _out.WriteLine(FormattableString.Invariant($"  [{part.Id}] PicoGK: voxels de {voxel} mm"));

        try
        {
            var render = PicoGkPartRenderer.Render(part, voxel, Path.Combine(partDirectory, stlName));
            var referenceVolume = reference.RawMesh.VolumeMm3;
            var volumeDifference = Math.Abs(render.VolumeMm3 - referenceVolume) / Math.Max(referenceVolume, 1e-9);
            var volumeTolerance = Math.Max(0.03, 0.25 * reference.RawMesh.AreaMm2 * voxel / Math.Max(referenceVolume, 1e-9));
            var min = reference.PrintMesh.BoundsMin;
            var max = reference.PrintMesh.BoundsMax;
            var boundsDeviation = new[]
            {
                Math.Abs(render.BoundsMin.X - min[0]), Math.Abs(render.BoundsMin.Y - min[1]), Math.Abs(render.BoundsMin.Z - min[2]),
                Math.Abs(render.BoundsMax.X - max[0]), Math.Abs(render.BoundsMax.Y - max[1]), Math.Abs(render.BoundsMax.Z - max[2])
            }.Max();
            var boundsTolerance = 1.5 * voxel;
            var passed = volumeDifference <= volumeTolerance && boundsDeviation <= boundsTolerance && render.Triangles > 0;

            return new PicoGkCrossCheck(
                passed ? "PASS" : "FAIL",
                passed ? null : "PicoGK difiere de la referencia mas de la tolerancia",
                voxel,
                Math.Round(render.VolumeMm3, 3),
                referenceVolume,
                Math.Round(volumeDifference, 5),
                Math.Round(volumeTolerance, 5),
                Math.Round(boundsDeviation, 4),
                boundsTolerance,
                render.Triangles,
                Math.Round(render.Seconds, 2),
                stlName);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new PicoGkCrossCheck("FAIL", ex.GetType().Name + ": " + ex.Message, voxel, null, null, null, null, null, null, null, null, null);
        }
    }

    /// <summary>Index of every part folder in the output root (so repeated runs accumulate in one viewer).</summary>
    public static string WriteIndex(string root)
    {
        var parts = new List<object>();

        // Sample parts first, in demo order (gears, piston, block, flange); any other part after them by name.
        var directories = Directory.GetDirectories(root)
            .OrderBy(d => Array.IndexOf(EmbeddedAssets.DemoOrder, Path.GetFileName(d)) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(d => d, StringComparer.Ordinal);

        foreach (var directory in directories)
        {
            var reportPath = Path.Combine(directory, PartGenerator.ReportFileName);

            if (!File.Exists(reportPath) || !File.Exists(Path.Combine(directory, PartGenerator.PreviewFileName)))
            {
                continue;
            }

            using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
            var r = report.RootElement;
            var id = r.GetProperty("id").GetString() ?? Path.GetFileName(directory);
            var folder = Path.GetFileName(directory);
            string? picogk = null;
            var crossPath = Path.Combine(directory, CrossCheckFileName);

            if (File.Exists(crossPath))
            {
                using var cross = JsonDocument.Parse(File.ReadAllText(crossPath));
                picogk = cross.RootElement.GetProperty("status").GetString();
            }

            parts.Add(new
            {
                id,
                title = r.GetProperty("title").GetString(),
                template = r.GetProperty("template").GetString(),
                status = r.GetProperty("status").GetString(),
                picogk,
                files = new
                {
                    preview = folder + "/" + PartGenerator.PreviewFileName,
                    features = folder + "/" + PartGenerator.FeaturesFileName,
                    lattice = folder + "/" + PartGenerator.LatticeFileName,
                    report = folder + "/" + PartGenerator.ReportFileName,
                    stl = folder + "/" + id + ".stl"
                }
            });
        }

        var index = new
        {
            schema = "picomoldforge.parts-index.v1",
            axisConvention = Axes.AxisConvention.Name,
            parts
        };

        var path = Path.Combine(root, IndexFileName);
        File.WriteAllText(path, JsonSerializer.Serialize(index, Json), new UTF8Encoding(false));
        return path;
    }

    private static string WriteSummary(string root, string command, IReadOnlyList<(PartReport Report, PicoGkCrossCheck? PicoGk)> results)
    {
        var text = new StringBuilder();
        text.AppendLine("PicoMoldForge Parts - resumen");
        text.AppendLine("Comando: " + command);
        text.AppendLine("Salida: " + root);
        text.AppendLine();
        text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-16} {1,-6} {2,-9} {3,10} {4,8} {5,14} {6,-8}", "pieza", "estado", "checks", "triangulos", "genero", "volumen mm3", "PicoGK"));

        foreach (var (report, picogk) in results)
        {
            var passed = report.Checks.Count(c => c.Passed);
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-16} {1,-6} {2,-9} {3,10} {4,8} {5,14:0.###} {6,-8}",
                report.Id,
                report.Status,
                $"{passed}/{report.Checks.Count}",
                report.PrintMesh.Triangles,
                report.PrintMesh.Genus?.ToString(CultureInfo.InvariantCulture) ?? "?",
                report.PrintMesh.VolumeMm3,
                picogk?.Status ?? "-"));
        }

        var path = Path.Combine(root, SummaryFileName);
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        return path;
    }
}
