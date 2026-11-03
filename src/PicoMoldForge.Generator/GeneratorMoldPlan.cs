using System.Globalization;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Parting;
using CorePartingAxis = PicoMoldForge.Core.Parting.PartingAxis;
using SeparationPartingAxis = PicoMoldForge.Core.Engineering.Separation.PartingAxis;

namespace PicoMoldForge.Generator;

/// <summary>
/// Layout of the mold for one generator run, computed once from the input STL (pure .NET, no PicoGK runtime) and
/// shared by the geometry steps and the report, so they all use the same part and the same split.
/// <paramref name="PartBounds"/> are measured on the STL; <paramref name="SplitAxis"/> and
/// <paramref name="PartingOffsetMm"/> are the split used for the mold halves.
/// </summary>
public sealed record GeneratorMoldPlan(
    IReadOnlyList<MeshTriangle> Triangles,
    MoldSeparationBounds PartBounds,
    MoldBlockFitSummary BlockFit,
    CorePartingAxis SplitAxis,
    decimal PartingOffsetMm,
    string PartingSource,
    IReadOnlyList<string> Notes)
{
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
        var partBounds = new MoldSeparationBounds(
            Round(bounds.Min.X),
            Round(bounds.Min.Y),
            Round(bounds.Min.Z),
            Round(bounds.Max.X),
            Round(bounds.Max.Y),
            Round(bounds.Max.Z));
        var block = input.BooleanMoldBlockBounds;
        var blockFit = MoldBlockFitChecker.Check(
            new MoldSeparationBounds(block.MinXmm, block.MinYmm, block.MinZmm, block.MaxXmm, block.MaxYmm, block.MaxZmm),
            partBounds,
            input.MoldSystem.MoldMarginMm);

        var notes = new List<string>
        {
            Invariant($"Part bounds measured on the input STL: X {partBounds.MinXmm:0.###} to {partBounds.MaxXmm:0.###}, Y {partBounds.MinYmm:0.###} to {partBounds.MaxYmm:0.###}, Z {partBounds.MinZmm:0.###} to {partBounds.MaxZmm:0.###} mm ({partBounds.SizeXmm:0.###} x {partBounds.SizeYmm:0.###} x {partBounds.SizeZmm:0.###} mm).")
        };

        if (!blockFit.IsPartInsideBlock)
        {
            notes.Add(blockFit.MinimumMarginMm < 0m
                ? Invariant($"FAIL: the part sticks out of the mold block by {-blockFit.MinimumMarginMm:0.###} mm on {blockFit.MinimumMarginSide}; the mold halves are not valid. Separation and parting-plane scoring fall back to the configured part size.")
                : Invariant($"FAIL: the part touches the mold block face {blockFit.MinimumMarginSide}; the cavity would be open there. Separation and parting-plane scoring fall back to the configured part size."));
        }
        else if (!blockFit.MeetsRequiredMargin)
        {
            notes.Add(Invariant($"Mold block margin is {blockFit.MinimumMarginMm:0.###} mm on {blockFit.MinimumMarginSide}, below the required {blockFit.RequiredMarginMm:0.###} mm (moldSystem.moldMarginMm): thin steel there."));
        }
        else
        {
            notes.Add(Invariant($"Mold block margin: at least {blockFit.MinimumMarginMm:0.###} mm ({blockFit.MinimumMarginSide}); required {blockFit.RequiredMarginMm:0.###} mm (moldSystem.moldMarginMm)."));
        }

        var axis = input.PartingOverride?.Axis ?? automaticPlane.Axis;
        var offset = input.PartingOverride?.OffsetMm ?? Convert.ToDecimal(automaticPlane.PlaneOffsetMm);
        var source = input.PartingOverride is null ? "Auto (bounding-box centre)" : "Manual";

        return new GeneratorMoldPlan(triangles, partBounds, blockFit, axis, offset, source, notes);
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 4);

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
