using System.Globalization;
using System.Text.Json;
using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Templates;
using PicoMoldForge.Parts.Verification;

namespace PicoMoldForge.Parts.Recipes;

/// <summary>Raised for invalid recipes; lists every problem with its JSON path.</summary>
public sealed class RecipeException : Exception
{
    public RecipeException(string source, IReadOnlyList<string> errors)
        : base($"Invalid recipe '{source}':{Environment.NewLine}  - " + string.Join(Environment.NewLine + "  - ", errors))
    {
        Source = source;
        Errors = errors;
    }

    public new string Source { get; }

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// Loads part recipes (JSON, schema picomoldforge.part-recipe.v1). A recipe is either a parametric template
/// (spurGear, helicalGear, piston, monoblock) or an explicit list of features. Unknown properties are errors,
/// so typos never silently fall back to defaults.
/// </summary>
public static class RecipeLoader
{
    public const string SchemaVersion = "picomoldforge.part-recipe.v1";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static PartDefinition LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Recipe path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Recipe file was not found.", fullPath);
        }

        return Parse(File.ReadAllText(fullPath), Path.GetFileName(fullPath));
    }

    public static PartDefinition Parse(string json, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new RecipeException(sourceName, new[] { "JSON syntax error: " + ex.Message });
        }

        using (document)
        {
            var errors = new List<string>();
            var reader = new JsonObjectReader(document.RootElement, "$", errors);
            PartDefinition? part = null;

            try
            {
                part = ParseRoot(reader, errors);
            }
            catch (PartDefinitionException ex)
            {
                errors.AddRange(ex.Errors.Select(e => "$.template: " + e));
            }
            catch (ArgumentException ex)
            {
                errors.Add(ex.Message);
            }

            if (errors.Count > 0 || part is null)
            {
                throw new RecipeException(sourceName, errors.Count > 0 ? errors : new[] { "Recipe could not be built." });
            }

            return part;
        }
    }

    private static PartDefinition? ParseRoot(JsonObjectReader root, List<string> errors)
    {
        if (!root.IsObject)
        {
            errors.Add("$: the recipe must be a JSON object.");
            return null;
        }

        var schema = root.RequiredString("schemaVersion");

        if (schema is not null && schema != SchemaVersion)
        {
            errors.Add($"$.schemaVersion: expected '{SchemaVersion}', found '{schema}'.");
        }

        var name = root.RequiredString("name");
        var title = root.OptionalString("title");
        root.OptionalString("description");
        var units = root.OptionalString("units") ?? "mm";
        var convention = root.OptionalString("axisConvention") ?? AxisConvention.Name;

        if (units != "mm")
        {
            errors.Add("$.units: only 'mm' is supported.");
        }

        if (convention != AxisConvention.Name)
        {
            errors.Add($"$.axisConvention: only '{AxisConvention.Name}' is supported (X, Y, Z linear; A, B, C rotary about X, Y, Z; right-hand rule).");
        }

        var hasTemplate = root.Has("template");
        var hasFeatures = root.Has("features");

        if (hasTemplate == hasFeatures)
        {
            errors.Add("$: define exactly one of 'template' or 'features'.");
        }

        var templateReader = hasTemplate ? root.Object("template") : null;
        var featureReaders = hasFeatures ? root.Array("features") : null;
        var templateType = templateReader?.RequiredString("type");
        var mesh = ParseMesh(root.OptionalObject("mesh"), DefaultMesh(templateType), errors);
        var expectReader = root.OptionalObject("expect");

        root.RejectUnknown();

        if (name is not null && !IsValidId(name))
        {
            errors.Add("$.name: use only ASCII letters, digits, '-' or '_' (it becomes the output folder name).");
        }

        if (errors.Count > 0 || name is null)
        {
            return null;
        }

        if (templateReader is not null)
        {
            if (expectReader is not null)
            {
                errors.Add("$.expect: templates define their own expectations; 'expect' is only for feature recipes.");
                return null;
            }

            return BuildTemplate(templateReader, templateType, name, title, mesh, errors);
        }

        var features = ParseFeatures(featureReaders, errors);
        var expectations = ParseExpectations(expectReader, errors);

        if (errors.Count > 0)
        {
            return null;
        }

        return new PartDefinition(
            name,
            title ?? name,
            "features",
            new List<KeyValuePair<string, string>> { new("Rasgos", features.Count.ToString(CultureInfo.InvariantCulture)) },
            features,
            expectations,
            mesh);
    }

    private static bool IsValidId(string id) => id.Length is > 0 and <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    /// <summary>Default meshing per template: fine enough for 3D printing, fast enough for the demo.</summary>
    public static MeshSettings DefaultMesh(string? templateType) => templateType switch
    {
        GearTemplate.SpurType or GearTemplate.HelicalType => new MeshSettings(0.12, true, 0.01),
        PistonTemplate.Type => new MeshSettings(0.18, true, 0.015),
        MonoblockTemplate.Type => new MeshSettings(0.35, true, 0.03),
        _ => new MeshSettings(0.2, true, 0.02)
    };

    private static MeshSettings ParseMesh(JsonObjectReader? reader, MeshSettings defaults, List<string> errors)
    {
        if (reader is null)
        {
            return defaults;
        }

        var resolution = reader.OptionalDouble("resolutionMm") ?? defaults.ResolutionMm;
        var decimate = reader.OptionalBool("decimate") ?? defaults.Decimate;
        var tolerance = reader.OptionalDouble("decimateToleranceMm") ?? Math.Min(defaults.DecimateToleranceMm, resolution);
        var previewMax = reader.OptionalInt("previewMaxTriangles") ?? defaults.PreviewMaxTriangles;
        reader.RejectUnknown();

        var settings = new MeshSettings(resolution, decimate, tolerance, previewMax);
        errors.AddRange(settings.Validate().Select(e => "$.mesh: " + e));
        return settings;
    }

    private static PartDefinition? BuildTemplate(JsonObjectReader t, string? type, string name, string? title, MeshSettings mesh, List<string> errors)
    {
        switch (type)
        {
            case GearTemplate.SpurType:
            case GearTemplate.HelicalType:
            {
                var d = new GearParameters();
                var p = new GearParameters
                {
                    Module = t.OptionalDouble("module") ?? d.Module,
                    Teeth = t.OptionalInt("teeth") ?? d.Teeth,
                    PressureAngleDeg = t.OptionalDouble("pressureAngleDeg") ?? d.PressureAngleDeg,
                    HelixAngleDeg = t.OptionalDouble("helixAngleDeg") ?? (type == GearTemplate.HelicalType ? 20.0 : 0.0),
                    FaceWidth = t.OptionalDouble("faceWidth") ?? d.FaceWidth,
                    BoreDiameter = t.OptionalDouble("boreDiameter") ?? d.BoreDiameter,
                    KeywayWidth = t.OptionalDouble("keywayWidth") ?? d.KeywayWidth,
                    KeywayDepth = t.OptionalDouble("keywayDepth") ?? d.KeywayDepth,
                    ProfileShift = t.OptionalDouble("profileShift") ?? d.ProfileShift,
                    ThicknessAllowance = t.OptionalDouble("thicknessAllowance") ?? d.ThicknessAllowance
                };
                t.RejectUnknown();
                return errors.Count > 0 ? null : GearTemplate.Build(name, title, p, mesh, type == GearTemplate.HelicalType);
            }

            case PistonTemplate.Type:
            {
                var d = new PistonParameters();
                var p = new PistonParameters
                {
                    BoreDiameter = t.OptionalDouble("boreDiameter") ?? d.BoreDiameter,
                    Clearance = t.OptionalDouble("clearance") ?? d.Clearance,
                    Height = t.OptionalDouble("height") ?? d.Height,
                    CompressionHeight = t.OptionalDouble("compressionHeight") ?? d.CompressionHeight,
                    PinDiameter = t.OptionalDouble("pinDiameter") ?? d.PinDiameter,
                    PinBossDiameter = t.OptionalDouble("pinBossDiameter") ?? d.PinBossDiameter,
                    PinBossGap = t.OptionalDouble("pinBossGap") ?? d.PinBossGap,
                    CrownThickness = t.OptionalDouble("crownThickness") ?? d.CrownThickness,
                    SkirtWall = t.OptionalDouble("skirtWall") ?? d.SkirtWall,
                    RingBeltWall = t.OptionalDouble("ringBeltWall") ?? d.RingBeltWall,
                    TopLand = t.OptionalDouble("topLand") ?? d.TopLand,
                    RingGrooveWidths = t.OptionalDoubleArray("ringGrooveWidths") ?? d.RingGrooveWidths,
                    RingLands = t.OptionalDoubleArray("ringLands") ?? d.RingLands,
                    GrooveDepth = t.OptionalDouble("grooveDepth") ?? d.GrooveDepth,
                    SkirtReliefDepth = t.OptionalDouble("skirtReliefDepth") ?? d.SkirtReliefDepth
                };
                t.RejectUnknown();
                return errors.Count > 0 ? null : PistonTemplate.Build(name, title, p, mesh);
            }

            case MonoblockTemplate.Type:
            {
                var d = new MonoblockParameters();
                var p = new MonoblockParameters
                {
                    Cylinders = t.OptionalInt("cylinders") ?? d.Cylinders,
                    Bore = t.OptionalDouble("bore") ?? d.Bore,
                    CylinderSpacing = t.OptionalDouble("cylinderSpacing") ?? d.CylinderSpacing,
                    LinerThickness = t.OptionalDouble("linerThickness") ?? d.LinerThickness,
                    JacketWidth = t.OptionalDouble("jacketWidth") ?? d.JacketWidth,
                    JacketDepth = t.OptionalDouble("jacketDepth") ?? d.JacketDepth,
                    Length = t.OptionalDouble("length") ?? d.Length,
                    Width = t.OptionalDouble("width") ?? d.Width,
                    Height = t.OptionalDouble("height") ?? d.Height,
                    CrankcaseHeight = t.OptionalDouble("crankcaseHeight") ?? d.CrankcaseHeight,
                    WallThickness = t.OptionalDouble("wallThickness") ?? d.WallThickness,
                    BulkheadThickness = t.OptionalDouble("bulkheadThickness") ?? d.BulkheadThickness,
                    MainBearingDiameter = t.OptionalDouble("mainBearingDiameter") ?? d.MainBearingDiameter,
                    CrankAxisHeight = t.OptionalDouble("crankAxisHeight") ?? d.CrankAxisHeight,
                    BoltHoleDiameter = t.OptionalDouble("boltHoleDiameter") ?? d.BoltHoleDiameter,
                    BoltDepth = t.OptionalDouble("boltDepth") ?? d.BoltDepth,
                    BoltOffsetY = t.OptionalDouble("boltOffsetY") ?? d.BoltOffsetY
                };
                t.RejectUnknown();
                return errors.Count > 0 ? null : MonoblockTemplate.Build(name, title, p, mesh);
            }

            case null:
                return null;

            default:
                errors.Add($"$.template.type: unknown template '{type}' (use spurGear, helicalGear, piston or monoblock).");
                return null;
        }
    }

    private static List<FeatureDefinition> ParseFeatures(IReadOnlyList<JsonObjectReader>? items, List<string> errors)
    {
        var features = new List<FeatureDefinition>();

        if (items is null)
        {
            return features;
        }

        if (items.Count == 0)
        {
            errors.Add("$.features: at least one feature is required.");
            return features;
        }

        foreach (var f in items)
        {
            var id = f.RequiredString("id");
            var label = f.OptionalString("label");
            var opText = f.RequiredString("op");
            var sweepReader = f.Object("sweep");
            var profileReader = f.Object("profile");
            var origin = f.OptionalVec3("origin") ?? Vec3.Zero;
            var orientationReader = f.OptionalObject("orientation");
            var positions = f.OptionalVec3Array("positions");
            var patternReader = f.OptionalObject("pattern");
            f.RejectUnknown();

            FeatureOperation operation = default;

            switch (opText)
            {
                case "add":
                    operation = FeatureOperation.Add;
                    break;
                case "subtract":
                    operation = FeatureOperation.Subtract;
                    break;
                case "intersect":
                    operation = FeatureOperation.Intersect;
                    break;
                case null:
                    break;
                default:
                    errors.Add($"{f.Path}.op: use 'add', 'subtract' or 'intersect'.");
                    break;
            }

            if (id is not null && !IsValidId(id))
            {
                errors.Add($"{f.Path}.id: use only ASCII letters, digits, '-' or '_'.");
            }

            var sweep = sweepReader is null ? null : ParseSweep(sweepReader, errors);
            var profile = profileReader is null ? null : ParseProfile(profileReader, errors);
            var orientation = ParseOrientation(orientationReader);

            if (positions is not null && f.Has("origin"))
            {
                errors.Add($"{f.Path}: use either 'origin' or 'positions', not both.");
            }

            if (sweep is RotarySweepDefinition && profile is not null)
            {
                ValidateRotaryProfile(profile, profileReader!.Path, errors);
            }

            if (id is null || sweep is null || profile is null || opText is null)
            {
                continue;
            }

            var bases = (positions ?? new[] { origin })
                .Select(position => new RigidTransform(orientation.ToMatrix(), position))
                .ToArray();
            var instances = patternReader is null ? bases : ParsePattern(patternReader, bases, errors);

            if (instances is null)
            {
                continue;
            }

            features.Add(new FeatureDefinition(id, label ?? id, operation, sweep, profile, instances));
        }

        if (features.Count > 0 && features[0].Operation != FeatureOperation.Add)
        {
            errors.Add("$.features[0].op: the first feature must be 'add'.");
        }

        var duplicates = features.GroupBy(x => x.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();

        foreach (var duplicate in duplicates)
        {
            errors.Add($"$.features: duplicate feature id '{duplicate}'.");
        }

        return features;
    }

    private static SweepDefinition? ParseSweep(JsonObjectReader s, List<string> errors)
    {
        var kind = s.RequiredString("kind");
        var axisText = s.RequiredString("axis");
        SweepDefinition? result = null;

        switch (kind)
        {
            case "linear":
            {
                var from = s.RequiredDouble("from");
                var to = s.RequiredDouble("to");
                var twist = s.OptionalDouble("twistDeg") ?? 0;

                if (!AxisConvention.TryParseLinear(axisText, out var axis))
                {
                    errors.Add($"{s.Path}.axis: linear sweeps use X, Y or Z.");
                }
                else if (from is not null && to is not null)
                {
                    if (from == to)
                    {
                        errors.Add($"{s.Path}: 'from' and 'to' must differ.");
                    }
                    else
                    {
                        result = new LinearSweepDefinition(axis, from.Value, to.Value, twist);
                    }
                }

                break;
            }

            case "rotary":
            {
                var start = s.OptionalDouble("startDeg") ?? 0;
                var end = s.OptionalDouble("endDeg") ?? 360;

                if (!AxisConvention.TryParseRotary(axisText, out var axis))
                {
                    errors.Add($"{s.Path}.axis: rotary sweeps use A (about X), B (about Y) or C (about Z).");
                }
                else if (start == end || Math.Abs(end - start) > 360)
                {
                    errors.Add($"{s.Path}: rotary angles must differ by more than 0 and at most 360 degrees.");
                }
                else
                {
                    result = new RotarySweepDefinition(axis, start, end);
                }

                break;
            }

            case null:
                break;

            default:
                errors.Add($"{s.Path}.kind: use 'linear' or 'rotary'.");
                break;
        }

        s.RejectUnknown();
        return result;
    }

    private static IProfile2D? ParseProfile(JsonObjectReader p, List<string> errors)
    {
        var type = p.RequiredString("type");
        IProfile2D? result = null;

        try
        {
            switch (type)
            {
                case "circle":
                {
                    var center = p.OptionalVec2("center") ?? Vec2.Zero;
                    var diameter = p.RequiredDouble("diameter");

                    if (diameter is not null)
                    {
                        result = new CircleProfile(center, diameter.Value / 2);
                    }

                    break;
                }

                case "ring":
                {
                    var center = p.OptionalVec2("center") ?? Vec2.Zero;
                    var outer = p.RequiredDouble("outerDiameter");
                    var inner = p.RequiredDouble("innerDiameter");

                    if (outer is not null && inner is not null)
                    {
                        if (!(inner > 0 && outer > inner))
                        {
                            errors.Add($"{p.Path}: ring needs 0 < innerDiameter < outerDiameter.");
                        }
                        else
                        {
                            result = new ProfileDifference(new CircleProfile(center, outer.Value / 2), new CircleProfile(center, inner.Value / 2));
                        }
                    }

                    break;
                }

                case "rectangle":
                {
                    var min = p.RequiredVec2("min");
                    var max = p.RequiredVec2("max");
                    var corner = p.OptionalDouble("cornerRadius") ?? 0;

                    if (min is not null && max is not null)
                    {
                        result = new RectangleProfile(min.Value, max.Value, corner);
                    }

                    break;
                }

                case "polygon":
                {
                    var points = p.RequiredVec2Array("points");

                    if (points is not null)
                    {
                        result = new PolygonProfile(points);
                    }

                    break;
                }

                case "halfSection":
                {
                    var points = p.RequiredVec2Array("points");

                    if (points is not null)
                    {
                        result = PolygonProfile.FromHalfSection(points);
                    }

                    break;
                }

                case "union":
                {
                    var children = p.Array("profiles")?.Select(child => ParseProfile(child, errors)).ToArray();

                    if (children is not null && children.Length > 0 && children.All(c => c is not null))
                    {
                        result = new ProfileUnion(children!);
                    }

                    break;
                }

                case "difference":
                {
                    var baseProfile = p.Object("base") is { } b ? ParseProfile(b, errors) : null;
                    var cut = p.Object("cut") is { } c ? ParseProfile(c, errors) : null;

                    if (baseProfile is not null && cut is not null)
                    {
                        result = new ProfileDifference(baseProfile, cut);
                    }

                    break;
                }

                case null:
                    break;

                default:
                    errors.Add($"{p.Path}.type: unknown profile '{type}' (circle, ring, rectangle, polygon, halfSection, union, difference).");
                    break;
            }
        }
        catch (ArgumentException ex)
        {
            errors.Add($"{p.Path}: {ex.Message}");
            result = null;
        }

        p.RejectUnknown();
        return result;
    }

    private static void ValidateRotaryProfile(IProfile2D profile, string path, List<string> errors)
    {
        var bounds = profile.Bounds;

        if (bounds.Min.X >= -1e-9)
        {
            return;
        }

        var symmetric = Math.Abs(bounds.Min.X + bounds.Max.X) <= 1e-6;

        if (!symmetric)
        {
            errors.Add($"{path}: a rotary profile that crosses the axis must be a full symmetric section (use 'halfSection' for shapes that touch the axis).");
        }
    }

    private static Orientation ParseOrientation(JsonObjectReader? o)
    {
        if (o is null)
        {
            return Orientation.None;
        }

        var orientation = new Orientation(o.OptionalDouble("A") ?? 0, o.OptionalDouble("B") ?? 0, o.OptionalDouble("C") ?? 0);
        o.RejectUnknown();
        return orientation;
    }

    private static IReadOnlyList<RigidTransform>? ParsePattern(JsonObjectReader p, IReadOnlyList<RigidTransform> bases, List<string> errors)
    {
        var type = p.RequiredString("type");
        var axisText = p.RequiredString("axis");
        var count = p.RequiredInt("count");
        IReadOnlyList<RigidTransform>? result = null;

        if (count is not null && (count < 1 || count > 1000))
        {
            errors.Add($"{p.Path}.count: must be between 1 and 1000.");
        }

        switch (type)
        {
            case "circular":
            {
                var start = p.OptionalDouble("startDeg") ?? 0;
                var span = p.OptionalDouble("spanDeg") ?? 360;
                var center = p.OptionalVec3("center") ?? Vec3.Zero;

                if (!AxisConvention.TryParseRotary(axisText, out var axis))
                {
                    errors.Add($"{p.Path}.axis: circular patterns turn about A, B or C.");
                }
                else if (count is >= 1 and <= 1000)
                {
                    result = Placement.Circular(bases, axis, count.Value, start, span, center);
                }

                break;
            }

            case "linear":
            {
                var step = p.RequiredDouble("step");

                if (!AxisConvention.TryParseLinear(axisText, out var axis))
                {
                    errors.Add($"{p.Path}.axis: linear patterns move along X, Y or Z.");
                }
                else if (count is >= 1 and <= 1000 && step is not null)
                {
                    result = Placement.Linear(bases, axis, count.Value, step.Value);
                }

                break;
            }

            case null:
                break;

            default:
                errors.Add($"{p.Path}.type: use 'circular' or 'linear'.");
                break;
        }

        p.RejectUnknown();
        return result;
    }

    private static PartExpectations ParseExpectations(JsonObjectReader? e, List<string> errors)
    {
        if (e is null)
        {
            return PartExpectations.None;
        }

        var genus = e.OptionalInt("genus");
        var boxReader = e.OptionalObject("boundingBox");
        var tolerance = e.OptionalDouble("boundingBoxToleranceMm");
        var pointReaders = e.OptionalArray("surfacePoints");
        e.RejectUnknown();

        Box3? box = null;

        if (boxReader is not null)
        {
            var min = boxReader.RequiredVec3("min");
            var max = boxReader.RequiredVec3("max");
            boxReader.RejectUnknown();

            if (min is not null && max is not null)
            {
                box = new Box3(min.Value, max.Value);
            }
        }

        var checks = new List<IPartCheck>();

        foreach (var point in pointReaders ?? Array.Empty<JsonObjectReader>())
        {
            var id = point.RequiredString("id");
            var description = point.OptionalString("description");
            var position = point.RequiredVec3("point");
            var tol = point.OptionalDouble("toleranceMm") ?? 0.002;
            point.RejectUnknown();

            if (id is not null && position is not null)
            {
                checks.Add(new SurfacePointCheck(id, description ?? id, position.Value, tol));
            }
        }

        if (genus is < 0)
        {
            errors.Add("$.expect.genus: cannot be negative.");
        }

        return new PartExpectations(box, tolerance, genus, checks);
    }
}
