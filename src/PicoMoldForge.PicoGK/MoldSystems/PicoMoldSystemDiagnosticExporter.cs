using System.Numerics;
using PicoMoldForge.Core.MoldSystems;
using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoLattice = global::PicoGK.Lattice;
using PicoMesh = global::PicoGK.Mesh;
using PicoVoxels = global::PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.MoldSystems;

public sealed class PicoMoldSystemDiagnosticExporter
{
    public PicoMoldSystemDiagnosticExportResult ExportMoldSystemDiagnostic(
        MoldSystemPlan plan,
        string outputStlPath,
        float voxelSizeMm = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.IsSuccessful)
        {
            throw new ArgumentException("Mold system plan must be successful.", nameof(plan));
        }

        if (string.IsNullOrWhiteSpace(outputStlPath))
        {
            throw new ArgumentException("Output STL path is required.", nameof(outputStlPath));
        }

        if (voxelSizeMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelSizeMm), "Voxel size must be greater than zero.");
        }

        var resolvedOutputPath = Path.GetFullPath(outputStlPath);
        var outputDirectory = Path.GetDirectoryName(resolvedOutputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        return PicoGkTaskRunner.Run(() =>
        {
            var lattice = new PicoLattice(PicoLibrary.oLibrary());

            AddEjectorPins(lattice, plan, voxelSizeMm);
            AddVents(lattice, plan, voxelSizeMm);
            AddInsertPockets(lattice, plan, voxelSizeMm);

            using var voxels = new PicoVoxels(in lattice);
            using var diagnosticMesh = voxels.mshAsMesh();

            diagnosticMesh.SaveToStlFile(
                resolvedOutputPath,
                PicoMesh.EStlUnit.MM,
                null,
                1.0f);

            var outputInfo = new FileInfo(resolvedOutputPath);

            var warnings = new[]
            {
                "MoldSystemDiagnostic.stl is a preliminary diagnostic representation of mold-system planning data.",
                "Ejector pins, vents, and insert pockets are represented as visual beam/frame geometry only.",
                "No collision, machining, plate, ejector mechanism, shutoff, tolerance, or manufacturability certification is performed."
            };

            return new PicoMoldSystemDiagnosticExportResult(
                resolvedOutputPath,
                outputInfo.Length,
                plan.Ejectors.Pins.Count,
                plan.Vents.Channels.Count,
                plan.Inserts.Pockets.Count,
                diagnosticMesh.nTriangleCount(),
                diagnosticMesh.nVertexCount(),
                warnings);
        }, voxelSizeMm);
    }

    private static void AddEjectorPins(PicoLattice lattice, MoldSystemPlan plan, float voxelSizeMm)
    {
        foreach (var pin in plan.Ejectors.Pins)
        {
            var start = new Vector3(
                Convert.ToSingle(pin.CenterXmm),
                Convert.ToSingle(pin.CenterYmm),
                Convert.ToSingle(pin.StartZmm));

            var end = new Vector3(
                Convert.ToSingle(pin.CenterXmm),
                Convert.ToSingle(pin.CenterYmm),
                Convert.ToSingle(pin.EndZmm));

            var radius = Math.Max(
                Convert.ToSingle(pin.DiameterMm / 2.0m),
                voxelSizeMm * 0.75f);

            lattice.AddBeam(
                ref start,
                radius,
                ref end,
                radius,
                bRoundCap: true);
        }
    }

    private static void AddVents(PicoLattice lattice, MoldSystemPlan plan, float voxelSizeMm)
    {
        foreach (var vent in plan.Vents.Channels)
        {
            var start = new Vector3(
                Convert.ToSingle(vent.StartXmm),
                Convert.ToSingle(vent.StartYmm),
                Convert.ToSingle(vent.StartZmm));

            var end = new Vector3(
                Convert.ToSingle(vent.EndXmm),
                Convert.ToSingle(vent.EndYmm),
                Convert.ToSingle(vent.EndZmm));

            var rawRadius = Convert.ToSingle(Math.Max(vent.WidthMm, vent.DepthMm) / 2.0m);
            var radius = Math.Max(rawRadius, voxelSizeMm * 0.75f);

            lattice.AddBeam(
                ref start,
                radius,
                ref end,
                radius,
                bRoundCap: true);
        }
    }

    private static void AddInsertPockets(PicoLattice lattice, MoldSystemPlan plan, float voxelSizeMm)
    {
        foreach (var pocket in plan.Inserts.Pockets)
        {
            var radius = Math.Max(
                Convert.ToSingle(Math.Max(pocket.ClearanceMm, 0.5m) / 2.0m),
                voxelSizeMm * 0.75f);

            AddBoxFrame(
                lattice,
                Convert.ToSingle(pocket.MinXmm),
                Convert.ToSingle(pocket.MinYmm),
                Convert.ToSingle(pocket.MinZmm),
                Convert.ToSingle(pocket.MaxXmm),
                Convert.ToSingle(pocket.MaxYmm),
                Convert.ToSingle(pocket.MaxZmm),
                radius);
        }
    }

    private static void AddBoxFrame(
        PicoLattice lattice,
        float minX,
        float minY,
        float minZ,
        float maxX,
        float maxY,
        float maxZ,
        float radius)
    {
        AddBeam(lattice, minX, minY, minZ, maxX, minY, minZ, radius);
        AddBeam(lattice, minX, maxY, minZ, maxX, maxY, minZ, radius);
        AddBeam(lattice, minX, minY, maxZ, maxX, minY, maxZ, radius);
        AddBeam(lattice, minX, maxY, maxZ, maxX, maxY, maxZ, radius);

        AddBeam(lattice, minX, minY, minZ, minX, maxY, minZ, radius);
        AddBeam(lattice, maxX, minY, minZ, maxX, maxY, minZ, radius);
        AddBeam(lattice, minX, minY, maxZ, minX, maxY, maxZ, radius);
        AddBeam(lattice, maxX, minY, maxZ, maxX, maxY, maxZ, radius);

        AddBeam(lattice, minX, minY, minZ, minX, minY, maxZ, radius);
        AddBeam(lattice, maxX, minY, minZ, maxX, minY, maxZ, radius);
        AddBeam(lattice, minX, maxY, minZ, minX, maxY, maxZ, radius);
        AddBeam(lattice, maxX, maxY, minZ, maxX, maxY, maxZ, radius);
    }

    private static void AddBeam(
        PicoLattice lattice,
        float startX,
        float startY,
        float startZ,
        float endX,
        float endY,
        float endZ,
        float radius)
    {
        var start = new Vector3(startX, startY, startZ);
        var end = new Vector3(endX, endY, endZ);

        lattice.AddBeam(
            ref start,
            radius,
            ref end,
            radius,
            bRoundCap: true);
    }
}