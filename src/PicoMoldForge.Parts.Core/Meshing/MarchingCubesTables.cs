namespace PicoMoldForge.Parts.Meshing;

/// <summary>
/// Marching cubes triangle table generated at startup by tracing the iso-contour on the six cube faces
/// (no hand-copied 256-entry table). On every face the contour walks the corners counterclockwise as seen from
/// outside the cube and connects each crossing where it enters the inside region to the next crossing where it
/// leaves it. Ambiguous faces therefore always separate the inside corners, and because two cubes that share a
/// face see the same corners in opposite order, they produce the same segments with opposite orientation: the
/// resulting surface is closed, manifold across cubes and consistently oriented (normals point outside).
/// Corner i is inside when its value is negative.
/// </summary>
internal static class MarchingCubesTables
{
    /// <summary>Cube corners: 0 (0,0,0), 1 (1,0,0), 2 (1,1,0), 3 (0,1,0), 4 (0,0,1), 5 (1,0,1), 6 (1,1,1), 7 (0,1,1).</summary>
    public static readonly int[] CornerX = { 0, 1, 1, 0, 0, 1, 1, 0 };
    public static readonly int[] CornerY = { 0, 0, 1, 1, 0, 0, 1, 1 };
    public static readonly int[] CornerZ = { 0, 0, 0, 0, 1, 1, 1, 1 };

    /// <summary>Edge endpoints; the first corner is always the lower one along the edge direction.</summary>
    public static readonly int[] EdgeA = { 0, 1, 3, 0, 4, 5, 7, 4, 0, 1, 2, 3 };
    public static readonly int[] EdgeB = { 1, 2, 2, 3, 5, 6, 6, 7, 4, 5, 6, 7 };

    /// <summary>Edge direction: 0 = X, 1 = Y, 2 = Z.</summary>
    public static readonly int[] EdgeDirection = { 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 2 };

    /// <summary>Triangles per case as edge index triples, flattened. Offsets index into <see cref="TriangleEdges"/>.</summary>
    public static readonly int[] TriangleEdges;
    public static readonly int[] CaseOffsets;

    private static readonly int[][] Faces =
    {
        new[] { 0, 1, 2, 3 },
        new[] { 4, 5, 6, 7 },
        new[] { 0, 1, 5, 4 },
        new[] { 3, 2, 6, 7 },
        new[] { 0, 3, 7, 4 },
        new[] { 1, 2, 6, 5 }
    };

    private static readonly int[][] FaceNormals =
    {
        new[] { 0, 0, -1 },
        new[] { 0, 0, 1 },
        new[] { 0, -1, 0 },
        new[] { 0, 1, 0 },
        new[] { -1, 0, 0 },
        new[] { 1, 0, 0 }
    };

    static MarchingCubesTables()
    {
        var orderedFaces = Faces.Select((corners, f) => OrderCounterclockwise(corners, FaceNormals[f])).ToArray();
        var triangles = new List<int>();
        var offsets = new int[257];

        for (var cubeCase = 0; cubeCase < 256; cubeCase++)
        {
            offsets[cubeCase] = triangles.Count;
            triangles.AddRange(BuildCase(cubeCase, orderedFaces));
        }

        offsets[256] = triangles.Count;
        TriangleEdges = triangles.ToArray();
        CaseOffsets = offsets;
    }

    internal static IReadOnlyList<IReadOnlyList<int>> Loops(int cubeCase)
    {
        var orderedFaces = Faces.Select((corners, f) => OrderCounterclockwise(corners, FaceNormals[f])).ToArray();
        return TraceLoops(cubeCase, orderedFaces);
    }

    private static IEnumerable<int> BuildCase(int cubeCase, int[][] orderedFaces)
    {
        foreach (var loop in TraceLoops(cubeCase, orderedFaces))
        {
            for (var i = 1; i < loop.Count - 1; i++)
            {
                yield return loop[0];
                yield return loop[i];
                yield return loop[i + 1];
            }
        }
    }

    private static List<IReadOnlyList<int>> TraceLoops(int cubeCase, int[][] orderedFaces)
    {
        var next = Enumerable.Repeat(-1, 12).ToArray();

        foreach (var face in orderedFaces)
        {
            var crossings = new List<(int Edge, bool Entry)>(4);

            for (var k = 0; k < 4; k++)
            {
                var a = face[k];
                var b = face[(k + 1) % 4];
                var insideA = ((cubeCase >> a) & 1) == 1;
                var insideB = ((cubeCase >> b) & 1) == 1;

                if (insideA != insideB)
                {
                    crossings.Add((EdgeBetween(a, b), !insideA && insideB));
                }
            }

            for (var c = 0; c < crossings.Count; c++)
            {
                if (!crossings[c].Entry)
                {
                    continue;
                }

                var exit = crossings[(c + 1) % crossings.Count];

                if (exit.Entry || next[crossings[c].Edge] != -1)
                {
                    throw new InvalidOperationException($"Marching cubes table generation failed for case {cubeCase}.");
                }

                next[crossings[c].Edge] = exit.Edge;
            }
        }

        var loops = new List<IReadOnlyList<int>>();
        var visited = new bool[12];

        for (var start = 0; start < 12; start++)
        {
            if (next[start] < 0 || visited[start])
            {
                continue;
            }

            var loop = new List<int>();
            var edge = start;

            while (!visited[edge])
            {
                visited[edge] = true;
                loop.Add(edge);
                edge = next[edge];

                if (edge < 0)
                {
                    throw new InvalidOperationException($"Open contour in marching cubes case {cubeCase}.");
                }
            }

            if (edge != start || loop.Count < 3)
            {
                throw new InvalidOperationException($"Invalid contour loop in marching cubes case {cubeCase}.");
            }

            loops.Add(loop);
        }

        return loops;
    }

    private static int EdgeBetween(int a, int b)
    {
        for (var e = 0; e < 12; e++)
        {
            if ((EdgeA[e] == a && EdgeB[e] == b) || (EdgeA[e] == b && EdgeB[e] == a))
            {
                return e;
            }
        }

        throw new InvalidOperationException($"Corners {a} and {b} do not share a cube edge.");
    }

    private static int[] OrderCounterclockwise(int[] corners, int[] normal)
    {
        int Cross(int c0, int c1, int c2)
        {
            var e1 = (CornerX[c1] - CornerX[c0], CornerY[c1] - CornerY[c0], CornerZ[c1] - CornerZ[c0]);
            var e2 = (CornerX[c2] - CornerX[c1], CornerY[c2] - CornerY[c1], CornerZ[c2] - CornerZ[c1]);
            var cx = (e1.Item2 * e2.Item3) - (e1.Item3 * e2.Item2);
            var cy = (e1.Item3 * e2.Item1) - (e1.Item1 * e2.Item3);
            var cz = (e1.Item1 * e2.Item2) - (e1.Item2 * e2.Item1);
            return (cx * normal[0]) + (cy * normal[1]) + (cz * normal[2]);
        }

        return Cross(corners[0], corners[1], corners[2]) > 0 ? corners : corners.Reverse().ToArray();
    }
}
