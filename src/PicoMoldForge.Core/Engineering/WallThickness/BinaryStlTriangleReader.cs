namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>
/// Reads the triangles of a binary STL file (80-byte header, uint32 count, 50 bytes per triangle). The stored
/// normal is ignored: geometry code takes the normal from the vertex order. No PicoGK runtime is needed.
/// </summary>
public static class BinaryStlTriangleReader
{
    private const int HeaderBytes = 84;
    private const int TriangleBytes = 50;

    public static IReadOnlyList<MeshTriangle> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("STL path is required.", nameof(path));
        }

        var resolvedPath = Path.GetFullPath(path);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("STL file was not found.", resolvedPath);
        }

        using var stream = File.OpenRead(resolvedPath);
        return Read(stream);
    }

    public static IReadOnlyList<MeshTriangle> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        var header = reader.ReadBytes(80);

        if (header.Length != 80)
        {
            throw new InvalidDataException("Binary STL is shorter than its 84-byte header.");
        }

        var count = reader.ReadUInt32();

        if (stream.CanSeek && stream.Length != HeaderBytes + (TriangleBytes * (long)count))
        {
            throw new InvalidDataException(FormattableString.Invariant(
                $"Binary STL declares {count} triangles ({HeaderBytes + (TriangleBytes * (long)count)} bytes) but has {stream.Length} bytes."));
        }

        var triangles = new MeshTriangle[count];

        for (var i = 0; i < count; i++)
        {
            reader.ReadBytes(12);
            var a = ReadVertex(reader);
            var b = ReadVertex(reader);
            var c = ReadVertex(reader);
            reader.ReadUInt16();
            triangles[i] = new MeshTriangle(a, b, c);
        }

        return triangles;
    }

    private static MeshVertex ReadVertex(BinaryReader reader)
    {
        var x = reader.ReadSingle();
        var y = reader.ReadSingle();
        var z = reader.ReadSingle();

        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            throw new InvalidDataException("Binary STL contains a non-finite vertex coordinate.");
        }

        return new MeshVertex(x, y, z);
    }
}
