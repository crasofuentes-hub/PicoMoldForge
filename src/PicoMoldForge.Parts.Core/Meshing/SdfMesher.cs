using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Meshing;

/// <summary>Indexed triangle mesh (millimeters). Triangles are counterclockwise seen from outside.</summary>
public sealed class TriangleMesh
{
    public TriangleMesh(Vec3[] vertices, int[] indices)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));

        if (indices.Length % 3 != 0)
        {
            throw new ArgumentException("Index count must be a multiple of 3.", nameof(indices));
        }
    }

    public Vec3[] Vertices { get; }

    public int[] Indices { get; }

    public int TriangleCount => Indices.Length / 3;
}

public sealed record MeshingOptions(double ResolutionMm, int BlockCells = 8, int PaddingCells = 2, int MaxDegreeOfParallelism = -1);

/// <summary>
/// Reference mesher: marching cubes on a regular grid, evaluated only in blocks that can contain the surface
/// (|distance at block center| &lt;= Lipschitz * half diagonal). Vertices are keyed by grid edge, so shared
/// vertices are welded exactly and the output is deterministic regardless of thread scheduling.
/// </summary>
public static class SdfMesher
{
    private const double InterpolationClamp = 1e-4;
    private const long MaxGridPoints = 4_000_000_000L;

    public static TriangleMesh Mesh(ISdf sdf, MeshingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sdf);
        ArgumentNullException.ThrowIfNull(options);

        var h = options.ResolutionMm;

        if (!(h > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Resolution must be positive.");
        }

        var block = Math.Clamp(options.BlockCells, 2, 32);
        var pad = Math.Max(options.PaddingCells, 1);
        var bounds = sdf.Bounds;
        var origin = bounds.Min - new Vec3(pad * h, pad * h, pad * h);
        var nx = (int)Math.Ceiling((bounds.Size.X + (2 * pad * h)) / h) + 1;
        var ny = (int)Math.Ceiling((bounds.Size.Y + (2 * pad * h)) / h) + 1;
        var nz = (int)Math.Ceiling((bounds.Size.Z + (2 * pad * h)) / h) + 1;

        if ((long)nx * ny * nz > MaxGridPoints)
        {
            throw new InvalidOperationException(FormattableString.Invariant(
                $"Resolution {h} mm is too fine for a part of {bounds.Size} mm ({nx} x {ny} x {nz} grid points). Use a coarser resolution."));
        }

        var bx = (nx - 2 + block) / block;
        var by = (ny - 2 + block) / block;
        var bz = (nz - 2 + block) / block;
        var totalBlocks = (long)bx * by * bz;
        var results = new BlockResult?[totalBlocks];
        var lipschitz = Math.Max(1.0, sdf.Lipschitz) * 1.05;
        var parallel = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
        };

        Parallel.For(0L, totalBlocks, parallel, blockIndex =>
        {
            var ib = (int)(blockIndex % bx);
            var jb = (int)((blockIndex / bx) % by);
            var kb = (int)(blockIndex / ((long)bx * by));
            var i0 = ib * block;
            var j0 = jb * block;
            var k0 = kb * block;
            var cx = Math.Min(block, nx - 1 - i0);
            var cy = Math.Min(block, ny - 1 - j0);
            var cz = Math.Min(block, nz - 1 - k0);

            if (cx <= 0 || cy <= 0 || cz <= 0)
            {
                return;
            }

            var center = origin + new Vec3((i0 + (cx / 2.0)) * h, (j0 + (cy / 2.0)) * h, (k0 + (cz / 2.0)) * h);
            var halfDiagonal = 0.5 * h * Math.Sqrt((cx * cx) + (cy * cy) + (cz * cz));

            if (Math.Abs(sdf.Distance(center)) > (lipschitz * halfDiagonal) + 1e-12)
            {
                return;
            }

            results[blockIndex] = MeshBlock(sdf, origin, h, nx, ny, i0, j0, k0, cx, cy, cz);
        });

        return Merge(results);
    }

    private static BlockResult? MeshBlock(ISdf sdf, Vec3 origin, double h, int nx, int ny, int i0, int j0, int k0, int cx, int cy, int cz)
    {
        var sx = cx + 1;
        var sy = cy + 1;
        var sz = cz + 1;
        var values = new double[sx * sy * sz];
        var anyInside = false;
        var anyOutside = false;

        for (var k = 0; k < sz; k++)
        {
            for (var j = 0; j < sy; j++)
            {
                for (var i = 0; i < sx; i++)
                {
                    var value = sdf.Distance(origin + new Vec3((i0 + i) * h, (j0 + j) * h, (k0 + k) * h));
                    values[(((k * sy) + j) * sx) + i] = value;

                    if (value < 0)
                    {
                        anyInside = true;
                    }
                    else
                    {
                        anyOutside = true;
                    }
                }
            }
        }

        if (!anyInside || !anyOutside)
        {
            return null;
        }

        var keys = new List<long>();
        var positions = new Dictionary<long, Vec3>();
        var corner = new double[8];

        for (var k = 0; k < cz; k++)
        {
            for (var j = 0; j < cy; j++)
            {
                for (var i = 0; i < cx; i++)
                {
                    var cubeCase = 0;

                    for (var c = 0; c < 8; c++)
                    {
                        var value = values[((((k + MarchingCubesTables.CornerZ[c]) * sy) + j + MarchingCubesTables.CornerY[c]) * sx) + i + MarchingCubesTables.CornerX[c]];
                        corner[c] = value;

                        if (value < 0)
                        {
                            cubeCase |= 1 << c;
                        }
                    }

                    var start = MarchingCubesTables.CaseOffsets[cubeCase];
                    var end = MarchingCubesTables.CaseOffsets[cubeCase + 1];

                    for (var t = start; t < end; t++)
                    {
                        var edge = MarchingCubesTables.TriangleEdges[t];
                        var a = MarchingCubesTables.EdgeA[edge];
                        var b = MarchingCubesTables.EdgeB[edge];
                        var gi = i0 + i + MarchingCubesTables.CornerX[a];
                        var gj = j0 + j + MarchingCubesTables.CornerY[a];
                        var gk = k0 + k + MarchingCubesTables.CornerZ[a];
                        var key = (((((long)gk * ny) + gj) * nx) + gi) * 3 + MarchingCubesTables.EdgeDirection[edge];
                        keys.Add(key);

                        if (!positions.ContainsKey(key))
                        {
                            var va = corner[a];
                            var vb = corner[b];
                            var fraction = Math.Clamp(va / (va - vb), InterpolationClamp, 1 - InterpolationClamp);
                            var pa = origin + new Vec3(gi * h, gj * h, gk * h);
                            var direction = MarchingCubesTables.EdgeDirection[edge];
                            var offset = fraction * h;
                            positions[key] = direction switch
                            {
                                0 => pa + new Vec3(offset, 0, 0),
                                1 => pa + new Vec3(0, offset, 0),
                                _ => pa + new Vec3(0, 0, offset)
                            };
                        }
                    }
                }
            }
        }

        return keys.Count == 0 ? null : new BlockResult(keys.ToArray(), positions);
    }

    private static TriangleMesh Merge(BlockResult?[] results)
    {
        var estimatedVertices = 0;

        foreach (var result in results)
        {
            if (result is not null)
            {
                estimatedVertices += result.Positions.Count;
            }
        }

        var indexOf = new Dictionary<long, int>(estimatedVertices);
        var vertices = new List<Vec3>(estimatedVertices);
        var indices = new List<int>(estimatedVertices * 2);

        foreach (var result in results)
        {
            if (result is null)
            {
                continue;
            }

            foreach (var key in result.Keys)
            {
                if (!indexOf.TryGetValue(key, out var index))
                {
                    index = vertices.Count;
                    indexOf[key] = index;
                    vertices.Add(result.Positions[key]);
                }

                indices.Add(index);
            }
        }

        return new TriangleMesh(vertices.ToArray(), indices.ToArray());
    }

    private sealed record BlockResult(long[] Keys, Dictionary<long, Vec3> Positions);
}
