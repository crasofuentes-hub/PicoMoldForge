using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.Verification;

/// <summary>
/// Topology of a triangle soup, as a binary STL holds it. Vertices with equal coordinates are welded, then each edge
/// is counted by the triangles that use it:
/// <list type="bullet">
/// <item>On a closed 2-manifold surface every edge lies in exactly two triangles.</item>
/// <item><paramref name="OpenEdges"/> lie in one triangle: a hole in the surface.</item>
/// <item><paramref name="NonManifoldEdges"/> lie in three or more: sheets that pinch together.</item>
/// </list>
/// <paramref name="Pieces"/> are the groups of triangles that share vertices. <paramref name="NonManifoldNear"/>
/// gives up to four places of non-manifold edges, at least 10 mm apart.
/// </summary>
public sealed record MeshIntegrity(
    int Triangles,
    int OpenEdges,
    int NonManifoldEdges,
    int Pieces,
    IReadOnlyList<MeshVertex> NonManifoldNear)
{
    public static MeshIntegrity Measure(IReadOnlyList<MeshTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        var ids = new Dictionary<MeshVertex, int>(Math.Max(16, triangles.Count / 2));
        var positions = new List<MeshVertex>(Math.Max(16, triangles.Count / 2));
        var faces = new int[triangles.Count * 3];

        int Id(MeshVertex v)
        {
            if (!ids.TryGetValue(v, out var id))
            {
                id = positions.Count;
                ids.Add(v, id);
                positions.Add(v);
            }

            return id;
        }

        for (var i = 0; i < triangles.Count; i++)
        {
            faces[3 * i] = Id(triangles[i].A);
            faces[(3 * i) + 1] = Id(triangles[i].B);
            faces[(3 * i) + 2] = Id(triangles[i].C);
        }

        // (low, high) vertex ids; a tuple key hashes both ids together (a packed long hashes to low ^ high, which collides
        // for the close ids of neighbouring vertices).
        var edges = new Dictionary<(int Low, int High), int>(triangles.Count * 2);
        var parent = Enumerable.Range(0, positions.Count).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        for (var i = 0; i < triangles.Count; i++)
        {
            for (var k = 0; k < 3; k++)
            {
                var a = faces[(3 * i) + k];
                var b = faces[(3 * i) + ((k + 1) % 3)];
                parent[Find(a)] = Find(b);

                // A collapsed edge (both ends welded together) bounds nothing.
                if (a != b)
                {
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    edges[key] = edges.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            }
        }

        var open = 0;
        var nonManifold = 0;
        var near = new List<MeshVertex>();

        foreach (var (key, count) in edges)
        {
            if (count == 1)
            {
                open++;
            }
            else if (count > 2)
            {
                nonManifold++;
                var v = positions[key.Low];

                if (near.Count < 4 && near.All(p => Distance(p, v) >= 10))
                {
                    near.Add(v);
                }
            }
        }

        var pieces = triangles.Count == 0 ? 0 : faces.Select(Find).Distinct().Count();
        return new MeshIntegrity(triangles.Count, open, nonManifold, pieces, near);
    }

    private static double Distance(MeshVertex a, MeshVertex b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
