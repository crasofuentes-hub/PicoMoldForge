using System.Numerics;
using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Parting;
using PicoMoldForge.PicoGK.Runtime;
using PicoLattice = global::PicoGK.Lattice;
using PicoLibrary = global::PicoGK.Library;
using PicoMesh = global::PicoGK.Mesh;
using PicoVoxels = global::PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.BooleanGeometry;

/// <summary>
/// Builds the functional mold halves: each half of the block minus the cavity, minus the feed, the ejector pin holes,
/// the cooling channels and the guide bores that belong to it, plus its guide pins; writes MovingHalf.stl and
/// FixedHalf.stl and measures, on the voxels, the steel each system removed or added and the steel both halves share.
/// </summary>
public sealed class PicoFunctionalMoldHalvesGenerator
{
    public FunctionalMoldHalvesResult Generate(FunctionalMoldHalvesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = request.Validate();

        if (errors.Count > 0)
        {
            throw new ArgumentException("Invalid functional mold halves request: " + string.Join(" ", errors), nameof(request));
        }

        var source = Path.GetFullPath(request.CavitySourcePath);
        var output = Path.GetFullPath(request.OutputDirectory);

        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Cavity STL file was not found.", source);
        }

        Directory.CreateDirectory(output);

        return PicoGkTaskRunner.Run(() =>
        {
            var library = PicoLibrary.oLibrary();
            var cavityMesh = PicoMesh.mshFromStlFile(source, default, 1.0f, null, library);

            try
            {
                using var cavity = new PicoVoxels(in cavityMesh);
                var (moving, movingVoxels) = BuildHalf(library, cavity, request, request.MovingHalf, "Moving", Path.Combine(output, FunctionalMoldHalvesRequest.MovingHalfFileName));
                var (fixedHalf, fixedVoxels) = BuildHalf(library, cavity, request, -request.MovingHalf, "Fixed", Path.Combine(output, FunctionalMoldHalvesRequest.FixedHalfFileName));

                try
                {
                    using var overlap = movingVoxels.voxBoolIntersect(in fixedVoxels);
                    overlap.CalculateProperties(out var overlapVolume, out _);

                    var warnings = new[]
                    {
                        "MovingHalf.stl and FixedHalf.stl are the mold halves with the feed, the ejector pin holes, the cooling channels and the guide bores cut, and the guide pins added, by voxel booleans.",
                        "Features are cut at the voxel size of the run; holes and pins finer than a few voxels are approximate."
                    };

                    return new FunctionalMoldHalvesResult(moving, fixedHalf, overlapVolume, request.VoxelSizeMm, warnings);
                }
                finally
                {
                    movingVoxels.Dispose();
                    fixedVoxels.Dispose();
                }
            }
            finally
            {
                cavityMesh.Dispose();
            }
        }, request.VoxelSizeMm);
    }

    private static (FunctionalMoldHalfResult Result, PicoVoxels Voxels) BuildHalf(
        PicoLibrary library,
        PicoVoxels cavity,
        FunctionalMoldHalvesRequest request,
        int half,
        string role,
        string path)
    {
        var bounds = HalfBounds(request.BlockBounds, request.SplitAxis, request.PartingPlaneOffsetMm, half);
        using var blockMesh = BlockMesh(bounds, library);
        using var block = new PicoVoxels(in blockMesh);
        var voxels = block.voxBoolSubtract(in cavity);
        voxels.CalculateProperties(out var afterCavity, out _);
        var current = (double)afterCavity;
        var systems = new List<MoldHalfSystemVolume>();

        foreach (var system in FunctionalMoldHalvesRequest.SystemOrder)
        {
            var features = request.Features
                .Where(f => f.System == system && (f.Half == half || f.Half == 0))
                .ToArray();

            if (features.Length == 0)
            {
                continue;
            }

            var lattice = new PicoLattice(library);

            foreach (var feature in features)
            {
                var start = new Vector3((float)feature.StartXmm, (float)feature.StartYmm, (float)feature.StartZmm);
                var end = new Vector3((float)feature.EndXmm, (float)feature.EndYmm, (float)feature.EndZmm);
                var radius = (float)feature.RadiusMm;
                lattice.AddBeam(in start, radius, in end, radius, feature.RoundEnds);
            }

            using var featureVoxels = new PicoVoxels(in lattice);
            var adds = features[0].AddsSteel;

            if (adds)
            {
                voxels.BoolAdd(in featureVoxels);
            }
            else
            {
                voxels.BoolSubtract(in featureVoxels);
            }

            voxels.CalculateProperties(out var after, out _);
            systems.Add(new MoldHalfSystemVolume(system, adds ? 0 : Math.Max(0, current - after), adds ? Math.Max(0, after - current) : 0));
            current = after;
        }

        voxels.CalculateProperties(out var finalVolume, out var box);
        using var mesh = voxels.mshAsMesh();
        mesh.SaveToStlFile(path, PicoMesh.EStlUnit.MM, null, 1.0f);

        var (min, max) = request.SplitAxis switch
        {
            PartingAxis.X => (box.vecMin.X, box.vecMax.X),
            PartingAxis.Y => (box.vecMin.Y, box.vecMax.Y),
            _ => (box.vecMin.Z, box.vecMax.Z)
        };

        var result = new FunctionalMoldHalfResult(
            role,
            half,
            path,
            afterCavity,
            finalVolume,
            systems,
            min,
            max,
            mesh.nTriangleCount());

        return (result, voxels);
    }

    /// <summary>The half of the block on side <paramref name="half"/> of the split (+1: from the plane up to the block maximum).</summary>
    private static MoldBlockBounds HalfBounds(MoldBlockBounds block, PartingAxis axis, decimal offset, int half) => axis switch
    {
        PartingAxis.X => half > 0 ? block with { MinXmm = offset } : block with { MaxXmm = offset },
        PartingAxis.Y => half > 0 ? block with { MinYmm = offset } : block with { MaxYmm = offset },
        PartingAxis.Z => half > 0 ? block with { MinZmm = offset } : block with { MaxZmm = offset },
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
    };

    private static PicoMesh BlockMesh(MoldBlockBounds b, PicoLibrary library)
    {
        var mesh = new PicoMesh(library);
        var (x0, y0, z0) = ((float)b.MinXmm, (float)b.MinYmm, (float)b.MinZmm);
        var (x1, y1, z1) = ((float)b.MaxXmm, (float)b.MaxYmm, (float)b.MaxZmm);

        void Triangle(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz) =>
            mesh.nAddTriangle(new Vector3(ax, ay, az), new Vector3(bx, by, bz), new Vector3(cx, cy, cz));

        Triangle(x0, y0, z0, x1, y1, z0, x1, y0, z0);
        Triangle(x0, y0, z0, x0, y1, z0, x1, y1, z0);
        Triangle(x0, y0, z1, x1, y0, z1, x1, y1, z1);
        Triangle(x0, y0, z1, x1, y1, z1, x0, y1, z1);
        Triangle(x0, y0, z0, x1, y0, z0, x1, y0, z1);
        Triangle(x0, y0, z0, x1, y0, z1, x0, y0, z1);
        Triangle(x0, y1, z0, x1, y1, z1, x1, y1, z0);
        Triangle(x0, y1, z0, x0, y1, z1, x1, y1, z1);
        Triangle(x0, y0, z0, x0, y0, z1, x0, y1, z1);
        Triangle(x0, y0, z0, x0, y1, z1, x0, y1, z0);
        Triangle(x1, y0, z0, x1, y1, z0, x1, y1, z1);
        Triangle(x1, y0, z0, x1, y1, z1, x1, y0, z1);
        return mesh;
    }
}
