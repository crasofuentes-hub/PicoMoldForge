using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Meshing;

namespace PicoMoldForge.Parts.Export;

/// <param name="DroppedDegenerateTriangles">Triangles that repeated a vertex after welding (zero area); they carry no surface.</param>
public sealed record StlReadResult(TriangleMesh Mesh, int DroppedDegenerateTriangles);

/// <summary>
/// Reads a binary STL back into an indexed mesh so its topology can be measured: vertices with identical
/// coordinates are welded and triangles that repeat a vertex are dropped. Used to check meshes made by
/// other kernels (PicoGK), whose mesher emits a few zero-area triangles.
/// </summary>
public static class StlReader
{
    public static StlReadResult ReadBinary(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return ReadBinary(stream);
    }

    public static StlReadResult ReadBinary(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        var header = reader.ReadBytes(80);

        if (header.Length != 80)
        {
            throw new InvalidDataException("Not a binary STL: the 80-byte header is incomplete.");
        }

        var count = reader.ReadUInt32();

        if (stream.CanSeek && stream.Length != 84 + (50L * count))
        {
            throw new InvalidDataException($"Not a binary STL: {count} triangles need {84 + (50L * count)} bytes, the file has {stream.Length}.");
        }

        var welded = new Dictionary<(float X, float Y, float Z), int>();
        var vertices = new List<Vec3>();
        var indices = new List<int>((int)Math.Min(count * 3L, int.MaxValue));
        var corner = new int[3];
        var dropped = 0;

        for (var t = 0u; t < count; t++)
        {
            reader.ReadSingle();
            reader.ReadSingle();
            reader.ReadSingle();

            for (var k = 0; k < 3; k++)
            {
                var key = (reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                if (!welded.TryGetValue(key, out var index))
                {
                    index = vertices.Count;
                    welded.Add(key, index);
                    vertices.Add(new Vec3(key.Item1, key.Item2, key.Item3));
                }

                corner[k] = index;
            }

            reader.ReadUInt16();

            if (corner[0] == corner[1] || corner[1] == corner[2] || corner[0] == corner[2])
            {
                dropped++;
                continue;
            }

            indices.Add(corner[0]);
            indices.Add(corner[1]);
            indices.Add(corner[2]);
        }

        return new StlReadResult(new TriangleMesh(vertices.ToArray(), indices.ToArray()), dropped);
    }
}
