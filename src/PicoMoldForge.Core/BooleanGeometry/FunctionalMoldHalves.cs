using PicoMoldForge.Core.Parting;

namespace PicoMoldForge.Core.BooleanGeometry;

/// <summary>
/// A straight feature of the functional mold halves: a cylinder from (StartX, StartY, StartZ) to (EndX, EndY, EndZ),
/// mm. It belongs to <paramref name="System"/> (Feed, EjectorPins, Cooling, GuideBores, GuidePins) and to half
/// <paramref name="Half"/> (+1, -1, or 0 for both, as the runner and gate on the parting plane).
/// <paramref name="RoundEnds"/> caps it with half spheres; <paramref name="AddsSteel"/> adds it to the half (a guide
/// pin) instead of cutting it.
/// </summary>
public sealed record MoldHalfFeature(
    string Id,
    string System,
    int Half,
    double StartXmm,
    double StartYmm,
    double StartZmm,
    double EndXmm,
    double EndYmm,
    double EndZmm,
    double RadiusMm,
    bool RoundEnds,
    bool AddsSteel);

/// <summary>
/// The two functional mold halves: each half of the block minus the cavity, minus the features of its systems, plus
/// its guide pins. <paramref name="MovingHalf"/> is +1 when the half on the positive side of the split moves.
/// </summary>
public sealed record FunctionalMoldHalvesRequest(
    string CavitySourcePath,
    string OutputDirectory,
    MoldBlockBounds BlockBounds,
    PartingAxis SplitAxis,
    decimal PartingPlaneOffsetMm,
    int MovingHalf,
    IReadOnlyList<MoldHalfFeature> Features,
    float VoxelSizeMm)
{
    public const string MovingHalfFileName = "MovingHalf.stl";

    public const string FixedHalfFileName = "FixedHalf.stl";

    /// <summary>Systems in the order they are cut; guide pins are added last.</summary>
    public static readonly IReadOnlyList<string> SystemOrder = new[] { "Feed", "EjectorPins", "Cooling", "GuideBores", "GuidePins" };

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CavitySourcePath))
        {
            errors.Add("CavitySourcePath is required.");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            errors.Add("OutputDirectory is required.");
        }

        if (BlockBounds is null)
        {
            errors.Add("BlockBounds is required.");
        }
        else
        {
            errors.AddRange(BlockBounds.Validate());
        }

        if (MovingHalf != 1 && MovingHalf != -1)
        {
            errors.Add("MovingHalf must be +1 or -1.");
        }

        if (!(VoxelSizeMm > 0))
        {
            errors.Add("VoxelSizeMm must be greater than zero.");
        }

        foreach (var feature in Features ?? Array.Empty<MoldHalfFeature>())
        {
            if (!(feature.RadiusMm > 0))
            {
                errors.Add($"Feature {feature.Id} must have a positive radius.");
            }

            if (feature.Half is < -1 or > 1)
            {
                errors.Add($"Feature {feature.Id} must belong to half +1, -1 or 0.");
            }

            if (!SystemOrder.Contains(feature.System))
            {
                errors.Add($"Feature {feature.Id} has an unknown system '{feature.System}'.");
            }
        }

        return errors;
    }
}

/// <summary>Steel a system removed from (or added to) one half, measured on the voxels, mm3.</summary>
public sealed record MoldHalfSystemVolume(string System, double RemovedVolumeMm3, double AddedVolumeMm3);

/// <summary>
/// One functional half: its file, its volume after the cavity is cut and at the end, what each system changed, and its
/// extent along the split axis (to see what crosses the parting plane).
/// </summary>
public sealed record FunctionalMoldHalfResult(
    string Role,
    int Half,
    string OutputPath,
    double VolumeAfterCavityMm3,
    double FinalVolumeMm3,
    IReadOnlyList<MoldHalfSystemVolume> Systems,
    double MinAlongAxisMm,
    double MaxAlongAxisMm,
    int TriangleCount);

/// <summary>
/// The two functional halves and the steel they share (<paramref name="OverlapVolumeMm3"/>, ideally zero: the halves
/// would collide where they overlap).
/// </summary>
public sealed record FunctionalMoldHalvesResult(
    FunctionalMoldHalfResult Moving,
    FunctionalMoldHalfResult Fixed,
    double OverlapVolumeMm3,
    double VoxelSizeMm,
    IReadOnlyList<string> Warnings);
