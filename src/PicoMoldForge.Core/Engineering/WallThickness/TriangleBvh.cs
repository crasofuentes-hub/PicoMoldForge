namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>Double-precision vector for mesh queries.</summary>
internal readonly record struct Vec3d(double X, double Y, double Z)
{
    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3d operator -(Vec3d a) => new(-a.X, -a.Y, -a.Z);

    public static Vec3d operator *(Vec3d a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public static double Dot(Vec3d a, Vec3d b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    public static Vec3d Cross(Vec3d a, Vec3d b) => new((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));

    public double Length => Math.Sqrt(Dot(this, this));

    public double this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;

    public static Vec3d From(MeshVertex v) => new(v.X, v.Y, v.Z);
}

/// <summary>
/// Bounding volume hierarchy over the triangles of a mesh: nearest-triangle, "anything closer than" and ray
/// queries. Built by median splits along the longest axis, so the same input always gives the same tree.
/// Immutable after construction and safe for parallel queries.
/// </summary>
internal sealed class TriangleBvh
{
    private const int LeafSize = 4;
    private const int MaxStack = 128;

    private readonly Vec3d[] _a;
    private readonly Vec3d[] _b;
    private readonly Vec3d[] _c;
    private readonly int[] _original;
    private readonly List<Node> _nodes = new();

    public TriangleBvh(IReadOnlyList<MeshTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);

        if (triangles.Count == 0)
        {
            throw new ArgumentException("At least one triangle is required.", nameof(triangles));
        }

        var count = triangles.Count;
        var order = Enumerable.Range(0, count).ToArray();
        var centroids = new Vec3d[count];

        for (var i = 0; i < count; i++)
        {
            var t = triangles[i];
            centroids[i] = (Vec3d.From(t.A) + Vec3d.From(t.B) + Vec3d.From(t.C)) * (1.0 / 3.0);
        }

        Build(triangles, order, centroids, 0, count);

        _a = new Vec3d[count];
        _b = new Vec3d[count];
        _c = new Vec3d[count];
        _original = order;

        for (var i = 0; i < count; i++)
        {
            var t = triangles[order[i]];
            _a[i] = Vec3d.From(t.A);
            _b[i] = Vec3d.From(t.B);
            _c[i] = Vec3d.From(t.C);
        }
    }

    public Vec3d Min => _nodes[0].Min;

    public Vec3d Max => _nodes[0].Max;

    /// <summary>True when some triangle lies closer than <paramref name="limit"/> to <paramref name="point"/>.</summary>
    public bool AnyCloserThan(Vec3d point, double limit)
    {
        if (!(limit > 0))
        {
            return false;
        }

        var limitSquared = limit * limit;
        Span<int> stack = stackalloc int[MaxStack];
        var top = 0;
        stack[top++] = 0;

        while (top > 0)
        {
            var node = _nodes[stack[--top]];

            if (BoxDistanceSquared(node, point) >= limitSquared)
            {
                continue;
            }

            if (node.Left < 0)
            {
                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    if (PointTriangleDistanceSquared(point, _a[i], _b[i], _c[i]) < limitSquared)
                    {
                        return true;
                    }
                }

                continue;
            }

            stack[top++] = node.Left;
            stack[top++] = node.Right;
        }

        return false;
    }

    /// <summary>Nearest triangle (original index) and its distance.</summary>
    public (double Distance, int Triangle) Nearest(Vec3d point)
    {
        var bestSquared = double.PositiveInfinity;
        var best = -1;
        Span<int> stack = stackalloc int[MaxStack];
        var top = 0;
        stack[top++] = 0;

        while (top > 0)
        {
            var node = _nodes[stack[--top]];

            if (BoxDistanceSquared(node, point) >= bestSquared)
            {
                continue;
            }

            if (node.Left < 0)
            {
                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    var d = PointTriangleDistanceSquared(point, _a[i], _b[i], _c[i]);

                    if (d < bestSquared)
                    {
                        bestSquared = d;
                        best = _original[i];
                    }
                }

                continue;
            }

            var left = _nodes[node.Left];
            var right = _nodes[node.Right];

            // Visit the nearer child first so the bound tightens early.
            if (BoxDistanceSquared(left, point) < BoxDistanceSquared(right, point))
            {
                stack[top++] = node.Right;
                stack[top++] = node.Left;
            }
            else
            {
                stack[top++] = node.Left;
                stack[top++] = node.Right;
            }
        }

        return (Math.Sqrt(bestSquared), best);
    }

    /// <summary>Distance along the ray to the first triangle hit, or +infinity.</summary>
    public double Ray(Vec3d origin, Vec3d direction)
    {
        var best = double.PositiveInfinity;
        Span<int> stack = stackalloc int[MaxStack];
        var top = 0;
        stack[top++] = 0;

        while (top > 0)
        {
            var node = _nodes[stack[--top]];

            if (!RayHitsBox(origin, direction, node.Min, node.Max, best))
            {
                continue;
            }

            if (node.Left < 0)
            {
                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    var t = RayTriangle(origin, direction, _a[i], _b[i], _c[i]);

                    if (t > 1e-12 && t < best)
                    {
                        best = t;
                    }
                }

                continue;
            }

            stack[top++] = node.Left;
            stack[top++] = node.Right;
        }

        return best;
    }

    /// <summary>
    /// First triangle hit along the ray, skipping <paramref name="excludedTriangle"/> (original index, or -1 for
    /// none): its distance and original index, or (+infinity, -1) when the ray misses.
    /// </summary>
    public (double Distance, int Triangle) RayHit(Vec3d origin, Vec3d direction, int excludedTriangle)
    {
        var best = double.PositiveInfinity;
        var bestTriangle = -1;
        Span<int> stack = stackalloc int[MaxStack];
        var top = 0;
        stack[top++] = 0;

        while (top > 0)
        {
            var node = _nodes[stack[--top]];

            if (!RayHitsBox(origin, direction, node.Min, node.Max, best))
            {
                continue;
            }

            if (node.Left < 0)
            {
                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    if (_original[i] == excludedTriangle)
                    {
                        continue;
                    }

                    var t = RayTriangle(origin, direction, _a[i], _b[i], _c[i]);

                    if (t > 1e-12 && t < best)
                    {
                        best = t;
                        bestTriangle = _original[i];
                    }
                }

                continue;
            }

            stack[top++] = node.Left;
            stack[top++] = node.Right;
        }

        return (best, bestTriangle);
    }

    private int Build(IReadOnlyList<MeshTriangle> triangles, int[] order, Vec3d[] centroids, int start, int count)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;

        for (var i = start; i < start + count; i++)
        {
            var t = triangles[order[i]];
            minX = Math.Min(minX, Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)));
            minY = Math.Min(minY, Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)));
            minZ = Math.Min(minZ, Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z)));
            maxX = Math.Max(maxX, Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)));
            maxY = Math.Max(maxY, Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)));
            maxZ = Math.Max(maxZ, Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z)));
        }

        var min = new Vec3d(minX, minY, minZ);
        var max = new Vec3d(maxX, maxY, maxZ);

        var index = _nodes.Count;
        _nodes.Add(default);

        if (count <= LeafSize)
        {
            _nodes[index] = new Node(min, max, -1, -1, start, count);
            return index;
        }

        var extent = max - min;
        var axis = extent.X >= extent.Y && extent.X >= extent.Z ? 0 : extent.Y >= extent.Z ? 1 : 2;
        var keys = new double[count];

        for (var i = 0; i < count; i++)
        {
            keys[i] = centroids[order[start + i]][axis];
        }

        // Sort by centroid along the axis; equal keys keep a deterministic order by triangle index.
        var slice = order.AsSpan(start, count).ToArray();
        var indices = Enumerable.Range(0, count).ToArray();
        Array.Sort(indices, (x, y) =>
        {
            var byKey = keys[x].CompareTo(keys[y]);
            return byKey != 0 ? byKey : slice[x].CompareTo(slice[y]);
        });

        for (var i = 0; i < count; i++)
        {
            order[start + i] = slice[indices[i]];
        }

        var half = count / 2;
        var left = Build(triangles, order, centroids, start, half);
        var right = Build(triangles, order, centroids, start + half, count - half);
        _nodes[index] = new Node(min, max, left, right, start, count);
        return index;
    }

    private static double BoxDistanceSquared(Node node, Vec3d p)
    {
        var dx = p.X < node.Min.X ? node.Min.X - p.X : p.X > node.Max.X ? p.X - node.Max.X : 0;
        var dy = p.Y < node.Min.Y ? node.Min.Y - p.Y : p.Y > node.Max.Y ? p.Y - node.Max.Y : 0;
        var dz = p.Z < node.Min.Z ? node.Min.Z - p.Z : p.Z > node.Max.Z ? p.Z - node.Max.Z : 0;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static bool RayHitsBox(Vec3d origin, Vec3d direction, Vec3d min, Vec3d max, double tMax)
    {
        var t0 = 0.0;
        var t1 = tMax;

        for (var axis = 0; axis < 3; axis++)
        {
            var inverse = 1.0 / direction[axis];
            var near = (min[axis] - origin[axis]) * inverse;
            var far = (max[axis] - origin[axis]) * inverse;

            if (inverse < 0)
            {
                (near, far) = (far, near);
            }

            // NaN (ray parallel to the slab and on its plane) compares false and keeps the box.
            t0 = near > t0 ? near : t0;
            t1 = far < t1 ? far : t1;

            if (t1 < t0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Moller-Trumbore; +infinity when the ray misses.</summary>
    private static double RayTriangle(Vec3d origin, Vec3d direction, Vec3d a, Vec3d b, Vec3d c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = Vec3d.Cross(direction, e2);
        var determinant = Vec3d.Dot(e1, p);

        if (Math.Abs(determinant) < 1e-18)
        {
            return double.PositiveInfinity;
        }

        var inverse = 1.0 / determinant;
        var s = origin - a;
        var u = Vec3d.Dot(s, p) * inverse;

        if (u < 0 || u > 1)
        {
            return double.PositiveInfinity;
        }

        var q = Vec3d.Cross(s, e1);
        var v = Vec3d.Dot(direction, q) * inverse;

        if (v < 0 || u + v > 1)
        {
            return double.PositiveInfinity;
        }

        return Vec3d.Dot(e2, q) * inverse;
    }

    /// <summary>Squared distance from a point to a triangle (Ericson, Real-Time Collision Detection, 5.1.5).</summary>
    internal static double PointTriangleDistanceSquared(Vec3d p, Vec3d a, Vec3d b, Vec3d c)
    {
        var ab = b - a;
        var ac = c - a;
        var ap = p - a;
        var d1 = Vec3d.Dot(ab, ap);
        var d2 = Vec3d.Dot(ac, ap);

        if (d1 <= 0 && d2 <= 0)
        {
            return Vec3d.Dot(ap, ap);
        }

        var bp = p - b;
        var d3 = Vec3d.Dot(ab, bp);
        var d4 = Vec3d.Dot(ac, bp);

        if (d3 >= 0 && d4 <= d3)
        {
            return Vec3d.Dot(bp, bp);
        }

        var vc = (d1 * d4) - (d3 * d2);

        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            var v = d1 / (d1 - d3);
            var q = p - (a + (ab * v));
            return Vec3d.Dot(q, q);
        }

        var cp = p - c;
        var d5 = Vec3d.Dot(ab, cp);
        var d6 = Vec3d.Dot(ac, cp);

        if (d6 >= 0 && d5 <= d6)
        {
            return Vec3d.Dot(cp, cp);
        }

        var vb = (d5 * d2) - (d1 * d6);

        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            var w = d2 / (d2 - d6);
            var q = p - (a + (ac * w));
            return Vec3d.Dot(q, q);
        }

        var va = (d3 * d6) - (d5 * d4);

        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
        {
            var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            var q = p - (b + ((c - b) * w));
            return Vec3d.Dot(q, q);
        }

        var denominator = 1.0 / (va + vb + vc);
        var vv = vb * denominator;
        var ww = vc * denominator;
        var closest = a + (ab * vv) + (ac * ww);
        var r = p - closest;
        return Vec3d.Dot(r, r);
    }

    private readonly record struct Node(Vec3d Min, Vec3d Max, int Left, int Right, int Start, int Count);
}
