namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>Axis-aligned bounds of a surface mesh, in millimeters.</summary>
public readonly record struct MeshBounds(MeshVertex Min, MeshVertex Max)
{
    public double SizeX => Max.X - Min.X;

    public double SizeY => Max.Y - Min.Y;

    public double SizeZ => Max.Z - Min.Z;

    public MeshVertex Center => new((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2);

    /// <summary>Bounds of all triangle vertices.</summary>
    public static MeshBounds Of(IReadOnlyList<MeshTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        if (triangles.Count == 0)
        {
            throw new ArgumentException("At least one triangle is required.", nameof(triangles));
        }

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;

        void Include(MeshVertex v)
        {
            minX = Math.Min(minX, v.X);
            minY = Math.Min(minY, v.Y);
            minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X);
            maxY = Math.Max(maxY, v.Y);
            maxZ = Math.Max(maxZ, v.Z);
        }

        foreach (var t in triangles)
        {
            Include(t.A);
            Include(t.B);
            Include(t.C);
        }

        return new MeshBounds(new MeshVertex(minX, minY, minZ), new MeshVertex(maxX, maxY, maxZ));
    }
}
