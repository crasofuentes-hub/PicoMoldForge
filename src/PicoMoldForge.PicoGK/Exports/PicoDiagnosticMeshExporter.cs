using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoMesh = global::PicoGK.Mesh;
using PicoVoxels = global::PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.Exports;

public sealed class PicoDiagnosticMeshExporter
{
    public PicoDiagnosticMeshExportResult ExportVoxelizedDiagnosticMesh(
        string sourceStlPath,
        string outputStlPath,
        float voxelSizeMm = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(sourceStlPath))
        {
            throw new ArgumentException("Source STL path is required.", nameof(sourceStlPath));
        }

        if (string.IsNullOrWhiteSpace(outputStlPath))
        {
            throw new ArgumentException("Output STL path is required.", nameof(outputStlPath));
        }

        if (voxelSizeMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelSizeMm), "Voxel size must be greater than zero.");
        }

        var resolvedSourcePath = Path.GetFullPath(sourceStlPath);
        var resolvedOutputPath = Path.GetFullPath(outputStlPath);
        var outputDirectory = Path.GetDirectoryName(resolvedOutputPath);

        if (!File.Exists(resolvedSourcePath))
        {
            throw new FileNotFoundException("Source STL file was not found.", resolvedSourcePath);
        }

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        return PicoGkTaskRunner.Run(() =>
        {
            var sourceMesh = PicoMesh.mshFromStlFile(
                resolvedSourcePath,
                default,
                1.0f,
                null,
                PicoLibrary.oLibrary());

            try
            {
                using var voxels = new PicoVoxels(in sourceMesh);
                using var diagnosticMesh = voxels.mshAsMesh();

                diagnosticMesh.SaveToStlFile(
                    resolvedOutputPath,
                    default,
                    null,
                    1.0f);

                var outputInfo = new FileInfo(resolvedOutputPath);

                return new PicoDiagnosticMeshExportResult(
                    resolvedSourcePath,
                    resolvedOutputPath,
                    outputInfo.Length,
                    diagnosticMesh.nTriangleCount(),
                    diagnosticMesh.nVertexCount());
            }
            finally
            {
                sourceMesh.Dispose();
            }
        }, voxelSizeMm);
    }
}