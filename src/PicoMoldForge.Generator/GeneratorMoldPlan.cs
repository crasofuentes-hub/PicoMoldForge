using System.Globalization;
using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.Shrinkage;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Parting;
using CorePartingAxis = PicoMoldForge.Core.Parting.PartingAxis;
using SeparationPartingAxis = PicoMoldForge.Core.Engineering.Separation.PartingAxis;

namespace PicoMoldForge.Generator;

/// <summary>
/// Layout of the mold for one generator run, computed once from the input STL (pure .NET, no PicoGK runtime) and
/// shared by the geometry steps and the report, so they all use the same part and the same split.
/// <paramref name="PartBounds"/> are measured on the STL (the part as designed); <paramref name="SplitAxis"/> and
/// <paramref name="PartingOffsetMm"/> are the split of the designed part; <paramref name="GeometrySearch"/> is the
/// split found on the part mesh, used when parting.mode is Geometry and reported in every case.
/// The mold is built around the cavity: the part scaled by 1 + shrinkage about its bounding-box centre
/// (<paramref name="CavityTriangles"/>, <paramref name="CavityBounds"/>); the split moves with it
/// (<paramref name="MoldPartingOffsetMm"/>), and the block fit is checked on the cavity.
/// </summary>
public sealed record GeneratorMoldPlan(
    IReadOnlyList<MeshTriangle> Triangles,
    MoldSeparationBounds PartBounds,
    MoldBlockFitSummary BlockFit,
    CorePartingAxis SplitAxis,
    decimal PartingOffsetMm,
    string PartingSource,
    PartingPlaneSearchResult GeometrySearch,
    IReadOnlyList<string> Notes,
    MoldShrinkage Shrinkage,
    decimal ShrinkageScale,
    MeshVertex ScaleCenter,
    IReadOnlyList<MeshTriangle> CavityTriangles,
    MoldSeparationBounds CavityBounds,
    decimal MoldPartingOffsetMm)
{
    /// <summary>File name of the cavity STL the runner writes for the boolean mold halves.</summary>
    public const string CavityFileName = "ShrinkageScaledPart.stl";

    public MoldShrinkageSummary ShrinkageSummary => new(
        Shrinkage.Rate,
        ShrinkageScale,
        Shrinkage.Source,
        Shrinkage.ResinName,
        CavityBounds,
        MoldPartingOffsetMm);

    public SeparationPartingAxis SeparationAxis => SplitAxis switch
    {
        CorePartingAxis.X => SeparationPartingAxis.X,
        CorePartingAxis.Y => SeparationPartingAxis.Y,
        CorePartingAxis.Z => SeparationPartingAxis.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(SplitAxis), SplitAxis, "Unsupported parting axis.")
    };
}

public static class GeneratorMoldPlanner
{
    public static GeneratorMoldPlan Plan(GeneratorPipelineInput input, PartingPlaneResult automaticPlane)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(automaticPlane);

        var triangles = BinaryStlTriangleReader.Read(input.ResolvedInputPath);
        var bounds = MeshBounds.Of(triangles);
        var partBounds = ToBounds(bounds);
        var shrinkage = MoldShrinkageResolver.Resolve(
            input.Resin?.Name,
            input.Resin?.ShrinkageRate,
            input.Config.Material.ShrinkageRate);
        var scale = 1m + shrinkage.Rate;
        var center = bounds.Center;
        var cavityTriangles = ScaleAbout(triangles, center, (double)scale);
        var cavityBounds = ToBounds(MeshBounds.Of(cavityTriangles));
        var block = input.BooleanMoldBlockBounds;
        var blockFit = MoldBlockFitChecker.Check(
            new MoldSeparationBounds(block.MinXmm, block.MinYmm, block.MinZmm, block.MaxXmm, block.MaxYmm, block.MaxZmm),
            cavityBounds,
            input.MoldSystem.MoldMarginMm);

        var notes = new List<string>
        {
            Invariant($"Part bounds measured on the input STL: X {partBounds.MinXmm:0.###} to {partBounds.MaxXmm:0.###}, Y {partBounds.MinYmm:0.###} to {partBounds.MaxYmm:0.###}, Z {partBounds.MinZmm:0.###} to {partBounds.MaxZmm:0.###} mm ({partBounds.SizeXmm:0.###} x {partBounds.SizeYmm:0.###} x {partBounds.SizeZmm:0.###} mm)."),
            shrinkage.Note,
            Invariant($"Cavity: the part scaled by {scale:0.####} about its bounding-box centre, X {cavityBounds.MinXmm:0.####} to {cavityBounds.MaxXmm:0.####}, Y {cavityBounds.MinYmm:0.####} to {cavityBounds.MaxYmm:0.####}, Z {cavityBounds.MinZmm:0.####} to {cavityBounds.MaxZmm:0.####} mm ({cavityBounds.SizeXmm:0.####} x {cavityBounds.SizeYmm:0.####} x {cavityBounds.SizeZmm:0.####} mm); the mold halves are cut around it.")
        };

        if (!blockFit.IsPartInsideBlock)
        {
            notes.Add(blockFit.MinimumMarginMm < 0m
                ? Invariant($"FAIL: the cavity sticks out of the mold block by {-blockFit.MinimumMarginMm:0.###} mm on {blockFit.MinimumMarginSide}; the mold halves are not valid. Separation and parting-plane scoring fall back to the configured part size.")
                : Invariant($"FAIL: the cavity touches the mold block face {blockFit.MinimumMarginSide}; it would be open there. Separation and parting-plane scoring fall back to the configured part size."));
        }
        else if (!blockFit.MeetsRequiredMargin)
        {
            notes.Add(Invariant($"Mold block margin is {blockFit.MinimumMarginMm:0.###} mm on {blockFit.MinimumMarginSide}, below the required {blockFit.RequiredMarginMm:0.###} mm (moldSystem.moldMarginMm): thin steel there."));
        }
        else
        {
            notes.Add(Invariant($"Mold block margin around the cavity: at least {blockFit.MinimumMarginMm:0.###} mm ({blockFit.MinimumMarginSide}); required {blockFit.RequiredMarginMm:0.###} mm (moldSystem.moldMarginMm)."));
        }

        var axis = input.PartingOverride?.Axis ?? automaticPlane.Axis;
        var offset = input.PartingOverride?.OffsetMm ?? Convert.ToDecimal(automaticPlane.PlaneOffsetMm);
        var source = input.PartingOverride is null ? "Auto (bounding-box centre)" : "Manual";
        var search = PartingPlaneSearch.Search(triangles, reference: (ToSearchAxis(axis), (double)offset));

        if (input.PartingByGeometry)
        {
            axis = ToPartingAxis(search.Best.Axis);
            offset = Round(search.Best.OffsetMm);
            source = "Geometry";
        }

        notes.AddRange(PartingNotes(search, axis, offset, source));

        // The split follows the cavity: same relative height after scaling about the centre.
        var axisCenter = (decimal)(axis switch
        {
            CorePartingAxis.X => center.X,
            CorePartingAxis.Y => center.Y,
            _ => center.Z
        });
        var moldOffset = Math.Round(axisCenter + ((offset - axisCenter) * scale), 4);

        if (moldOffset != offset)
        {
            notes.Add(Invariant($"Mold split at {axis} = {moldOffset:0.####} mm: the part split at {offset:0.####} mm, moved with the scaled cavity."));
        }

        return new GeneratorMoldPlan(
            triangles,
            partBounds,
            blockFit,
            axis,
            offset,
            source,
            search,
            notes,
            shrinkage,
            scale,
            center,
            cavityTriangles,
            cavityBounds,
            moldOffset);
    }

    /// <summary>Every vertex moved to centre + (v - centre) * factor.</summary>
    public static IReadOnlyList<MeshTriangle> ScaleAbout(IReadOnlyList<MeshTriangle> triangles, MeshVertex center, double factor)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        MeshVertex Scale(MeshVertex v) => new(
            center.X + ((v.X - center.X) * factor),
            center.Y + ((v.Y - center.Y) * factor),
            center.Z + ((v.Z - center.Z) * factor));

        var scaled = new MeshTriangle[triangles.Count];

        for (var i = 0; i < triangles.Count; i++)
        {
            scaled[i] = new MeshTriangle(Scale(triangles[i].A), Scale(triangles[i].B), Scale(triangles[i].C));
        }

        return scaled;
    }

    private static MoldSeparationBounds ToBounds(MeshBounds bounds) => new(
        Round(bounds.Min.X),
        Round(bounds.Min.Y),
        Round(bounds.Min.Z),
        Round(bounds.Max.X),
        Round(bounds.Max.Y),
        Round(bounds.Max.Z));

    private static IEnumerable<string> PartingNotes(
        PartingPlaneSearchResult search,
        CorePartingAxis axis,
        decimal offset,
        string source)
    {
        var best = search.Best;
        yield return Invariant($"Parting plane by geometry: {best.Axis} = {best.OffsetMm:0.###} mm ({best.Source.ToLowerInvariant()}): {Percent(best.TrappedAreaMm2, search.TotalAreaMm2)} of the surface trapped, {Percent(best.UndraftedAreaMm2, search.TotalAreaMm2)} with less than 1 deg of draft.");

        if (source == "Geometry")
        {
            yield return "Split by geometry (parting.mode Geometry).";
            yield break;
        }

        var used = search.Reference!;
        yield return Invariant($"Plane in use: {axis} = {offset:0.###} mm ({source}): {Percent(used.TrappedAreaMm2, search.TotalAreaMm2)} trapped, {Percent(used.UndraftedAreaMm2, search.TotalAreaMm2)} with less than 1 deg of draft.");

        if (used.TrappedAreaMm2 > best.TrappedAreaMm2 + search.ToleranceMm2)
        {
            yield return "Set \"parting\": { \"mode\": \"Geometry\" } in the project config to split at the plane found by geometry.";
        }
    }

    private static SeparationPartingAxis ToSearchAxis(CorePartingAxis axis) => axis switch
    {
        CorePartingAxis.X => SeparationPartingAxis.X,
        CorePartingAxis.Y => SeparationPartingAxis.Y,
        CorePartingAxis.Z => SeparationPartingAxis.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
    };

    private static CorePartingAxis ToPartingAxis(SeparationPartingAxis axis) => axis switch
    {
        SeparationPartingAxis.X => CorePartingAxis.X,
        SeparationPartingAxis.Y => CorePartingAxis.Y,
        SeparationPartingAxis.Z => CorePartingAxis.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
    };

    private static string Percent(double part, double total) =>
        Invariant($"{(total > 0 ? 100 * part / total : 0):0.0} %");

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
