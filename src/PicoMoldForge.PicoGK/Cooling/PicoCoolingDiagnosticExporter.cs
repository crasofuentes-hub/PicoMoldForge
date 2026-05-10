using System.Numerics;
using PicoMoldForge.Core.Cooling;
using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoLattice = global::PicoGK.Lattice;
using PicoMesh = global::PicoGK.Mesh;
using PicoVoxels = global::PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.Cooling;

public sealed class PicoCoolingDiagnosticExporter
{
    public PicoCoolingDiagnosticExportResult ExportCoolingDiagnostic(
        CoolingChannelPlan plan,
        string outputStlPath,
        float voxelSizeMm = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.IsSuccessful)
        {
            throw new ArgumentException("Cooling channel plan must be successful.", nameof(plan));
        }

        if (plan.Segments.Count == 0)
        {
            throw new ArgumentException("Cooling channel plan must contain at least one segment.", nameof(plan));
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

            foreach (var segment in plan.Segments)
            {
                var start = new Vector3(
                    Convert.ToSingle(segment.StartXmm),
                    Convert.ToSingle(segment.StartYmm),
                    Convert.ToSingle(segment.StartZmm));

                var end = new Vector3(
                    Convert.ToSingle(segment.EndXmm),
                    Convert.ToSingle(segment.EndYmm),
                    Convert.ToSingle(segment.EndZmm));

                var radius = Convert.ToSingle(segment.DiameterMm / 2.0m);

                lattice.AddBeam(
                    ref start,
                    radius,
                    ref end,
                    radius,
                    bRoundCap: true);
            }

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
                "CoolingDiagnostic.stl is a preliminary diagnostic representation of cooling channels.",
                "Cooling geometry is not subtracted from cavity/core geometry in Phase 6B.",
                "No thermal simulation, pressure-drop validation, drilling access validation, or manufacturability certification is performed."
            };

            return new PicoCoolingDiagnosticExportResult(
                resolvedOutputPath,
                outputInfo.Length,
                plan.Segments.Count,
                diagnosticMesh.nTriangleCount(),
                diagnosticMesh.nVertexCount(),
                warnings);
        }, voxelSizeMm);
    }
}