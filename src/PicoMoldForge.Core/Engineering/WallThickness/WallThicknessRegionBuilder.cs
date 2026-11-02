namespace PicoMoldForge.Core.Engineering.WallThickness;

/// <summary>Thickness bands, in the order <see cref="VoxelWallThicknessAnalyzer"/> classifies samples.</summary>
public enum WallThicknessBand
{
    Thin,
    AbruptLow,
    Nominal,
    AbruptHigh,
    Thick
}

/// <summary>
/// Band limits matching <see cref="VoxelWallThicknessAnalyzer"/>: thin below the minimum, thick above the
/// maximum, abrupt when the relative difference to the nominal exceeds the ratio (split into low and high so a
/// region never mixes both sides), nominal otherwise.
/// </summary>
public sealed record WallThicknessBands(double MinimumMm, double MaximumMm, double NominalMm, double AbruptChangeRatio)
{
    public WallThicknessBand Classify(double thicknessMm)
    {
        if (thicknessMm < MinimumMm)
        {
            return WallThicknessBand.Thin;
        }

        if (thicknessMm > MaximumMm)
        {
            return WallThicknessBand.Thick;
        }

        if (Math.Abs(thicknessMm - NominalMm) / NominalMm > AbruptChangeRatio)
        {
            return thicknessMm < NominalMm ? WallThicknessBand.AbruptLow : WallThicknessBand.AbruptHigh;
        }

        return WallThicknessBand.Nominal;
    }
}

/// <summary>
/// Connected wall area in one band. <paramref name="ThicknessMm"/> is the area-weighted median of its
/// measurements; <paramref name="Centroid"/> is the area-weighted centre of its triangles (where to look).
/// </summary>
public sealed record WallThicknessRegion(
    string RegionId,
    WallThicknessBand Band,
    double ThicknessMm,
    double MinimumThicknessMm,
    double MaximumThicknessMm,
    double AreaMm2,
    int TriangleCount,
    MeshVertex Centroid);

/// <summary>Regions kept, and the small regions left out (count and total area) with the area limit used.</summary>
public sealed record WallThicknessRegionSet(
    IReadOnlyList<WallThicknessRegion> Regions,
    int ExcludedRegionCount,
    double ExcludedAreaMm2,
    double MinimumRegionAreaMm2);

/// <summary>
/// Groups wall measurements into regions: wall triangles that share an edge and fall in the same band. Shared
/// edges are found by welding vertices with identical coordinates (STL repeats them exactly).
/// </summary>
public static class WallThicknessRegionBuilder
{
    /// <summary>Area-weighted median of the wall measurements (the dominant wall thickness), or 0 without walls.</summary>
    public static double NominalThickness(IReadOnlyList<WallThicknessMeasurement> measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);
        return WeightedMedian(measurements.Where(m => m.IsWall).Select(m => (m.ThicknessMm, m.AreaMm2)));
    }

    /// <summary>
    /// Builds the regions and leaves out those smaller than <paramref name="minimumRegionAreaMm2"/> (default: the
    /// thin limit squared). On real meshes, slivers at sharp edges give tiny regions with meaningless readings
    /// (0.5 mm2 of "thin" regions on a flange whose thinnest wall is 10 mm); the left-out count and area are
    /// returned so a report can state them instead of hiding them.
    /// </summary>
    public static WallThicknessRegionSet Build(
        IReadOnlyList<MeshTriangle> triangles,
        IReadOnlyList<WallThicknessMeasurement> measurements,
        WallThicknessBands bands,
        double? minimumRegionAreaMm2 = null)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        ArgumentNullException.ThrowIfNull(measurements);
        ArgumentNullException.ThrowIfNull(bands);
        var minimumArea = minimumRegionAreaMm2 ?? (bands.MinimumMm * bands.MinimumMm);

        if (!(minimumArea >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRegionAreaMm2), "Minimum region area cannot be negative.");
        }

        if (triangles.Count != measurements.Count)
        {
            throw new ArgumentException("There must be one measurement per triangle.", nameof(measurements));
        }

        var band = measurements.Select(m => m.IsWall ? bands.Classify(m.ThicknessMm) : (WallThicknessBand?)null).ToArray();
        var neighbours = EdgeNeighbours(triangles);
        var region = Enumerable.Repeat(-1, triangles.Count).ToArray();
        var groups = new List<List<int>>();
        var queue = new Queue<int>();

        for (var seed = 0; seed < triangles.Count; seed++)
        {
            if (band[seed] is null || region[seed] >= 0)
            {
                continue;
            }

            var members = new List<int>();
            region[seed] = groups.Count;
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                members.Add(current);

                foreach (var next in neighbours[current])
                {
                    if (region[next] < 0 && band[next] == band[seed])
                    {
                        region[next] = groups.Count;
                        queue.Enqueue(next);
                    }
                }
            }

            groups.Add(members);
        }

        var regions = groups
            .Select(members =>
            {
                var area = members.Sum(i => measurements[i].AreaMm2);
                var centroid = new Vec3d(0, 0, 0);

                foreach (var i in members)
                {
                    var t = triangles[i];
                    var c = (Vec3d.From(t.A) + Vec3d.From(t.B) + Vec3d.From(t.C)) * (1.0 / 3.0);
                    centroid += c * (measurements[i].AreaMm2 / Math.Max(area, 1e-300));
                }

                return (
                    Band: band[members[0]]!.Value,
                    Thickness: WeightedMedian(members.Select(i => (measurements[i].ThicknessMm, measurements[i].AreaMm2))),
                    Min: members.Min(i => measurements[i].ThicknessMm),
                    Max: members.Max(i => measurements[i].ThicknessMm),
                    Area: area,
                    Count: members.Count,
                    Centroid: new MeshVertex(centroid.X, centroid.Y, centroid.Z),
                    First: members.Min());
            })
            .OrderBy(r => r.Band)
            .ThenByDescending(r => r.Area)
            .ThenBy(r => r.First)
            .ToList();

        var small = regions.Where(r => r.Area < minimumArea).ToArray();
        var numberInBand = new Dictionary<WallThicknessBand, int>();

        var kept = regions
            .Where(r => r.Area >= minimumArea)
            .Select(r =>
            {
                numberInBand[r.Band] = numberInBand.TryGetValue(r.Band, out var n) ? n + 1 : 1;
                var id = FormattableString.Invariant($"wall-{BandName(r.Band)}-{numberInBand[r.Band]:000}");
                return new WallThicknessRegion(id, r.Band, r.Thickness, r.Min, r.Max, r.Area, r.Count, r.Centroid);
            })
            .ToArray();

        return new WallThicknessRegionSet(kept, small.Length, small.Sum(r => r.Area), minimumArea);
    }

    public static string BandName(WallThicknessBand band) => band switch
    {
        WallThicknessBand.Thin => "thin",
        WallThicknessBand.AbruptLow => "abrupt-low",
        WallThicknessBand.Nominal => "nominal",
        WallThicknessBand.AbruptHigh => "abrupt-high",
        WallThicknessBand.Thick => "thick",
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unknown band.")
    };

    private static double WeightedMedian(IEnumerable<(double Value, double Weight)> samples)
    {
        var sorted = samples.Where(s => s.Weight > 0).OrderBy(s => s.Value).ToArray();

        if (sorted.Length == 0)
        {
            return 0;
        }

        var half = sorted.Sum(s => s.Weight) / 2;
        var accumulated = 0.0;

        foreach (var (value, weight) in sorted)
        {
            accumulated += weight;

            if (accumulated >= half)
            {
                return value;
            }
        }

        return sorted[^1].Value;
    }

    private static List<int>[] EdgeNeighbours(IReadOnlyList<MeshTriangle> triangles)
    {
        var vertexIds = new Dictionary<MeshVertex, int>();
        int Id(MeshVertex v)
        {
            if (!vertexIds.TryGetValue(v, out var id))
            {
                id = vertexIds.Count;
                vertexIds[v] = id;
            }

            return id;
        }

        var byEdge = new Dictionary<(int, int), List<int>>();
        var ids = new int[triangles.Count * 3];

        for (var t = 0; t < triangles.Count; t++)
        {
            ids[3 * t] = Id(triangles[t].A);
            ids[(3 * t) + 1] = Id(triangles[t].B);
            ids[(3 * t) + 2] = Id(triangles[t].C);

            for (var e = 0; e < 3; e++)
            {
                var a = ids[(3 * t) + e];
                var b = ids[(3 * t) + ((e + 1) % 3)];
                var key = a < b ? (a, b) : (b, a);

                if (!byEdge.TryGetValue(key, out var list))
                {
                    list = new List<int>(2);
                    byEdge[key] = list;
                }

                list.Add(t);
            }
        }

        var neighbours = new List<int>[triangles.Count];

        for (var t = 0; t < triangles.Count; t++)
        {
            neighbours[t] = new List<int>(3);
        }

        foreach (var list in byEdge.Values)
        {
            foreach (var t in list)
            {
                foreach (var other in list)
                {
                    if (other != t && !neighbours[t].Contains(other))
                    {
                        neighbours[t].Add(other);
                    }
                }
            }
        }

        return neighbours;
    }
}
