using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.Verification;

/// <summary>
/// One piece of a triangle soup: triangles that share vertices. <paramref name="VolumeMm3"/> is the signed volume it
/// encloses (positive for a solid with outward triangles, negative for a void inside one).
/// </summary>
public sealed record MeshPiece(int Triangles, double VolumeMm3, MeshVertex Min, MeshVertex Max);

/// <summary>
/// Topology of a triangle soup, as a binary STL holds it. Vertices with equal coordinates are welded, then each edge
/// is counted by the triangles that use it:
/// <list type="bullet">
/// <item>On a closed 2-manifold surface every edge lies in exactly two triangles.</item>
/// <item><paramref name="OpenEdges"/> lie in one triangle: a hole in the surface.</item>
/// <item><paramref name="NonManifoldEdges"/> lie in three or more: sheets that pinch together.</item>
/// </list>
/// <paramref name="PieceList"/> holds the groups of triangles that share vertices, largest volume first.
/// <paramref name="NonManifoldNear"/> gives up to four places of non-manifold edges, at least 10 mm apart.
/// </summary>
public sealed record MeshIntegrity(
    int Triangles,
    int OpenEdges,
    int NonManifoldEdges,
    IReadOnlyList<MeshPiece> PieceList,
    IReadOnlyList<MeshVertex> NonManifoldNear)
{
    public int Pieces => PieceList.Count;

    public static MeshIntegrity Measure(IReadOnlyList<MeshTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        var topology = Topology.Of(triangles);
        var open = 0;
        var nonManifold = 0;
        var near = new List<MeshVertex>();

        foreach (var (key, count) in topology.Edges)
        {
            if (count == 1)
            {
                open++;
            }
            else if (count > 2)
            {
                nonManifold++;
                var v = topology.Positions[key.Low];

                if (near.Count < 4 && near.All(p => Distance(p, v) >= 10))
                {
                    near.Add(v);
                }
            }
        }

        return new MeshIntegrity(triangles.Count, open, nonManifold, topology.PieceList(triangles), near);
    }

    private static double Distance(MeshVertex a, MeshVertex b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>Welded vertices, edge counts and the piece of each triangle.</summary>
    internal sealed class Topology
    {
        private Topology(List<MeshVertex> positions, Dictionary<(int Low, int High), int> edges, int[] pieceOfTriangle)
        {
            Positions = positions;
            Edges = edges;
            PieceOfTriangle = pieceOfTriangle;
        }

        public List<MeshVertex> Positions { get; }

        public Dictionary<(int Low, int High), int> Edges { get; }

        /// <summary>A label per triangle; triangles of one piece share it.</summary>
        public int[] PieceOfTriangle { get; }

        public static Topology Of(IReadOnlyList<MeshTriangle> triangles)
        {
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

            // (low, high) vertex ids; a tuple key hashes both ids together (a packed long hashes to low ^ high, which
            // collides for the close ids of neighbouring vertices).
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

            var pieceOfTriangle = new int[triangles.Count];

            for (var i = 0; i < triangles.Count; i++)
            {
                pieceOfTriangle[i] = Find(faces[3 * i]);
            }

            return new Topology(positions, edges, pieceOfTriangle);
        }

        /// <summary>The pieces with their volume and bounding box, largest absolute volume first.</summary>
        public IReadOnlyList<MeshPiece> PieceList(IReadOnlyList<MeshTriangle> triangles)
        {
            var pieces = new Dictionary<int, (int Count, double Volume, double X0, double Y0, double Z0, double X1, double Y1, double Z1)>();

            for (var i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                var volume = SignedVolume(t);
                var (x0, y0, z0) = (Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)), Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)), Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)));
                var (x1, y1, z1) = (Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)), Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)), Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z)));

                pieces[PieceOfTriangle[i]] = pieces.TryGetValue(PieceOfTriangle[i], out var p)
                    ? (p.Count + 1, p.Volume + volume, Math.Min(p.X0, x0), Math.Min(p.Y0, y0), Math.Min(p.Z0, z0), Math.Max(p.X1, x1), Math.Max(p.Y1, y1), Math.Max(p.Z1, z1))
                    : (1, volume, x0, y0, z0, x1, y1, z1);
            }

            return pieces.Values
                .Select(p => new MeshPiece(p.Count, p.Volume, new MeshVertex(p.X0, p.Y0, p.Z0), new MeshVertex(p.X1, p.Y1, p.Z1)))
                .OrderByDescending(p => Math.Abs(p.VolumeMm3))
                .ThenByDescending(p => p.Triangles)
                .ToArray();
        }
    }

    internal static double SignedVolume(MeshTriangle t) =>
        ((t.A.X * ((t.B.Y * t.C.Z) - (t.B.Z * t.C.Y))) +
         (t.A.Y * ((t.B.Z * t.C.X) - (t.B.X * t.C.Z))) +
         (t.A.Z * ((t.B.X * t.C.Y) - (t.B.Y * t.C.X)))) / 6.0;
}

/// <summary>
/// Islands of a voxel-made solid: pieces that enclose less than a given volume (one voxel, for the halves). They are
/// what the voxels leave where a face of the part lies within mesh noise of the parting plane, as on the monoblock:
/// the crankcase ceiling of its cavity wanders from Z 49.91 to 50.07 mm about the plane at 49.978 mm, and where it
/// rises above the voxel layer at 50 mm that node is steel of the moving half, cut off from the rest. The largest piece
/// is always kept; any other piece of at least the given volume is kept too (a real loose piece of steel is a fault
/// the verification must see).
/// </summary>
public static class MeshIslands
{
    public static (IReadOnlyList<MeshTriangle> Kept, int Removed, double RemovedVolumeMm3) Remove(IReadOnlyList<MeshTriangle> triangles, double maximumIslandVolumeMm3)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        if (triangles.Count == 0)
        {
            return (triangles, 0, 0);
        }

        var topology = MeshIntegrity.Topology.Of(triangles);
        var volumes = new Dictionary<int, double>();

        for (var i = 0; i < triangles.Count; i++)
        {
            var piece = topology.PieceOfTriangle[i];
            volumes[piece] = volumes.GetValueOrDefault(piece) + MeshIntegrity.SignedVolume(triangles[i]);
        }

        if (volumes.Count == 1)
        {
            return (triangles, 0, 0);
        }

        var largest = volumes.MaxBy(v => Math.Abs(v.Value)).Key;
        var islands = volumes
            .Where(v => v.Key != largest && Math.Abs(v.Value) < maximumIslandVolumeMm3)
            .Select(v => v.Key)
            .ToHashSet();

        if (islands.Count == 0)
        {
            return (triangles, 0, 0);
        }

        var kept = new List<MeshTriangle>(triangles.Count);

        for (var i = 0; i < triangles.Count; i++)
        {
            if (!islands.Contains(topology.PieceOfTriangle[i]))
            {
                kept.Add(triangles[i]);
            }
        }

        return (kept, islands.Count, islands.Sum(k => Math.Abs(volumes[k])));
    }
}
