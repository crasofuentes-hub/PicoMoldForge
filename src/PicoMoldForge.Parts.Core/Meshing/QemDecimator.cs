using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Meshing;

public sealed record DecimationOptions(double ToleranceMm, double MaxNormalChangeDeg = 40, double CentroidToleranceFactor = 2.0);

public sealed record DecimationStats(
    int TrianglesBefore,
    int TrianglesAfter,
    int Collapses,
    int RejectedTopology,
    int RejectedGeometry,
    int RejectedFidelity);

/// <summary>
/// Quadric error metric edge collapse (Garland-Heckbert) guided by the exact distance field: an edge only
/// collapses if the new vertex stays within the tolerance of the true surface and the centroids of every
/// affected triangle stay within CentroidToleranceFactor * tolerance. Collapses keep the mesh closed and
/// manifold (link condition, minimum valence) and never flip a triangle by more than MaxNormalChangeDeg.
/// The optimal position uses a pseudo-inverse of the quadric around the edge midpoint, so creases and corners
/// are preserved while flat and smooth regions collapse.
/// </summary>
public static class QemDecimator
{
    public static TriangleMesh Decimate(TriangleMesh mesh, ISdf sdf, DecimationOptions options, out DecimationStats stats)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(sdf);
        ArgumentNullException.ThrowIfNull(options);

        if (!(options.ToleranceMm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Tolerance must be positive.");
        }

        var state = new State(mesh, sdf, options);
        state.Run();
        stats = state.Stats();
        return state.Compact();
    }

    private sealed class State
    {
        private readonly ISdf _sdf;
        private readonly double _tolerance;
        private readonly double _centroidTolerance;
        private readonly double _minNormalDot;
        private readonly Vec3[] _positions;
        private readonly int[] _triangles;
        private readonly bool[] _triangleAlive;
        private readonly List<int>[] _vertexTriangles;
        private readonly double[] _quadrics;
        private readonly int[] _version;
        private readonly bool[] _vertexAlive;
        private readonly int[] _mark;
        private readonly PriorityQueue<(int U, int V, int VersionU, int VersionV), double> _heap = new();
        private readonly int _trianglesBefore;
        private int _stamp;
        private int _aliveTriangles;
        private int _collapses;
        private int _rejectedTopology;
        private int _rejectedGeometry;
        private int _rejectedFidelity;

        public State(TriangleMesh mesh, ISdf sdf, DecimationOptions options)
        {
            _sdf = sdf;
            _tolerance = options.ToleranceMm;
            _centroidTolerance = options.ToleranceMm * Math.Max(1.0, options.CentroidToleranceFactor);
            _minNormalDot = Math.Cos(options.MaxNormalChangeDeg * Math.PI / 180.0);
            _positions = (Vec3[])mesh.Vertices.Clone();
            _triangles = (int[])mesh.Indices.Clone();
            _trianglesBefore = mesh.TriangleCount;
            _aliveTriangles = mesh.TriangleCount;
            _triangleAlive = Enumerable.Repeat(true, mesh.TriangleCount).ToArray();
            _vertexTriangles = new List<int>[_positions.Length];
            _quadrics = new double[_positions.Length * 10];
            _version = new int[_positions.Length];
            _vertexAlive = new bool[_positions.Length];
            _mark = new int[_positions.Length];

            for (var v = 0; v < _positions.Length; v++)
            {
                _vertexTriangles[v] = new List<int>(6);
            }

            for (var t = 0; t < mesh.TriangleCount; t++)
            {
                for (var e = 0; e < 3; e++)
                {
                    var v = _triangles[(3 * t) + e];
                    _vertexTriangles[v].Add(t);
                    _vertexAlive[v] = true;
                }

                AddPlaneQuadric(t);
            }
        }

        public void Run()
        {
            for (var t = 0; t < _trianglesBefore; t++)
            {
                for (var e = 0; e < 3; e++)
                {
                    var a = _triangles[(3 * t) + e];
                    var b = _triangles[(3 * t) + ((e + 1) % 3)];

                    if (a < b)
                    {
                        Push(a, b);
                    }
                }
            }

            while (_heap.TryDequeue(out var entry, out _))
            {
                var (u, v, versionU, versionV) = entry;

                if (!_vertexAlive[u] || !_vertexAlive[v] || _version[u] != versionU || _version[v] != versionV)
                {
                    continue;
                }

                TryCollapse(u, v);
            }
        }

        public DecimationStats Stats() => new(_trianglesBefore, _aliveTriangles, _collapses, _rejectedTopology, _rejectedGeometry, _rejectedFidelity);

        public TriangleMesh Compact()
        {
            var remap = Enumerable.Repeat(-1, _positions.Length).ToArray();
            var vertices = new List<Vec3>();
            var indices = new List<int>(_aliveTriangles * 3);

            for (var t = 0; t < _trianglesBefore; t++)
            {
                if (!_triangleAlive[t])
                {
                    continue;
                }

                for (var e = 0; e < 3; e++)
                {
                    var v = _triangles[(3 * t) + e];

                    if (remap[v] < 0)
                    {
                        remap[v] = vertices.Count;
                        vertices.Add(_positions[v]);
                    }

                    indices.Add(remap[v]);
                }
            }

            return new TriangleMesh(vertices.ToArray(), indices.ToArray());
        }

        private void Push(int u, int v)
        {
            var target = OptimalPosition(u, v, out var cost);
            _ = target;
            _heap.Enqueue((u, v, _version[u], _version[v]), cost);
        }

        private void TryCollapse(int u, int v)
        {
            // Triangles shared by the edge and the opposite vertices.
            var shared0 = -1;
            var shared1 = -1;
            var opposite0 = -1;
            var opposite1 = -1;

            foreach (var t in _vertexTriangles[u])
            {
                if (!ContainsVertex(t, v))
                {
                    continue;
                }

                if (shared0 < 0)
                {
                    shared0 = t;
                    opposite0 = ThirdVertex(t, u, v);
                }
                else if (shared1 < 0)
                {
                    shared1 = t;
                    opposite1 = ThirdVertex(t, u, v);
                }
                else
                {
                    _rejectedTopology++;
                    return;
                }
            }

            if (shared0 < 0 || shared1 < 0 || opposite0 == opposite1)
            {
                _rejectedTopology++;
                return;
            }

            // Link condition: the only common neighbours of u and v are the two opposite vertices.
            _stamp++;
            var degreeU = 0;

            foreach (var t in _vertexTriangles[u])
            {
                for (var e = 0; e < 3; e++)
                {
                    var w = _triangles[(3 * t) + e];

                    if (w != u && _mark[w] != _stamp)
                    {
                        _mark[w] = _stamp;
                        degreeU++;
                    }
                }
            }

            var markU = _stamp;
            _stamp++;
            var degreeV = 0;
            var common = 0;

            foreach (var t in _vertexTriangles[v])
            {
                for (var e = 0; e < 3; e++)
                {
                    var w = _triangles[(3 * t) + e];

                    if (w == v || _mark[w] == _stamp)
                    {
                        continue;
                    }

                    if (_mark[w] == markU)
                    {
                        common++;

                        if (w != opposite0 && w != opposite1 && w != u)
                        {
                            _rejectedTopology++;
                            return;
                        }
                    }

                    _mark[w] = _stamp;
                    degreeV++;
                }
            }

            // degreeU counts v, degreeV counts u; the merged vertex keeps degreeU + degreeV - 4 neighbours.
            if (common != 2 || degreeU + degreeV - 4 < 3 || Degree(opposite0) < 4 || Degree(opposite1) < 4)
            {
                _rejectedTopology++;
                return;
            }

            var target = OptimalPosition(u, v, out _);

            if (Math.Abs(_sdf.Distance(target)) > _tolerance)
            {
                _rejectedFidelity++;
                return;
            }

            if (!GeometryAcceptable(u, v, target, shared0, shared1, out var fidelityFailure))
            {
                if (fidelityFailure)
                {
                    _rejectedFidelity++;
                }
                else
                {
                    _rejectedGeometry++;
                }

                return;
            }

            Collapse(u, v, target, shared0, shared1, opposite0, opposite1);
        }

        private int Degree(int vertex)
        {
            _stamp++;
            var degree = 0;

            foreach (var t in _vertexTriangles[vertex])
            {
                for (var e = 0; e < 3; e++)
                {
                    var w = _triangles[(3 * t) + e];

                    if (w != vertex && _mark[w] != _stamp)
                    {
                        _mark[w] = _stamp;
                        degree++;
                    }
                }
            }

            return degree;
        }

        private bool GeometryAcceptable(int u, int v, Vec3 target, int shared0, int shared1, out bool fidelityFailure)
        {
            fidelityFailure = false;

            // Pass 0/1: cheap normal and area checks on every affected triangle. Pass 2/3: distance-field checks
            // of the new centroids, only once the collapse is geometrically valid.
            for (var pass = 0; pass < 4; pass++)
            {
                var vertex = (pass & 1) == 0 ? u : v;
                var fidelityPass = pass >= 2;

                foreach (var t in _vertexTriangles[vertex])
                {
                    if (t == shared0 || t == shared1)
                    {
                        continue;
                    }

                    var a = _triangles[3 * t];
                    var b = _triangles[(3 * t) + 1];
                    var c = _triangles[(3 * t) + 2];
                    var pa = _positions[a];
                    var pb = _positions[b];
                    var pc = _positions[c];
                    var na = a == u || a == v ? target : pa;
                    var nb = b == u || b == v ? target : pb;
                    var nc = c == u || c == v ? target : pc;

                    if (fidelityPass)
                    {
                        var centroid = (na + nb + nc) * (1.0 / 3.0);

                        if (Math.Abs(_sdf.Distance(centroid)) > _centroidTolerance)
                        {
                            fidelityFailure = true;
                            return false;
                        }

                        continue;
                    }

                    var before = Vec3.Cross(pb - pa, pc - pa);
                    var after = Vec3.Cross(nb - na, nc - na);
                    var beforeLength = before.Length;
                    var afterLength = after.Length;

                    if (afterLength <= 1e-12 * Math.Max(1.0, beforeLength) || beforeLength <= 0)
                    {
                        return false;
                    }

                    if (Vec3.Dot(before, after) / (beforeLength * afterLength) < _minNormalDot)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void Collapse(int u, int v, Vec3 target, int shared0, int shared1, int opposite0, int opposite1)
        {
            _triangleAlive[shared0] = false;
            _triangleAlive[shared1] = false;
            _aliveTriangles -= 2;
            _vertexTriangles[u].Remove(shared0);
            _vertexTriangles[u].Remove(shared1);
            _vertexTriangles[opposite0].Remove(shared0);
            _vertexTriangles[opposite1].Remove(shared1);

            foreach (var t in _vertexTriangles[v])
            {
                if (t == shared0 || t == shared1)
                {
                    continue;
                }

                for (var e = 0; e < 3; e++)
                {
                    if (_triangles[(3 * t) + e] == v)
                    {
                        _triangles[(3 * t) + e] = u;
                    }
                }

                _vertexTriangles[u].Add(t);
            }

            _vertexTriangles[v].Clear();
            _vertexAlive[v] = false;
            _positions[u] = target;

            for (var i = 0; i < 10; i++)
            {
                _quadrics[(u * 10) + i] += _quadrics[(v * 10) + i];
            }

            _version[u]++;
            _version[v]++;
            _collapses++;

            _stamp++;

            foreach (var t in _vertexTriangles[u])
            {
                for (var e = 0; e < 3; e++)
                {
                    var w = _triangles[(3 * t) + e];

                    if (w != u && _mark[w] != _stamp)
                    {
                        _mark[w] = _stamp;
                        var low = Math.Min(u, w);
                        var high = Math.Max(u, w);
                        Push(low, high);
                    }
                }
            }
        }

        private bool ContainsVertex(int t, int vertex) =>
            _triangles[3 * t] == vertex || _triangles[(3 * t) + 1] == vertex || _triangles[(3 * t) + 2] == vertex;

        private int ThirdVertex(int t, int u, int v)
        {
            for (var e = 0; e < 3; e++)
            {
                var w = _triangles[(3 * t) + e];

                if (w != u && w != v)
                {
                    return w;
                }
            }

            return -1;
        }

        private void AddPlaneQuadric(int t)
        {
            var pa = _positions[_triangles[3 * t]];
            var pb = _positions[_triangles[(3 * t) + 1]];
            var pc = _positions[_triangles[(3 * t) + 2]];
            var cross = Vec3.Cross(pb - pa, pc - pa);
            var length = cross.Length;

            if (length <= 1e-18)
            {
                return;
            }

            var n = cross * (1.0 / length);
            var d = -Vec3.Dot(n, pa);
            var w = length / 2.0;
            Span<double> q = stackalloc double[10]
            {
                w * n.X * n.X, w * n.X * n.Y, w * n.X * n.Z, w * n.X * d,
                w * n.Y * n.Y, w * n.Y * n.Z, w * n.Y * d,
                w * n.Z * n.Z, w * n.Z * d,
                w * d * d
            };

            for (var e = 0; e < 3; e++)
            {
                var offset = _triangles[(3 * t) + e] * 10;

                for (var i = 0; i < 10; i++)
                {
                    _quadrics[offset + i] += q[i];
                }
            }
        }

        /// <summary>Minimizer of the summed quadric closest to the edge midpoint (pseudo-inverse).</summary>
        private Vec3 OptimalPosition(int u, int v, out double cost)
        {
            Span<double> q = stackalloc double[10];

            for (var i = 0; i < 10; i++)
            {
                q[i] = _quadrics[(u * 10) + i] + _quadrics[(v * 10) + i];
            }

            var midpoint = (_positions[u] + _positions[v]) * 0.5;

            // A = [[q0 q1 q2] [q1 q4 q5] [q2 q5 q7]], b = (q3, q6, q8): minimize x'Ax + 2 b'x + q9.
            var a00 = q[0];
            var a01 = q[1];
            var a02 = q[2];
            var a11 = q[4];
            var a12 = q[5];
            var a22 = q[7];
            var gx = -(q[3] + (a00 * midpoint.X) + (a01 * midpoint.Y) + (a02 * midpoint.Z));
            var gy = -(q[6] + (a01 * midpoint.X) + (a11 * midpoint.Y) + (a12 * midpoint.Z));
            var gz = -(q[8] + (a02 * midpoint.X) + (a12 * midpoint.Y) + (a22 * midpoint.Z));

            SymmetricEigen3.Decompose(a00, a01, a02, a11, a12, a22, out var values, out var axis0, out var axis1, out var axis2);
            var largest = Math.Max(Math.Abs(values.X), Math.Max(Math.Abs(values.Y), Math.Abs(values.Z)));
            var threshold = Math.Max(largest * 1e-3, 1e-30);
            var g = new Vec3(gx, gy, gz);
            var step = Vec3.Zero;

            if (Math.Abs(values.X) > threshold)
            {
                step += axis0 * (Vec3.Dot(axis0, g) / values.X);
            }

            if (Math.Abs(values.Y) > threshold)
            {
                step += axis1 * (Vec3.Dot(axis1, g) / values.Y);
            }

            if (Math.Abs(values.Z) > threshold)
            {
                step += axis2 * (Vec3.Dot(axis2, g) / values.Z);
            }

            var target = midpoint + step;
            var edgeLength = (_positions[u] - _positions[v]).Length;

            if ((target - midpoint).Length > 2 * edgeLength)
            {
                target = midpoint;
            }

            cost = Math.Max(0.0, Evaluate(q, target));
            return target;
        }

        private static double Evaluate(ReadOnlySpan<double> q, Vec3 p) =>
            (q[0] * p.X * p.X) + (2 * q[1] * p.X * p.Y) + (2 * q[2] * p.X * p.Z) + (2 * q[3] * p.X) +
            (q[4] * p.Y * p.Y) + (2 * q[5] * p.Y * p.Z) + (2 * q[6] * p.Y) +
            (q[7] * p.Z * p.Z) + (2 * q[8] * p.Z) + q[9];
    }
}

/// <summary>Jacobi eigen decomposition of a symmetric 3x3 matrix (allocation free).</summary>
internal static class SymmetricEigen3
{
    public static void Decompose(double a00, double a01, double a02, double a11, double a12, double a22, out Vec3 values, out Vec3 axis0, out Vec3 axis1, out Vec3 axis2)
    {
        Span<double> a = stackalloc double[9] { a00, a01, a02, a01, a11, a12, a02, a12, a22 };
        Span<double> v = stackalloc double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        for (var sweep = 0; sweep < 32; sweep++)
        {
            var off = Math.Abs(a[1]) + Math.Abs(a[2]) + Math.Abs(a[5]);
            var scale = Math.Abs(a[0]) + Math.Abs(a[4]) + Math.Abs(a[8]);

            if (off <= 1e-15 * Math.Max(scale, 1e-300))
            {
                break;
            }

            for (var p = 0; p < 2; p++)
            {
                for (var r = p + 1; r < 3; r++)
                {
                    var apr = a[(p * 3) + r];

                    if (Math.Abs(apr) < 1e-300)
                    {
                        continue;
                    }

                    var theta = (a[(r * 3) + r] - a[(p * 3) + p]) / (2 * apr);
                    var t = theta == 0 ? 1.0 : Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt((theta * theta) + 1));
                    var c = 1 / Math.Sqrt((t * t) + 1);
                    var s = t * c;

                    for (var k = 0; k < 3; k++)
                    {
                        var akp = a[(k * 3) + p];
                        var akr = a[(k * 3) + r];
                        a[(k * 3) + p] = (c * akp) - (s * akr);
                        a[(k * 3) + r] = (s * akp) + (c * akr);
                    }

                    for (var k = 0; k < 3; k++)
                    {
                        var apk = a[(p * 3) + k];
                        var ark = a[(r * 3) + k];
                        a[(p * 3) + k] = (c * apk) - (s * ark);
                        a[(r * 3) + k] = (s * apk) + (c * ark);
                    }

                    for (var k = 0; k < 3; k++)
                    {
                        var vkp = v[(k * 3) + p];
                        var vkr = v[(k * 3) + r];
                        v[(k * 3) + p] = (c * vkp) - (s * vkr);
                        v[(k * 3) + r] = (s * vkp) + (c * vkr);
                    }
                }
            }
        }

        values = new Vec3(a[0], a[4], a[8]);
        axis0 = new Vec3(v[0], v[3], v[6]);
        axis1 = new Vec3(v[1], v[4], v[7]);
        axis2 = new Vec3(v[2], v[5], v[8]);
    }
}
