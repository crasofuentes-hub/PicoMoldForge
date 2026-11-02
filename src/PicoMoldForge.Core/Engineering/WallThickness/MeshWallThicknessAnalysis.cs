using System.Globalization;

namespace PicoMoldForge.Core.Engineering.WallThickness;

public sealed record MeshWallThicknessAnalysisResult(
    VoxelWallThicknessAnalysisResult Analysis,
    MeshWallThicknessResult Measurement,
    double NominalThicknessMm,
    IReadOnlyList<WallThicknessRegion> ReportedRegions,
    int OmittedRegionCount,
    WallThicknessRegionSet Regions,
    IReadOnlyList<string> Notes);

/// <summary>
/// Wall thickness of a part measured on its surface mesh and judged by <see cref="VoxelWallThicknessAnalyzer"/>:
/// inscribed-ball measurements (<see cref="MeshWallThicknessSampler"/>), the area-weighted median of the walls as
/// the nominal thickness, connected regions per band (<see cref="WallThicknessRegionBuilder"/>), one analyzer
/// sample per region. Regions that are not nominal go first; at most <c>maximumRegions</c> are sampled.
/// The thick limit is three times the measured nominal, as the previous configuration-based summary used three
/// times its nominal.
/// </summary>
public static class MeshWallThicknessAnalysis
{
    public const int DefaultMaximumRegions = 50;

    public static MeshWallThicknessAnalysisResult Analyze(
        IReadOnlyList<MeshTriangle> triangles,
        decimal minimumThicknessMm,
        decimal abruptChangeRatio,
        int maximumRegions = DefaultMaximumRegions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        if (minimumThicknessMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumThicknessMm), "Minimum thickness must be greater than zero.");
        }

        if (abruptChangeRatio < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(abruptChangeRatio), "Abrupt change ratio cannot be negative.");
        }

        if (maximumRegions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRegions), "At least one region must be reported.");
        }

        var measurement = MeshWallThicknessSampler.Sample(triangles, cancellationToken: cancellationToken);
        var nominal = WallThicknessRegionBuilder.NominalThickness(measurement.Measurements);
        var notes = new List<string>();
        var analyzer = new VoxelWallThicknessAnalyzer();

        if (measurement.OrientationFlipped)
        {
            notes.Add("The input STL is inside out (negative signed volume); its normals were flipped for the wall-thickness measurement.");
        }

        if (!(nominal > 0))
        {
            notes.Add(Invariant($"Wall thickness could not be measured on the input STL: none of its {measurement.TriangleCount} triangles has an opposing face (open mesh or no walls)."));
            var empty = analyzer.Analyze(new VoxelWallThicknessAnalysisInput(
                MinimumThicknessMm: minimumThicknessMm,
                MaximumThicknessMm: minimumThicknessMm,
                AbruptChangeWarningRatio: abruptChangeRatio,
                Samples: Array.Empty<VoxelWallThicknessSample>()));
            var none = new WallThicknessRegionSet(Array.Empty<WallThicknessRegion>(), 0, 0, (double)(minimumThicknessMm * minimumThicknessMm));
            return new MeshWallThicknessAnalysisResult(empty, measurement, 0, Array.Empty<WallThicknessRegion>(), 0, none, notes);
        }

        var maximumThicknessMm = Math.Max(Round(3 * nominal, 4), minimumThicknessMm);
        var bands = new WallThicknessBands((double)minimumThicknessMm, (double)maximumThicknessMm, nominal, (double)abruptChangeRatio);
        var regions = WallThicknessRegionBuilder.Build(triangles, measurement.Measurements, bands);
        var reported = regions.Regions
            .OrderBy(r => r.Band == WallThicknessBand.Nominal ? 1 : 0)
            .ThenBy(r => r.Band)
            .ThenByDescending(r => r.AreaMm2)
            .Take(maximumRegions)
            .ToArray();
        var omitted = regions.Regions.Count - reported.Length;
        var nominalDecimal = Round(nominal, 4);

        var analysis = analyzer.Analyze(new VoxelWallThicknessAnalysisInput(
            MinimumThicknessMm: minimumThicknessMm,
            MaximumThicknessMm: maximumThicknessMm,
            AbruptChangeWarningRatio: abruptChangeRatio,
            Samples: reported
                .Select(r => new VoxelWallThicknessSample(r.RegionId, Round(r.ThicknessMm, 4), nominalDecimal, Round(r.AreaMm2, 3)))
                .ToArray()));

        notes.Add(Invariant($"Wall thickness measured on the input STL (inscribed-ball method): {measurement.WallSampleCount} of {measurement.TriangleCount} triangles are wall samples ({Percent(measurement.WallAreaMm2, measurement.TotalAreaMm2)} of the surface area); nominal wall {nominal:0.000} mm (area-weighted median)."));
        notes.Add(Invariant($"Wall thickness regions: {reported.Length} reported{(omitted > 0 ? $", {omitted} more beyond the first {maximumRegions} not reported" : string.Empty)}; {regions.ExcludedRegionCount} regions smaller than {regions.MinimumRegionAreaMm2:0.###} mm2 ({regions.ExcludedAreaMm2:0.###} mm2 in total) left out as edge noise."));

        var thinnest = regions.Regions.Where(r => r.Band == WallThicknessBand.Thin).OrderBy(r => r.ThicknessMm).FirstOrDefault();

        if (thinnest is not null)
        {
            notes.Add(Invariant($"Thinnest wall region {thinnest.RegionId}: {thinnest.ThicknessMm:0.000} mm over {thinnest.AreaMm2:0.0} mm2 near ({thinnest.Centroid.X:0.0}, {thinnest.Centroid.Y:0.0}, {thinnest.Centroid.Z:0.0}) mm; limit {minimumThicknessMm} mm."));
        }

        return new MeshWallThicknessAnalysisResult(analysis, measurement, nominal, reported, omitted, regions, notes);
    }

    private static decimal Round(double value, int decimals) => Math.Round((decimal)value, decimals, MidpointRounding.AwayFromZero);

    private static string Percent(double part, double total) =>
        total > 0 ? (part / total).ToString("P1", CultureInfo.InvariantCulture) : "0 %";

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
