using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Meshing;

/// <summary>
/// Independent volume estimate: counts cell centers inside the solid (sign of the distance only, no
/// triangles). Used to cross-check the mesh volume, so a mesher defect cannot validate itself.
/// Deterministic (integer counts).
/// </summary>
public static class VoxelVolumeEstimator
{
    public static double EstimateVolume(ISdf sdf, double cellSizeMm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sdf);

        if (!(cellSizeMm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(cellSizeMm), "Cell size must be positive.");
        }

        const int block = 8;
        var h = cellSizeMm;
        var bounds = sdf.Bounds.Expand(h);
        var nx = (int)Math.Ceiling(bounds.Size.X / h);
        var ny = (int)Math.Ceiling(bounds.Size.Y / h);
        var nz = (int)Math.Ceiling(bounds.Size.Z / h);
        var bx = (nx + block - 1) / block;
        var by = (ny + block - 1) / block;
        var bz = (nz + block - 1) / block;
        var lipschitz = Math.Max(1.0, sdf.Lipschitz) * 1.05;
        long inside = 0;

        Parallel.For(0L, (long)bx * by * bz, new ParallelOptions { CancellationToken = cancellationToken }, () => 0L, (index, _, local) =>
        {
            var ib = (int)(index % bx);
            var jb = (int)((index / bx) % by);
            var kb = (int)(index / ((long)bx * by));
            var i0 = ib * block;
            var j0 = jb * block;
            var k0 = kb * block;
            var cx = Math.Min(block, nx - i0);
            var cy = Math.Min(block, ny - j0);
            var cz = Math.Min(block, nz - k0);
            var center = bounds.Min + new Vec3((i0 + (cx / 2.0)) * h, (j0 + (cy / 2.0)) * h, (k0 + (cz / 2.0)) * h);
            var halfDiagonal = 0.5 * h * Math.Sqrt((cx * cx) + (cy * cy) + (cz * cz));
            var d = sdf.Distance(center);

            if (Math.Abs(d) > lipschitz * halfDiagonal)
            {
                return d < 0 ? local + ((long)cx * cy * cz) : local;
            }

            var count = 0L;

            for (var k = 0; k < cz; k++)
            {
                for (var j = 0; j < cy; j++)
                {
                    for (var i = 0; i < cx; i++)
                    {
                        var p = bounds.Min + new Vec3((i0 + i + 0.5) * h, (j0 + j + 0.5) * h, (k0 + k + 0.5) * h);

                        if (sdf.Distance(p) < 0)
                        {
                            count++;
                        }
                    }
                }
            }

            return local + count;
        }, local => Interlocked.Add(ref inside, local));

        return inside * h * h * h;
    }
}
