using System.Text;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Meshing;

namespace PicoMoldForge.Parts.Export;

/// <summary>Binary STL writer (millimeters). Deterministic: no timestamps in the header.</summary>
public static class StlWriter
{
    public static void Write(TriangleMesh mesh, string path, string headerText)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        Write(mesh, stream, headerText);
    }

    public static void Write(TriangleMesh mesh, Stream stream, string headerText)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[80];
        var text = Encoding.ASCII.GetBytes((headerText ?? string.Empty).Replace("solid", "SOLID", StringComparison.OrdinalIgnoreCase));
        Array.Copy(text, header, Math.Min(text.Length, 80));

        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(header);
        writer.Write((uint)mesh.TriangleCount);

        var vertices = mesh.Vertices;
        var indices = mesh.Indices;

        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var a = vertices[indices[3 * t]];
            var b = vertices[indices[(3 * t) + 1]];
            var c = vertices[indices[(3 * t) + 2]];
            var normal = Vec3.Cross(b - a, c - a).Normalized();
            WriteVector(writer, normal);
            WriteVector(writer, a);
            WriteVector(writer, b);
            WriteVector(writer, c);
            writer.Write((ushort)0);
        }
    }

    private static void WriteVector(BinaryWriter writer, Vec3 v)
    {
        writer.Write((float)v.X);
        writer.Write((float)v.Y);
        writer.Write((float)v.Z);
    }
}

/// <summary>
/// Compact preview mesh for the viewer: magic "PMFMESH1", uint32 version, uint32 vertex count,
/// uint32 triangle count, float32[3] bounds min, float32[3] bounds max, uint16[3] quantized positions per
/// vertex (relative to the bounds), uint32[3] indices per triangle. Little endian.
/// </summary>
public static class PreviewMeshWriter
{
    public const string Magic = "PMFMESH1";

    public static void Write(TriangleMesh mesh, string path)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = new Vec3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

        foreach (var v in mesh.Vertices)
        {
            min = Vec3.Min(min, v);
            max = Vec3.Max(max, v);
        }

        if (mesh.Vertices.Length == 0)
        {
            min = Vec3.Zero;
            max = Vec3.Zero;
        }

        var size = max - min;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(1u);
        writer.Write((uint)mesh.Vertices.Length);
        writer.Write((uint)mesh.TriangleCount);
        writer.Write((float)min.X);
        writer.Write((float)min.Y);
        writer.Write((float)min.Z);
        writer.Write((float)max.X);
        writer.Write((float)max.Y);
        writer.Write((float)max.Z);

        foreach (var v in mesh.Vertices)
        {
            writer.Write(Quantize(v.X, min.X, size.X));
            writer.Write(Quantize(v.Y, min.Y, size.Y));
            writer.Write(Quantize(v.Z, min.Z, size.Z));
        }

        foreach (var index in mesh.Indices)
        {
            writer.Write((uint)index);
        }
    }

    private static ushort Quantize(double value, double min, double size) =>
        size <= 0 ? (ushort)0 : (ushort)Math.Clamp(Math.Round((value - min) / size * 65535.0), 0, 65535);
}
