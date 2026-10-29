using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Export;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Meshing;
using PicoMoldForge.Parts.Sdf;
using PicoMoldForge.Parts.Verification;

namespace PicoMoldForge.Parts.Pipeline;

/// <param name="SurfaceSamples">Maximum vertices and triangles (each) probed for the geometric surface distance.</param>
public sealed record PartGenerationOptions(
    double? ResolutionOverrideMm = null,
    bool? DecimateOverride = null,
    bool WriteViewerData = true,
    int SurfaceSamples = 50_000);

public sealed record MeshSummary(
    int Vertices,
    int Triangles,
    double[] BoundsMin,
    double[] BoundsMax,
    double VolumeMm3,
    double AreaMm2,
    bool Closed,
    bool Manifold,
    bool ConsistentlyOriented,
    bool OutwardNormals,
    int EulerCharacteristic,
    int Components,
    int? Genus,
    int DegenerateTriangles)
{
    public static MeshSummary From(MeshReport r) => new(
        r.Vertices,
        r.Triangles,
        FeatureExporter.ToArray(r.BoundsMin),
        FeatureExporter.ToArray(r.BoundsMax),
        Math.Round(r.VolumeMm3, 3),
        Math.Round(r.AreaMm2, 3),
        r.Closed,
        r.Manifold,
        r.ConsistentlyOriented,
        r.OutwardNormals,
        r.EulerCharacteristic,
        r.Components,
        r.Genus,
        r.DegenerateTriangles);
}

public sealed record PartReport(
    string Schema,
    string Id,
    string Title,
    string Template,
    string Backend,
    string AxisConvention,
    string OrientationOrder,
    IReadOnlyList<KeyValuePair<string, string>> Parameters,
    MeshSettings Mesh,
    MeshSummary RawMesh,
    MeshSummary PrintMesh,
    MeshSummary? PreviewMesh,
    double RawMaxVertexFieldMm,
    double PrintMaxVertexFieldMm,
    SurfaceDistanceReport SurfaceDistance,
    double VoxelVolumeMm3,
    LatticeInfo? Lattice,
    IReadOnlyList<PartCheckResult> Checks,
    IReadOnlyDictionary<string, double> TimingsSeconds,
    IReadOnlyList<string> Artifacts,
    string Status,
    IReadOnlyList<string> Notes)
{
    public const string SchemaVersion = "picomoldforge.parts-report.v1";

    public bool Passed => Status == "PASS";
}

public sealed record PartGenerationResult(PartReport Report, TriangleMesh PrintMesh, IReadOnlyList<string> ArtifactPaths);

/// <summary>
/// Reference pipeline for one part: mesh the distance field, simplify within tolerance, export STL and viewer
/// data, then verify topology, dimensions and volume with checks that do not trust the mesher.
/// </summary>
public sealed class PartGenerator
{
    public const string ReportFileName = "part-report.json";
    public const string PreviewFileName = "preview.bin";
    public const string FeaturesFileName = "features.json";
    public const string LatticeFileName = "lattice.bin";

    private readonly Action<string> _log;

    public PartGenerator(Action<string>? log = null)
    {
        _log = log ?? (_ => { });
    }

    public PartGenerationResult Generate(PartDefinition part, string partOutputDirectory, PartGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentException.ThrowIfNullOrWhiteSpace(partOutputDirectory);

        options ??= new PartGenerationOptions();
        var mesh = ResolveMesh(part.Mesh, options);
        var errors = mesh.Validate();

        if (errors.Count > 0)
        {
            throw new ArgumentException("Invalid mesh settings: " + string.Join(" ", errors));
        }

        Directory.CreateDirectory(partOutputDirectory);
        var timings = new Dictionary<string, double>();
        var artifacts = new List<string>();
        var watch = Stopwatch.StartNew();
        var sdf = part.Sdf;
        timings["build"] = Lap(watch);

        _log(FormattableString.Invariant($"[{part.Id}] meshing at {mesh.ResolutionMm} mm"));
        var raw = SdfMesher.Mesh(sdf, new MeshingOptions(mesh.ResolutionMm), cancellationToken);
        var rawReport = MeshAnalysis.Analyze(raw);
        var rawField = MaxVertexField(raw, sdf);
        timings["mesh"] = Lap(watch);

        var print = raw;

        if (mesh.Decimate)
        {
            _log(FormattableString.Invariant($"[{part.Id}] simplifying {raw.TriangleCount} triangles (tolerance {mesh.DecimateToleranceMm} mm)"));
            print = QemDecimator.Decimate(raw, sdf, new DecimationOptions(mesh.DecimateToleranceMm), out _);
        }

        var printReport = mesh.Decimate ? MeshAnalysis.Analyze(print) : rawReport;
        var printField = mesh.Decimate ? MaxVertexField(print, sdf) : rawField;
        timings["simplify"] = Lap(watch);

        var stlPath = Path.Combine(partOutputDirectory, part.Id + ".stl");
        StlWriter.Write(print, stlPath, "PicoMoldForge Parts " + part.Id + " mm ISO-841");
        artifacts.Add(stlPath);
        timings["stl"] = Lap(watch);

        _log(FormattableString.Invariant($"[{part.Id}] measuring distance to the ideal surface"));
        var surface = SurfaceDistanceProbe.Measure(print, sdf, 2 * mesh.ResolutionMm, options.SurfaceSamples, cancellationToken);
        timings["surfaceDistance"] = Lap(watch);

        MeshSummary? previewSummary = null;
        LatticeInfo? lattice = null;

        if (options.WriteViewerData)
        {
            var preview = BuildPreview(print, sdf, mesh);
            var previewPath = Path.Combine(partOutputDirectory, PreviewFileName);
            PreviewMeshWriter.Write(preview, previewPath);
            previewSummary = MeshSummary.From(MeshAnalysis.Analyze(preview));
            artifacts.Add(previewPath);

            var featuresPath = Path.Combine(partOutputDirectory, FeaturesFileName);
            FeatureExporter.Write(part, featuresPath);
            artifacts.Add(featuresPath);

            var latticePath = Path.Combine(partOutputDirectory, LatticeFileName);
            lattice = LatticeExporter.Write(part, latticePath);
            artifacts.Add(latticePath);
            timings["viewer"] = Lap(watch);
        }

        var voxelVolume = VoxelVolumeEstimator.EstimateVolume(sdf, mesh.ResolutionMm, cancellationToken);
        timings["voxelVolume"] = Lap(watch);

        var checks = new List<PartCheckResult>();
        checks.AddRange(MeshChecks(part, mesh, rawReport, printReport, rawField, printField, surface, voxelVolume, lattice));

        var context = new PartCheckContext(sdf, mesh.ResolutionMm);

        foreach (var check in part.Expectations.Checks)
        {
            checks.Add(check.Evaluate(context));
        }

        timings["checks"] = Lap(watch);

        var status = checks.All(c => c.Passed) ? "PASS" : "FAIL";
        var reportPath = Path.Combine(partOutputDirectory, ReportFileName);
        artifacts.Add(reportPath);

        var report = new PartReport(
            PartReport.SchemaVersion,
            part.Id,
            part.Title,
            part.Template,
            "reference",
            AxisConvention.Name,
            AxisConvention.OrientationOrder,
            part.Parameters,
            mesh,
            MeshSummary.From(rawReport),
            MeshSummary.From(printReport),
            previewSummary,
            Math.Round(rawField, 6),
            Math.Round(printField, 6),
            surface with { VertexMaxMm = Math.Round(surface.VertexMaxMm, 6), TriangleMaxMm = Math.Round(surface.TriangleMaxMm, 6) },
            Math.Round(voxelVolume, 3),
            lattice,
            checks,
            timings.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 3)),
            artifacts.Select(Path.GetFileName).Where(n => n is not null).Select(n => n!).ToArray(),
            status,
            new[]
            {
                "Geometría generada para revisión de ingeniería; no está certificada para fabricación.",
                "Ejes ISO 841: X, Y, Z lineales; A, B, C giran sobre X, Y, Z (regla de la mano derecha).",
                "Fuera de alcance: trayectorias de herramienta / G-code (CAM) y exportación STEP."
            });

        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, ReportJson.Options), new UTF8Encoding(false));
        _log(FormattableString.Invariant($"[{part.Id}] {status}: {print.TriangleCount} triangles, genus {printReport.Genus?.ToString(CultureInfo.InvariantCulture) ?? "?"}, volume {printReport.VolumeMm3:0.###} mm3"));

        return new PartGenerationResult(report, print, artifacts);
    }

    private static MeshSettings ResolveMesh(MeshSettings settings, PartGenerationOptions options)
    {
        var resolution = options.ResolutionOverrideMm ?? settings.ResolutionMm;
        var tolerance = Math.Min(settings.DecimateToleranceMm, resolution);

        if (options.ResolutionOverrideMm is not null)
        {
            // Keep the same tolerance-to-resolution ratio as the recipe.
            tolerance = settings.DecimateToleranceMm / settings.ResolutionMm * resolution;
        }

        return new MeshSettings(resolution, options.DecimateOverride ?? settings.Decimate, tolerance, settings.PreviewMaxTriangles);
    }

    /// <summary>The viewer shows the STL mesh itself; only meshes above the budget are simplified further.</summary>
    private static TriangleMesh BuildPreview(TriangleMesh print, ISdf sdf, MeshSettings mesh)
    {
        var preview = print;
        var tolerance = Math.Max(mesh.DecimateToleranceMm, mesh.ResolutionMm / 4);

        for (var attempt = 0; attempt < 4 && preview.TriangleCount > mesh.PreviewMaxTriangles; attempt++)
        {
            preview = QemDecimator.Decimate(preview, sdf, new DecimationOptions(tolerance), out _);
            tolerance *= 2;
        }

        return preview;
    }

    private static IEnumerable<PartCheckResult> MeshChecks(
        PartDefinition part,
        MeshSettings mesh,
        MeshReport raw,
        MeshReport print,
        double rawField,
        double printField,
        SurfaceDistanceReport surface,
        double voxelVolume,
        LatticeInfo? lattice)
    {
        string Mm(double v) => v.ToString("0.####", CultureInfo.InvariantCulture) + " mm";
        string Pct(double v) => (v * 100).ToString("0.###", CultureInfo.InvariantCulture) + " %";

        yield return new PartCheckResult(
            "mesh.watertight",
            "Malla cerrada, manifold y orientada hacia afuera (cada arista en exactamente 2 triángulos)",
            "closed, manifold, oriented, outward",
            FormattableString.Invariant($"closed={print.Closed}, manifold={print.Manifold}, oriented={print.ConsistentlyOriented}, outward={print.OutwardNormals}, boundaryEdges={print.BoundaryEdges}, nonManifoldEdges={print.NonManifoldEdges}"),
            print.Watertight && raw.Watertight);

        yield return new PartCheckResult(
            "mesh.single-body",
            "La pieza es un solo cuerpo",
            "1 componente",
            print.Components.ToString(CultureInfo.InvariantCulture) + " componente(s)",
            print.Components == 1);

        if (part.Expectations.Genus is int genus)
        {
            yield return new PartCheckResult(
                "mesh.topology",
                "Topología esperada: número de agujeros pasantes (género) medido en la malla",
                FormattableString.Invariant($"género {genus} (Euler {2 - (2 * genus)})"),
                FormattableString.Invariant($"género {print.Genus?.ToString(CultureInfo.InvariantCulture) ?? "indefinido"} (Euler {print.EulerCharacteristic})"),
                print.Genus == genus && raw.Genus == genus);
        }

        if (part.Expectations.BoundingBox is Box3 box)
        {
            var tolerance = Math.Max(part.Expectations.BoundingBoxToleranceMm ?? 0, mesh.ResolutionMm);
            var worst = new[]
            {
                Math.Abs(print.BoundsMin.X - box.Min.X), Math.Abs(print.BoundsMin.Y - box.Min.Y), Math.Abs(print.BoundsMin.Z - box.Min.Z),
                Math.Abs(print.BoundsMax.X - box.Max.X), Math.Abs(print.BoundsMax.Y - box.Max.Y), Math.Abs(print.BoundsMax.Z - box.Max.Z)
            }.Max();

            yield return new PartCheckResult(
                "mesh.bounding-box",
                "Dimensiones exteriores medidas en la malla",
                $"{box.Min} .. {box.Max} ± {Mm(tolerance)}",
                $"{print.BoundsMin} .. {print.BoundsMax} (desviación máx. {Mm(worst)})",
                worst <= tolerance);
        }

        var volumeTolerance = Math.Max(0.01, 0.25 * raw.AreaMm2 * mesh.ResolutionMm / Math.Max(raw.VolumeMm3, 1e-9));
        var volumeError = Math.Abs(raw.VolumeMm3 - voxelVolume) / Math.Max(voxelVolume, 1e-9);

        yield return new PartCheckResult(
            "mesh.volume-crosscheck",
            "Volumen de la malla contra conteo independiente de vóxeles (solo signo de la distancia)",
            "diferencia <= " + Pct(volumeTolerance),
            FormattableString.Invariant($"malla {raw.VolumeMm3:0.###} mm³, vóxeles {voxelVolume:0.###} mm³, diferencia {Pct(volumeError)}"),
            volumeError <= volumeTolerance);

        var simplificationError = Math.Abs(print.VolumeMm3 - raw.VolumeMm3) / Math.Max(raw.VolumeMm3, 1e-9);

        yield return new PartCheckResult(
            "mesh.simplification-volume",
            "La simplificación no altera el volumen",
            "diferencia <= 0.5 %",
            Pct(simplificationError),
            simplificationError <= 0.005);

        var fieldLimit = Math.Max(rawField, mesh.Decimate ? mesh.DecimateToleranceMm : 0) + 1e-9;

        yield return new PartCheckResult(
            "mesh.simplification-fidelity",
            "Valor del campo de distancia |SDF| en cada vértice simplificado <= tolerancia del simplificador (o el de la malla original). Es lo que el simplificador garantiza; la distancia geométrica se mide en mesh.surface-distance",
            "<= " + Mm(fieldLimit),
            Mm(printField),
            printField <= fieldLimit);

        var surfaceLimit = mesh.ResolutionMm + 1e-9;
        var unresolvedText = surface.Unresolved > 0
            ? FormattableString.Invariant($", {surface.Unresolved} muestra(s) sin superficie a menos de {surface.SearchLimitMm:0.###} mm")
            : string.Empty;

        yield return new PartCheckResult(
            "mesh.surface-distance",
            "Distancia de la malla STL a la superficie ideal: cota superior medida en vértices y en centroide y puntos medios de aristas de los triángulos (bisección sobre el signo exacto). Límite: la resolución de mallado (marching cubes achaflana aristas vivas hasta ~h/2)",
            "<= " + Mm(surfaceLimit),
            FormattableString.Invariant($"vértices {Mm(surface.VertexMaxMm)}, triángulos {Mm(surface.TriangleMaxMm)} ({surface.VertexSamples} de {surface.Vertices} vértices y {surface.TriangleSamples} de {surface.Triangles} triángulos muestreados{unresolvedText})"),
            surface.Unresolved == 0 && surface.MaxMm <= surfaceLimit);

        if (lattice is not null)
        {
            yield return new PartCheckResult(
                "viewer.lattice-replay",
                "La animación de la malla (rasgos en orden) reproduce exactamente el sólido",
                "reproducción idéntica",
                lattice.ReplayMatchesSolid ? "idéntica" : "difiere",
                lattice.ReplayMatchesSolid);
        }
    }

    /// <summary>Largest |field value| at the vertices (what the simplifier constrains; not a geometric distance).</summary>
    private static double MaxVertexField(TriangleMesh mesh, ISdf sdf)
    {
        var vertices = mesh.Vertices;
        var partial = new double[Environment.ProcessorCount];
        var chunk = (vertices.Length + partial.Length - 1) / Math.Max(partial.Length, 1);

        Parallel.For(0, partial.Length, c =>
        {
            var max = 0.0;
            var end = Math.Min(vertices.Length, (c + 1) * chunk);

            for (var i = c * chunk; i < end; i++)
            {
                max = Math.Max(max, Math.Abs(sdf.Distance(vertices[i])));
            }

            partial[c] = max;
        });

        return partial.Length == 0 ? 0 : partial.Max();
    }

    private static double Lap(Stopwatch watch)
    {
        var seconds = watch.Elapsed.TotalSeconds;
        watch.Restart();
        return seconds;
    }
}

internal static class ReportJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new Vec3JsonConverter(), new KeyValueListConverter() }
    };
}

internal sealed class Vec3JsonConverter : System.Text.Json.Serialization.JsonConverter<Vec3>
{
    public override Vec3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, Vec3 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(Math.Round(value.X, 4));
        writer.WriteNumberValue(Math.Round(value.Y, 4));
        writer.WriteNumberValue(Math.Round(value.Z, 4));
        writer.WriteEndArray();
    }
}

internal sealed class KeyValueListConverter : System.Text.Json.Serialization.JsonConverter<IReadOnlyList<KeyValuePair<string, string>>>
{
    public override IReadOnlyList<KeyValuePair<string, string>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<KeyValuePair<string, string>> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var pair in value)
        {
            writer.WriteStartObject();
            writer.WriteString("name", pair.Key);
            writer.WriteString("value", pair.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
