namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>
/// Writes triangles as a binary STL (80-byte header, uint32 count, 50 bytes per triangle). The stored normal is the
/// right-hand normal of the vertex order (zero for a degenerate triangle); coordinates are written as float32.
/// </summary>
public static class BinaryStlTriangleWriter
{
    public static void Write(string path, IReadOnlyList<MeshTriangle> triangles, string header = "PicoMoldForge")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("STL path is required.", nameof(path));
        }

        var resolvedPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(resolvedPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(resolvedPath);
        Write(stream, triangles, header);
    }

    public static void Write(Stream stream, IReadOnlyList<MeshTriangle> triangles, string header = "PicoMoldForge")
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(triangles);

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        var headerBytes = new byte[80];
        var text = System.Text.Encoding.ASCII.GetBytes(header ?? string.Empty);
        Array.Copy(text, headerBytes, Math.Min(text.Length, headerBytes.Length));
        writer.Write(headerBytes);
        writer.Write((uint)triangles.Count);

        foreach (var t in triangles)
        {
            var ux = t.B.X - t.A.X;
            var uy = t.B.Y - t.A.Y;
            var uz = t.B.Z - t.A.Z;
            var vx = t.C.X - t.A.X;
            var vy = t.C.Y - t.A.Y;
            var vz = t.C.Z - t.A.Z;
            var nx = (uy * vz) - (uz * vy);
            var ny = (uz * vx) - (ux * vz);
            var nz = (ux * vy) - (uy * vx);
            var length = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
            var scale = length > 0 ? 1.0 / length : 0.0;

            writer.Write((float)(nx * scale));
            writer.Write((float)(ny * scale));
            writer.Write((float)(nz * scale));
            WriteVertex(writer, t.A);
            WriteVertex(writer, t.B);
            WriteVertex(writer, t.C);
            writer.Write((ushort)0);
        }
    }

    private static void WriteVertex(BinaryWriter writer, MeshVertex v)
    {
        writer.Write((float)v.X);
        writer.Write((float)v.Y);
        writer.Write((float)v.Z);
    }
}
