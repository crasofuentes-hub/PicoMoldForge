using System.Numerics;
using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoLattice = global::PicoGK.Lattice;
using PicoMesh = global::PicoGK.Mesh;
using PicoVoxels = global::PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.GateSystem;

public sealed class PicoGateRunnerSprueDiagnosticExporter
{
    public PicoGateRunnerSprueDiagnosticExportResult ExportGateRunnerSprueDiagnostic(
        GateRunnerSprueGenerationInput input,
        string outputStlPath,
        float voxelSizeMm = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Segments);

        if (input.Segments.Count == 0)
        {
            throw new ArgumentException("Gate/runner/sprue input must contain at least one segment.", nameof(input));
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

        var warnings = new List<string>
        {
            "GateRunnerSprueDiagnostic.stl is preliminary feed-system diagnostic geometry and is not production tooling."
        };

        return PicoGkTaskRunner.Run(() =>
        {
            var lattice = new PicoLattice(PicoLibrary.oLibrary());

            foreach (var segment in input.Segments)
            {
                AddBeam(lattice, segment, voxelSizeMm);
            }

            using var voxels = new PicoVoxels(in lattice);
            using var diagnosticMesh = voxels.mshAsMesh();

            diagnosticMesh.SaveToStlFile(
                resolvedOutputPath,
                PicoMesh.EStlUnit.MM,
                null,
                1.0f);

            var output = new FileInfo(resolvedOutputPath);

            return new PicoGateRunnerSprueDiagnosticExportResult(
                resolvedOutputPath,
                output.Length,
                input.Segments.Count,
                diagnosticMesh.nTriangleCount(),
                diagnosticMesh.nVertexCount(),
                warnings);
        }, voxelSizeMm);
    }

    private static void AddBeam(
        PicoLattice lattice,
        GateRunnerSprueSegment segment,
        float voxelSizeMm)
    {
        var start = new Vector3(
            Convert.ToSingle(segment.Start.Xmm),
            Convert.ToSingle(segment.Start.Ymm),
            Convert.ToSingle(segment.Start.Zmm));

        var end = new Vector3(
            Convert.ToSingle(segment.End.Xmm),
            Convert.ToSingle(segment.End.Ymm),
            Convert.ToSingle(segment.End.Zmm));

        var radius = Math.Max(
            Convert.ToSingle(segment.HydraulicDiameterMm / 2.0m),
            voxelSizeMm * 0.50f);

        lattice.AddBeam(
            in start,
            radius,
            in end,
            radius,
            bRoundCap: true);
    }
}