using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;
using PicoMoldForge.Parts.Verification;

namespace PicoMoldForge.Parts.Features;

/// <summary>
/// Meshing settings in millimeters. The viewer shows the same simplified mesh that goes into the STL
/// (quantized to 16 bits per axis) unless it exceeds <see cref="PreviewMaxTriangles"/>.
/// </summary>
public sealed record MeshSettings(
    double ResolutionMm,
    bool Decimate,
    double DecimateToleranceMm,
    int PreviewMaxTriangles = 250_000)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!(ResolutionMm >= 0.02 && ResolutionMm <= 10))
        {
            errors.Add("mesh.resolutionMm must be between 0.02 and 10 mm.");
        }

        if (PreviewMaxTriangles < 1_000 || PreviewMaxTriangles > 5_000_000)
        {
            errors.Add("mesh.previewMaxTriangles must be between 1000 and 5000000.");
        }

        if (!(DecimateToleranceMm > 0 && DecimateToleranceMm <= ResolutionMm))
        {
            errors.Add("mesh.decimateToleranceMm must be greater than 0 and at most mesh.resolutionMm.");
        }

        return errors;
    }
}

/// <summary>What the generated part must satisfy (verified after meshing).</summary>
public sealed record PartExpectations(
    Box3? BoundingBox,
    double? BoundingBoxToleranceMm,
    int? Genus,
    IReadOnlyList<IPartCheck> Checks)
{
    public static readonly PartExpectations None = new(null, null, null, Array.Empty<IPartCheck>());
}

/// <summary>
/// A part is an ordered list of features (profile + trajectory + boolean operation) in ISO 841 coordinates.
/// This is the single geometric truth: the reference mesher, the PicoGK renderer, the viewer outlines, the
/// lattice animation and every verification read from it.
/// </summary>
public sealed class PartDefinition
{
    private ISdf? _sdf;

    public PartDefinition(
        string id,
        string title,
        string template,
        IReadOnlyList<KeyValuePair<string, string>> parameters,
        IReadOnlyList<FeatureDefinition> features,
        PartExpectations expectations,
        MeshSettings mesh)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')))
        {
            throw new ArgumentException("Part id must be non-empty and use only ASCII letters, digits, '-' or '_'.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(features);

        if (features.Count == 0 || features[0].Operation != FeatureOperation.Add)
        {
            throw new ArgumentException("A part needs at least one feature and the first one must add material.", nameof(features));
        }

        var duplicate = features.GroupBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate feature id '{duplicate.Key}'.", nameof(features));
        }

        Id = id;
        Title = title;
        Template = template;
        Parameters = parameters;
        Features = features;
        Expectations = expectations ?? PartExpectations.None;
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
    }

    public string Id { get; }

    public string Title { get; }

    public string Template { get; }

    public IReadOnlyList<KeyValuePair<string, string>> Parameters { get; }

    public IReadOnlyList<FeatureDefinition> Features { get; }

    public PartExpectations Expectations { get; }

    public MeshSettings Mesh { get; }

    /// <summary>The solid as a signed distance bound (built once, immutable, thread-safe).</summary>
    public ISdf Sdf => _sdf ??= BuildSdf();

    private ISdf BuildSdf()
    {
        var steps = Features
            .Select(feature => (ToCsg(feature.Operation), feature.BuildSdf()))
            .ToArray();

        return new CsgSdf(steps);
    }

    private static CsgOperation ToCsg(FeatureOperation operation) => operation switch
    {
        FeatureOperation.Add => CsgOperation.Add,
        FeatureOperation.Subtract => CsgOperation.Subtract,
        FeatureOperation.Intersect => CsgOperation.Intersect,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
