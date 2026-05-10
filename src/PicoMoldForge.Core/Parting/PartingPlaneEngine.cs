using PicoMoldForge.Core.Analysis;

namespace PicoMoldForge.Core.Parting;

public sealed class PartingPlaneEngine
{
    public PartingPlaneResult CalculateAutomatic(PartingBoundingBox boundingBox)
    {
        var validationErrors = boundingBox.Validate();

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid bounding box: " + string.Join(" ", validationErrors),
                nameof(boundingBox));
        }

        var axis = SelectDominantAxis(boundingBox);
        var openingDirection = CreateOpeningDirection(axis);
        var planeOffset = GetCenterOffset(boundingBox, axis);

        var warnings = new List<string>
        {
            "Automatic parting plane is preliminary and based only on dominant bounding-box axis.",
            "This result does not evaluate undercuts, draft angles, side actions, inserts, or manufacturability."
        };

        if (HasNearTie(boundingBox))
        {
            warnings.Add("Bounding-box dimensions are tied or near-tied; deterministic tie-break order X > Y > Z was applied.");
        }

        return new PartingPlaneResult(
            PartingPlaneMode.Automatic,
            axis,
            openingDirection,
            planeOffset,
            "Dominant bounding-box axis with center-plane placement.",
            warnings);
    }

    private static PartingAxis SelectDominantAxis(PartingBoundingBox boundingBox)
    {
        if (boundingBox.SizeX >= boundingBox.SizeY && boundingBox.SizeX >= boundingBox.SizeZ)
        {
            return PartingAxis.X;
        }

        if (boundingBox.SizeY >= boundingBox.SizeZ)
        {
            return PartingAxis.Y;
        }

        return PartingAxis.Z;
    }

    private static OpeningDirection3 CreateOpeningDirection(PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => new OpeningDirection3(1.0f, 0.0f, 0.0f),
            PartingAxis.Y => new OpeningDirection3(0.0f, 1.0f, 0.0f),
            PartingAxis.Z => new OpeningDirection3(0.0f, 0.0f, 1.0f),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static float GetCenterOffset(PartingBoundingBox boundingBox, PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => boundingBox.CenterX,
            PartingAxis.Y => boundingBox.CenterY,
            PartingAxis.Z => boundingBox.CenterZ,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static bool HasNearTie(PartingBoundingBox boundingBox)
    {
        const float tolerance = 0.0001f;

        return Math.Abs(boundingBox.SizeX - boundingBox.SizeY) <= tolerance ||
               Math.Abs(boundingBox.SizeX - boundingBox.SizeZ) <= tolerance ||
               Math.Abs(boundingBox.SizeY - boundingBox.SizeZ) <= tolerance;
    }
}