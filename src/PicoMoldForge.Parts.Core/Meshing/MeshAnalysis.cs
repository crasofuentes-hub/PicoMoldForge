using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Meshing;

/// <summary>Topology and geometry measured from a mesh (independent of how it was produced).</summary>
public sealed record MeshReport(
    int Vertices,
    int Triangles,
    Vec3 BoundsMin,
    Vec3 BoundsMax,
    double VolumeMm3,
    double AreaMm2,
    int BoundaryEdges,
    int NonManifoldEdges,
    int InconsistentEdges,
    int NonManifoldVertices,
    int DegenerateTriangles,
    int EulerCharacteristic,
    int Components,
    int? Genus)
{
    /// <summary>Every edge is shared by exactly two triangles.</summary>
    public bool Closed => BoundaryEdges == 0 && NonManifoldEdges == 0;

    /// <summary>Closed and every vertex has a single fan of triangles around it.</summary>
    public bool Manifold => Closed && NonManifoldVertices == 0;

    /// <summary>Each shared edge is used in opposite directions by its two triangles.</summary>
    public bool ConsistentlyOriented => InconsistentEdges == 0;

    /// <summary>Positive enclosed volume: triangle normals point outside.</summary>
    public bool OutwardNormals => VolumeMm3 > 0;

    public bool Watertight => Manifold && ConsistentlyOriented && OutwardNormals;
}

public static class MeshAnalysis
{
    public static MeshReport Analyze(TriangleMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var indices = mesh.Indices;
        var vertices = mesh.Vertices;
        var triangleCount = mesh.TriangleCount;
        var used = new bool[vertices.Length];

        foreach (var index in indices)
        {
            if ((uint)index >= (uint)vertices.Length)
            {
                throw new ArgumentException("Mesh index out of range.", nameof(mesh));
            }

            used[index] = true;
        }

        var usedCount = used.Count(u => u);
        var min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = new Vec3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

        for (var i = 0; i < vertices.Length; i++)
        {
            if (used[i])
            {
                min = Vec3.Min(min, vertices[i]);
                max = Vec3.Max(max, vertices[i]);
            }
        }

        if (usedCount == 0)
        {
            min = Vec3.Zero;
            max = Vec3.Zero;
        }

        var center = (min + max) * 0.5;
        var volume6 = 0.0;
        var area2 = 0.0;
        var degenerate = 0;

        for (var t = 0; t < triangleCount; t++)
        {
            var a = vertices[indices[3 * t]] - center;
            var b = vertices[indices[(3 * t) + 1]] - center;
            var c = vertices[indices[(3 * t) + 2]] - center;
            volume6 += Vec3.Dot(a, Vec3.Cross(b, c));
            var cross = Vec3.Cross(b - a, c - a).Length;
            area2 += cross;

            if (cross <= 1e-18)
            {
                degenerate++;
            }
        }

        // Directed edges sorted by undirected key; direction flag says whether the edge runs low -> high.
        var edgeCount = indices.Length;
        var keys = new ulong[edgeCount];
        var forward = new bool[edgeCount];

        for (var t = 0; t < triangleCount; t++)
        {
            for (var e = 0; e < 3; e++)
            {
                var from = indices[(3 * t) + e];
                var to = indices[(3 * t) + ((e + 1) % 3)];
                var low = Math.Min(from, to);
                var high = Math.Max(from, to);
                keys[(3 * t) + e] = ((ulong)(uint)low << 32) | (uint)high;
                forward[(3 * t) + e] = from < to;
            }
        }

        Array.Sort(keys, forward);

        var uniqueEdges = 0;
        var boundary = 0;
        var nonManifold = 0;
        var inconsistent = 0;

        for (var i = 0; i < edgeCount;)
        {
            var j = i + 1;

            while (j < edgeCount && keys[j] == keys[i])
            {
                j++;
            }

            var count = j - i;
            uniqueEdges++;

            if (count == 1)
            {
                boundary++;
            }
            else if (count > 2)
            {
                nonManifold++;
            }
            else if (forward[i] == forward[i + 1])
            {
                inconsistent++;
            }

            i = j;
        }

        var nonManifoldVertices = CountNonManifoldVertices(vertices.Length, indices, triangleCount);
        var components = CountComponents(vertices.Length, indices, used);
        var euler = usedCount - uniqueEdges + triangleCount;
        int? genus = null;

        if (boundary == 0 && nonManifold == 0 && nonManifoldVertices == 0 && inconsistent == 0 && ((2 * components) - euler) % 2 == 0)
        {
            genus = ((2 * components) - euler) / 2;
        }

        return new MeshReport(
            usedCount,
            triangleCount,
            min,
            max,
            volume6 / 6.0,
            area2 / 2.0,
            boundary,
            nonManifold,
            inconsistent,
            nonManifoldVertices,
            degenerate,
            euler,
            components,
            genus);
    }

    /// <summary>Counts vertices whose incident triangles do not form a single fan (pinched vertices).</summary>
    private static int CountNonManifoldVertices(int vertexCount, int[] indices, int triangleCount)
    {
        var degree = new int[vertexCount + 1];

        foreach (var index in indices)
        {
            degree[index + 1]++;
        }

        for (var i = 0; i < vertexCount; i++)
        {
            degree[i + 1] += degree[i];
        }

        var start = degree;
        var fill = new int[vertexCount];
        var linkFrom = new int[indices.Length];
        var linkTo = new int[indices.Length];

        for (var t = 0; t < triangleCount; t++)
        {
            for (var e = 0; e < 3; e++)
            {
                var v = indices[(3 * t) + e];
                var next = indices[(3 * t) + ((e + 1) % 3)];
                var previous = indices[(3 * t) + ((e + 2) % 3)];
                var slot = start[v] + fill[v]++;
                linkFrom[slot] = next;
                linkTo[slot] = previous;
            }
        }

        var bad = 0;

        for (var v = 0; v < vertexCount; v++)
        {
            var s = start[v];
            var count = start[v + 1] - s;

            if (count == 0)
            {
                continue;
            }

            // Follow the link cycle starting at the first entry: from -> to, then find the entry whose from == to.
            var visited = 1;
            var current = linkTo[s];
            var guard = 0;

            while (current != linkFrom[s] && guard++ < count)
            {
                var found = -1;

                for (var k = s; k < s + count; k++)
                {
                    if (linkFrom[k] == current)
                    {
                        found = k;
                        break;
                    }
                }

                if (found < 0)
                {
                    break;
                }

                visited++;
                current = linkTo[found];
            }

            if (visited != count || current != linkFrom[s])
            {
                bad++;
            }
        }

        return bad;
    }

    private static int CountComponents(int vertexCount, int[] indices, bool[] used)
    {
        var parent = new int[vertexCount];

        for (var i = 0; i < vertexCount; i++)
        {
            parent[i] = i;
        }

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        for (var t = 0; t < indices.Length; t += 3)
        {
            var a = Find(indices[t]);
            var b = Find(indices[t + 1]);
            var c = Find(indices[t + 2]);
            parent[b] = a;
            parent[Find(c)] = a;
        }

        var roots = new HashSet<int>();

        for (var i = 0; i < vertexCount; i++)
        {
            if (used[i])
            {
                roots.Add(Find(i));
            }
        }

        return roots.Count;
    }
}
