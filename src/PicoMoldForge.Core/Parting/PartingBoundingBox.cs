namespace PicoMoldForge.Core.Parting;

public sealed record PartingBoundingBox(
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ)
{
    public float SizeX => MaxX - MinX;

    public float SizeY => MaxY - MinY;

    public float SizeZ => MaxZ - MinZ;

    public float CenterX => MinX + (SizeX / 2.0f);

    public float CenterY => MinY + (SizeY / 2.0f);

    public float CenterZ => MinZ + (SizeZ / 2.0f);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (SizeX <= 0)
        {
            errors.Add("Bounding box SizeX must be greater than zero.");
        }

        if (SizeY <= 0)
        {
            errors.Add("Bounding box SizeY must be greater than zero.");
        }

        if (SizeZ <= 0)
        {
            errors.Add("Bounding box SizeZ must be greater than zero.");
        }

        return errors;
    }
}