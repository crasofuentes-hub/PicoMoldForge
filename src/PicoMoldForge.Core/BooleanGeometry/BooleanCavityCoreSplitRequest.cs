using PicoMoldForge.Core.Parting;

namespace PicoMoldForge.Core.BooleanGeometry;

public sealed record BooleanCavityCoreSplitRequest(
    string SourcePath,
    string OutputDirectory,
    MoldBlockBounds BlockBounds,
    PartingAxis SplitAxis,
    decimal PartingPlaneOffsetMm,
    float VoxelSizeMm = 1.0f)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            errors.Add("SourcePath is required.");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            errors.Add("OutputDirectory is required.");
        }

        if (VoxelSizeMm <= 0)
        {
            errors.Add("VoxelSizeMm must be greater than zero.");
        }

        foreach (var blockError in BlockBounds.Validate())
        {
            errors.Add($"BlockBounds: {blockError}");
        }

        var minimum = GetAxisMinimum(BlockBounds, SplitAxis);
        var maximum = GetAxisMaximum(BlockBounds, SplitAxis);

        if (PartingPlaneOffsetMm <= minimum || PartingPlaneOffsetMm >= maximum)
        {
            errors.Add("PartingPlaneOffsetMm must be inside the mold block bounds for the selected SplitAxis.");
        }

        return errors;
    }

    private static decimal GetAxisMinimum(MoldBlockBounds bounds, PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => bounds.MinXmm,
            PartingAxis.Y => bounds.MinYmm,
            PartingAxis.Z => bounds.MinZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static decimal GetAxisMaximum(MoldBlockBounds bounds, PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => bounds.MaxXmm,
            PartingAxis.Y => bounds.MaxYmm,
            PartingAxis.Z => bounds.MaxZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }
}