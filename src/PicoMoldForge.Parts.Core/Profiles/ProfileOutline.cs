using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Profiles;

/// <summary>
/// Extracts closed outline polylines of a profile with marching squares. Used for visualization
/// (the viewer animates these outlines along their sweep trajectories). The outlines are derived from the
/// same distance function that the solid is built from, so they cannot drift from the geometry.
/// </summary>
public static class ProfileOutline
{
    private const int MaxGridPointsPerAxis = 1600;

    public static IReadOnlyList<IReadOnlyList<Vec2>> Trace(IProfile2D profile, double step, double simplifyTolerance)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!(step > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be positive.");
        }

        var bounds = profile.Bounds.Expand(2 * step);
        var size = bounds.Size;
        var largest = Math.Max(size.X, size.Y);

        if (largest / step > MaxGridPointsPerAxis - 1)
        {
            step = largest / (MaxGridPointsPerAxis - 1);
        }

        var nx = (int)Math.Ceiling(size.X / step) + 1;
        var ny = (int)Math.Ceiling(size.Y / step) + 1;
        var values = new double[nx * ny];

        Parallel.For(0, ny, j =>
        {
            for (var i = 0; i < nx; i++)
            {
                values[(j * nx) + i] = profile.Distance(new Vec2(bounds.Min.X + (i * step), bounds.Min.Y + (j * step)));
            }
        });

        Vec2 GridPoint(int i, int j) => new(bounds.Min.X + (i * step), bounds.Min.Y + (j * step));
        double Value(int i, int j) => values[(j * nx) + i];

        // Edge keys: horizontal edge from (i, j) to (i + 1, j) -> 2 * index; vertical edge (i, j) to (i, j + 1) -> 2 * index + 1.
        long HorizontalKey(int i, int j) => 2L * ((long)j * nx + i);
        long VerticalKey(int i, int j) => (2L * ((long)j * nx + i)) + 1;

        var points = new Dictionary<long, Vec2>();
        var next = new Dictionary<long, long>();

        Vec2 Crossing(long key, int ia, int ja, int ib, int jb)
        {
            if (points.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var va = Value(ia, ja);
            var vb = Value(ib, jb);
            var t = Math.Clamp(va / (va - vb), 0.0, 1.0);
            var pa = GridPoint(ia, ja);
            var pb = GridPoint(ib, jb);
            var p = pa + ((pb - pa) * t);
            points[key] = p;
            return p;
        }

        var cornerI = new int[4];
        var cornerJ = new int[4];
        var edgeKeys = new long[4];
        var crossingEdges = new List<(int Edge, bool Entry)>(4);

        for (var j = 0; j < ny - 1; j++)
        {
            for (var i = 0; i < nx - 1; i++)
            {
                // Counterclockwise walk: (i,j) -> (i+1,j) -> (i+1,j+1) -> (i,j+1).
                cornerI[0] = i; cornerJ[0] = j;
                cornerI[1] = i + 1; cornerJ[1] = j;
                cornerI[2] = i + 1; cornerJ[2] = j + 1;
                cornerI[3] = i; cornerJ[3] = j + 1;
                edgeKeys[0] = HorizontalKey(i, j);
                edgeKeys[1] = VerticalKey(i + 1, j);
                edgeKeys[2] = HorizontalKey(i, j + 1);
                edgeKeys[3] = VerticalKey(i, j);

                crossingEdges.Clear();

                for (var k = 0; k < 4; k++)
                {
                    var a = k;
                    var b = (k + 1) % 4;
                    var insideA = Value(cornerI[a], cornerJ[a]) < 0;
                    var insideB = Value(cornerI[b], cornerJ[b]) < 0;

                    if (insideA != insideB)
                    {
                        crossingEdges.Add((k, !insideA && insideB));
                    }
                }

                if (crossingEdges.Count == 0)
                {
                    continue;
                }

                for (var c = 0; c < crossingEdges.Count; c++)
                {
                    if (!crossingEdges[c].Entry)
                    {
                        continue;
                    }

                    var exit = crossingEdges[(c + 1) % crossingEdges.Count];

                    var entryEdge = crossingEdges[c].Edge;
                    var exitEdge = exit.Edge;
                    var entryKey = edgeKeys[entryEdge];
                    var exitKey = edgeKeys[exitEdge];

                    Crossing(entryKey, cornerI[entryEdge], cornerJ[entryEdge], cornerI[(entryEdge + 1) % 4], cornerJ[(entryEdge + 1) % 4]);
                    Crossing(exitKey, cornerI[exitEdge], cornerJ[exitEdge], cornerI[(exitEdge + 1) % 4], cornerJ[(exitEdge + 1) % 4]);
                    next[entryKey] = exitKey;
                }
            }
        }

        var loops = new List<IReadOnlyList<Vec2>>();
        var visited = new HashSet<long>();

        foreach (var start in next.Keys.OrderBy(key => key))
        {
            if (visited.Contains(start))
            {
                continue;
            }

            var loop = new List<Vec2>();
            var current = start;

            while (visited.Add(current))
            {
                loop.Add(points[current]);

                if (!next.TryGetValue(current, out current))
                {
                    break;
                }
            }

            if (loop.Count >= 3)
            {
                loops.Add(Simplify(loop, simplifyTolerance));
            }
        }

        return loops;
    }

    private static IReadOnlyList<Vec2> Simplify(List<Vec2> loop, double tolerance)
    {
        if (tolerance <= 0 || loop.Count < 8)
        {
            return loop;
        }

        // Split the closed loop at the point farthest from the first point and simplify both halves.
        var far = 0;
        var farDistance = -1.0;

        for (var i = 1; i < loop.Count; i++)
        {
            var d = (loop[i] - loop[0]).LengthSquared;

            if (d > farDistance)
            {
                farDistance = d;
                far = i;
            }
        }

        var keep = new bool[loop.Count];
        keep[0] = true;
        keep[far] = true;
        SimplifyRange(loop, 0, far, tolerance, keep);
        SimplifyRange(loop, far, loop.Count, tolerance, keep);

        var result = new List<Vec2>();

        for (var i = 0; i < loop.Count; i++)
        {
            if (keep[i])
            {
                result.Add(loop[i]);
            }
        }

        return result.Count >= 3 ? result : loop;
    }

    private static void SimplifyRange(List<Vec2> loop, int first, int last, double tolerance, bool[] keep)
    {
        // Indices are taken modulo the loop length so the closing segment is handled.
        var stack = new Stack<(int First, int Last)>();
        stack.Push((first, last));

        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop();

            if (b - a < 2)
            {
                continue;
            }

            var pa = loop[a % loop.Count];
            var pb = loop[b % loop.Count];
            var maxDistance = -1.0;
            var index = -1;

            for (var i = a + 1; i < b; i++)
            {
                var d = DistanceToSegment(loop[i % loop.Count], pa, pb);

                if (d > maxDistance)
                {
                    maxDistance = d;
                    index = i;
                }
            }

            if (maxDistance > tolerance)
            {
                keep[index % loop.Count] = true;
                stack.Push((a, index));
                stack.Push((index, b));
            }
        }
    }

    private static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
    {
        var e = b - a;
        var lengthSquared = e.LengthSquared;

        if (lengthSquared == 0)
        {
            return (p - a).Length;
        }

        var t = Math.Clamp(Vec2.Dot(p - a, e) / lengthSquared, 0.0, 1.0);
        return (p - (a + (e * t))).Length;
    }
}
