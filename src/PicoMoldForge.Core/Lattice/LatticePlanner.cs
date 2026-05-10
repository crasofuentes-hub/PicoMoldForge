namespace PicoMoldForge.Core.Lattice;

public sealed class LatticePlanner
{
    private const int MaxPreliminaryBeamCount = 20000;

    public LatticeCellPlan PlanSimpleGrid(LatticeRegionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid lattice region request: " + string.Join(" ", validationErrors),
                nameof(request));
        }

        var xPositions = BuildAxisPositions(request.MinXmm, request.MaxXmm, request.CellSizeMm);
        var yPositions = BuildAxisPositions(request.MinYmm, request.MaxYmm, request.CellSizeMm);
        var zPositions = BuildAxisPositions(request.MinZmm, request.MaxZmm, request.CellSizeMm);

        var beams = new List<LatticeBeamSegment>();
        var nextId = 1;

        AddXBeams(request, xPositions, yPositions, zPositions, beams, ref nextId);
        AddYBeams(request, xPositions, yPositions, zPositions, beams, ref nextId);
        AddZBeams(request, xPositions, yPositions, zPositions, beams, ref nextId);

        if (beams.Count > MaxPreliminaryBeamCount)
        {
            throw new ArgumentException(
                $"Preliminary lattice plan produced too many beams: {beams.Count}. Limit is {MaxPreliminaryBeamCount}.",
                nameof(request));
        }

        var warnings = new[]
        {
            "Phase 7A creates deterministic lattice contracts only; no PicoGK lattice geometry is generated yet.",
            "The current lattice is a simple orthogonal grid, not an optimized intelligent lattice.",
            "TargetRelativeDensity is validated but not yet used for structural optimization.",
            "This plan does not evaluate stress, thermal behavior, fatigue, printability, or manufacturability certification."
        };

        return new LatticeCellPlan(
            IsSuccessful: true,
            RegionName: request.RegionName,
            XNodeCount: xPositions.Count,
            YNodeCount: yPositions.Count,
            ZNodeCount: zPositions.Count,
            Beams: beams,
            Warnings: warnings);
    }

    private static IReadOnlyList<decimal> BuildAxisPositions(decimal min, decimal max, decimal cellSize)
    {
        var positions = new List<decimal>();
        var current = min;

        while (current < max)
        {
            positions.Add(current);
            current += cellSize;
        }

        if (positions.Count == 0 || positions[^1] != max)
        {
            positions.Add(max);
        }

        return positions;
    }

    private static void AddXBeams(
        LatticeRegionRequest request,
        IReadOnlyList<decimal> xPositions,
        IReadOnlyList<decimal> yPositions,
        IReadOnlyList<decimal> zPositions,
        List<LatticeBeamSegment> beams,
        ref int nextId)
    {
        foreach (var z in zPositions)
        {
            foreach (var y in yPositions)
            {
                for (var xIndex = 0; xIndex < xPositions.Count - 1; xIndex++)
                {
                    beams.Add(new LatticeBeamSegment(
                        FormatId(nextId++),
                        LatticeBeamAxis.X,
                        xPositions[xIndex],
                        y,
                        z,
                        xPositions[xIndex + 1],
                        y,
                        z,
                        request.BeamRadiusMm));
                }
            }
        }
    }

    private static void AddYBeams(
        LatticeRegionRequest request,
        IReadOnlyList<decimal> xPositions,
        IReadOnlyList<decimal> yPositions,
        IReadOnlyList<decimal> zPositions,
        List<LatticeBeamSegment> beams,
        ref int nextId)
    {
        foreach (var z in zPositions)
        {
            foreach (var x in xPositions)
            {
                for (var yIndex = 0; yIndex < yPositions.Count - 1; yIndex++)
                {
                    beams.Add(new LatticeBeamSegment(
                        FormatId(nextId++),
                        LatticeBeamAxis.Y,
                        x,
                        yPositions[yIndex],
                        z,
                        x,
                        yPositions[yIndex + 1],
                        z,
                        request.BeamRadiusMm));
                }
            }
        }
    }

    private static void AddZBeams(
        LatticeRegionRequest request,
        IReadOnlyList<decimal> xPositions,
        IReadOnlyList<decimal> yPositions,
        IReadOnlyList<decimal> zPositions,
        List<LatticeBeamSegment> beams,
        ref int nextId)
    {
        foreach (var y in yPositions)
        {
            foreach (var x in xPositions)
            {
                for (var zIndex = 0; zIndex < zPositions.Count - 1; zIndex++)
                {
                    beams.Add(new LatticeBeamSegment(
                        FormatId(nextId++),
                        LatticeBeamAxis.Z,
                        x,
                        y,
                        zPositions[zIndex],
                        x,
                        y,
                        zPositions[zIndex + 1],
                        request.BeamRadiusMm));
                }
            }
        }
    }

    private static string FormatId(int value)
    {
        return $"lattice-beam-{value:000000}";
    }
}