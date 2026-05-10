using System.Numerics;
using PicoMoldForge.Core.Lattice;
using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoLattice = global::PicoGK.Lattice;
using PicoMesh = global::PicoGK.Mesh;
using PicoVoxels = global::PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.Lattice;

public sealed class PicoLatticeDiagnosticExporter
{
    public PicoLatticeDiagnosticExportResult ExportLatticeDiagnostic(
        LatticeCellPlan plan,
        string outputStlPath,
        float voxelSizeMm = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.IsSuccessful)
        {
            throw new ArgumentException("Lattice plan must be successful.", nameof(plan));
        }

        if (plan.Beams.Count == 0)
        {
            throw new ArgumentException("Lattice plan must contain at least one beam.", nameof(plan));
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

            foreach (var beam in plan.Beams)
            {
                var start = new Vector3(
                    Convert.ToSingle(beam.StartXmm),
                    Convert.ToSingle(beam.StartYmm),
                    Convert.ToSingle(beam.StartZmm));

                var end = new Vector3(
                    Convert.ToSingle(beam.EndXmm),
                    Convert.ToSingle(beam.EndYmm),
                    Convert.ToSingle(beam.EndZmm));

                var radius = Convert.ToSingle(beam.BeamRadiusMm);

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
                "LatticeDiagnostic.stl is a preliminary diagnostic representation of the lattice plan.",
                "The current lattice is a deterministic orthogonal grid, not an optimized intelligent lattice.",
                "No structural simulation, fatigue validation, printability validation, or manufacturability certification is performed."
            };

            return new PicoLatticeDiagnosticExportResult(
                resolvedOutputPath,
                outputInfo.Length,
                plan.RegionName,
                plan.Beams.Count,
                diagnosticMesh.nTriangleCount(),
                diagnosticMesh.nVertexCount(),
                warnings);
        }, voxelSizeMm);
    }
}