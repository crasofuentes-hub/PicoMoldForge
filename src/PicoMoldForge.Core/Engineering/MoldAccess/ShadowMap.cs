using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.MoldAccess;

/// <summary>
/// Height field of a mesh seen along the parting axis: for each cell of a grid on the parting plane, the highest
/// point of the part in that column for each opening direction (+axis and -axis). A value is an upper bound of
/// the part over the whole cell (the triangle plane bounded at the cell corners and by its highest vertex).
/// Triangles seen edge-on (walls parallel to the axis) are left out: on a closed surface their edges are shared
/// with the faces that cover the neighbouring cells. A triangle covers the cells its footprint overlaps; a
/// footprint ending exactly on a cell boundary does not cover the next cell. Free cells are confirmed by a ray.
/// </summary>
internal sealed class ShadowMap
{
    private readonly double _min1;
    private readonly double _min2;
    private readonly double _cell;
    private readonly int _n1;
    private readonly int _n2;
    private readonly float[] _positive;
    private readonly float[] _negative;

    public ShadowMap(IReadOnlyList<MeshTriangle> triangles, Vec3d pull, Vec3d sideA, Vec3d sideB, double cell)
    {
        if (!(cell > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(cell), "Cell size must be positive.");
        }

        double min1 = double.PositiveInfinity, min2 = double.PositiveInfinity;
        double max1 = double.NegativeInfinity, max2 = double.NegativeInfinity;

        void Include(MeshVertex vertex)
        {
            var v = Vec3d.From(vertex);
            var x = Vec3d.Dot(v, sideA);
            var y = Vec3d.Dot(v, sideB);
            min1 = Math.Min(min1, x);
            max1 = Math.Max(max1, x);
            min2 = Math.Min(min2, y);
            max2 = Math.Max(max2, y);
        }

        foreach (var t in triangles)
        {
            Include(t.A);
            Include(t.B);
            Include(t.C);
        }

        _cell = cell;
        _min1 = min1 - cell;
        _min2 = min2 - cell;
        _n1 = (int)Math.Ceiling((max1 - _min1) / cell) + 2;
        _n2 = (int)Math.Ceiling((max2 - _min2) / cell) + 2;
        _positive = new float[_n1 * _n2];
        _negative = new float[_n1 * _n2];
        Array.Fill(_positive, float.NegativeInfinity);
        Array.Fill(_negative, float.NegativeInfinity);

        Span<double> x3 = stackalloc double[3];
        Span<double> y3 = stackalloc double[3];
        Span<double> h3 = stackalloc double[3];

        foreach (var t in triangles)
        {
            var a = Vec3d.From(t.A);
            var b = Vec3d.From(t.B);
            var c = Vec3d.From(t.C);
            (x3[0], y3[0], h3[0]) = (Vec3d.Dot(a, sideA), Vec3d.Dot(a, sideB), Vec3d.Dot(a, pull));
            (x3[1], y3[1], h3[1]) = (Vec3d.Dot(b, sideA), Vec3d.Dot(b, sideB), Vec3d.Dot(b, pull));
            (x3[2], y3[2], h3[2]) = (Vec3d.Dot(c, sideA), Vec3d.Dot(c, sideB), Vec3d.Dot(c, pull));
            Rasterize(x3, y3, h3);
        }
    }

    public double Cell => _cell;

    /// <summary>
    /// Smallest s in [0, limit) at which the column through (x, y) + s (ux, uy) holds nothing of the part beyond
    /// <paramref name="height"/> along the opening direction of <paramref name="half"/> (+1 or -1); +infinity when
    /// none. Cells are visited in order along the line, so the first free cell is found.
    /// </summary>
    public double FirstFree(double x, double y, double ux, double uy, int half, double height, double limit)
    {
        var values = half > 0 ? _positive : _negative;
        var gx = (x - _min1) / _cell;
        var gy = (y - _min2) / _cell;
        var i = (int)Math.Floor(gx);
        var j = (int)Math.Floor(gy);
        var stepI = ux > 0 ? 1 : ux < 0 ? -1 : 0;
        var stepJ = uy > 0 ? 1 : uy < 0 ? -1 : 0;
        var nextI = stepI == 0 ? double.PositiveInfinity : (((stepI > 0 ? i + 1 : i) - gx) * _cell) / ux;
        var nextJ = stepJ == 0 ? double.PositiveInfinity : (((stepJ > 0 ? j + 1 : j) - gy) * _cell) / uy;
        var deltaI = stepI == 0 ? double.PositiveInfinity : _cell / Math.Abs(ux);
        var deltaJ = stepJ == 0 ? double.PositiveInfinity : _cell / Math.Abs(uy);
        var s = 0.0;

        while (s < limit)
        {
            if (i < 0 || j < 0 || i >= _n1 || j >= _n2 || values[(j * _n1) + i] <= height)
            {
                return s;
            }

            if (nextI < nextJ)
            {
                s = nextI;
                nextI += deltaI;
                i += stepI;
            }
            else
            {
                s = nextJ;
                nextJ += deltaJ;
                j += stepJ;
            }
        }

        return double.PositiveInfinity;
    }

    private void Rasterize(ReadOnlySpan<double> x, ReadOnlySpan<double> y, ReadOnlySpan<double> h)
    {
        var maxHeight = Math.Max(h[0], Math.Max(h[1], h[2]));
        var minHeight = Math.Min(h[0], Math.Min(h[1], h[2]));

        // Plane h = h0 + gx (x - x0) + gy (y - y0); a triangle seen edge-on has no usable plane.
        var e1x = x[1] - x[0];
        var e1y = y[1] - y[0];
        var e2x = x[2] - x[0];
        var e2y = y[2] - y[0];
        var determinant = (e1x * e2y) - (e2x * e1y);
        var scale = Math.Max(Math.Abs(e1x) + Math.Abs(e1y), Math.Abs(e2x) + Math.Abs(e2y));
        var hasPlane = Math.Abs(determinant) > 1e-9 * scale * scale;
        double gradX = 0, gradY = 0;

        if (hasPlane)
        {
            var dh1 = h[1] - h[0];
            var dh2 = h[2] - h[0];
            gradX = ((dh1 * e2y) - (dh2 * e1y)) / determinant;
            gradY = ((dh2 * e1x) - (dh1 * e2x)) / determinant;
        }

        if (!hasPlane)
        {
            return;
        }

        var halfSpread = (Math.Abs(gradX) + Math.Abs(gradY)) * _cell / 2;
        var minY = Math.Min(y[0], Math.Min(y[1], y[2]));
        var maxY = Math.Max(y[0], Math.Max(y[1], y[2]));
        var (j0, j1) = Cells(minY, maxY, _min2, _n2);

        for (var j = j0; j <= j1; j++)
        {
            var bandLow = _min2 + (j * _cell);
            var bandHigh = bandLow + _cell;

            if (!BandRange(x, y, bandLow, bandHigh, out var low, out var high))
            {
                continue;
            }

            var (i0, i1) = Cells(low, high, _min1, _n1);
            var centerY = bandLow + (_cell / 2);

            for (var i = i0; i <= i1; i++)
            {
                var centerX = _min1 + ((i + 0.5) * _cell);
                var atCenter = h[0] + (gradX * (centerX - x[0])) + (gradY * (centerY - y[0]));
                var up = Math.Min(maxHeight, atCenter + halfSpread);
                var down = Math.Min(-minHeight, -atCenter + halfSpread);
                var index = (j * _n1) + i;
                _positive[index] = Math.Max(_positive[index], RoundUp(up));
                _negative[index] = Math.Max(_negative[index], RoundUp(down));
            }
        }
    }

    /// <summary>Cells overlapped by [low, high]; an end exactly on a cell boundary does not reach into the next cell.</summary>
    private (int First, int Last) Cells(double low, double high, double origin, int count)
    {
        var first = (int)Math.Floor((low - origin) / _cell);
        var last = Math.Max(first, (int)Math.Ceiling((high - origin) / _cell) - 1);
        return (Math.Max(0, first), Math.Min(count - 1, last));
    }

    /// <summary>x extent of the triangle within the band low &lt;= y &lt;= high (its vertices in the band and its edge crossings).</summary>
    private static bool BandRange(ReadOnlySpan<double> x, ReadOnlySpan<double> y, double low, double high, out double min, out double max)
    {
        min = double.PositiveInfinity;
        max = double.NegativeInfinity;

        for (var k = 0; k < 3; k++)
        {
            if (y[k] >= low && y[k] <= high)
            {
                min = Math.Min(min, x[k]);
                max = Math.Max(max, x[k]);
            }

            var m = (k + 1) % 3;
            Cross(x[k], y[k], x[m], y[m], low, ref min, ref max);
            Cross(x[k], y[k], x[m], y[m], high, ref min, ref max);
        }

        return min <= max;
    }

    private static void Cross(double xa, double ya, double xb, double yb, double level, ref double min, ref double max)
    {
        if ((ya - level) * (yb - level) < 0)
        {
            var crossing = xa + ((xb - xa) * (level - ya) / (yb - ya));
            min = Math.Min(min, crossing);
            max = Math.Max(max, crossing);
        }
    }

    private static float RoundUp(double value)
    {
        var rounded = (float)value;
        return rounded < value ? MathF.BitIncrement(rounded) : rounded;
    }
}
