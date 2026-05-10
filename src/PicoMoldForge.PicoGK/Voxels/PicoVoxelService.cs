using PicoGK;
using PicoMoldForge.PicoGK.Runtime;
using PicoVoxels = PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.Voxels;

public sealed class PicoVoxelService
{
    public PicoVoxelMetrics LoadStlAsVoxelMetrics(string stlPath, float voxelSizeMm = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(stlPath))
        {
            throw new ArgumentException("STL path is required.", nameof(stlPath));
        }

        if (voxelSizeMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelSizeMm), "Voxel size must be greater than zero.");
        }

        var resolvedPath = Path.GetFullPath(stlPath);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("STL file was not found.", resolvedPath);
        }

        return PicoGkTaskRunner.Run(() =>
        {
            var mesh = Mesh.mshFromStlFile(
                resolvedPath,
                default,
                1.0f,
                null,
                Library.oLibrary());

            try
            {
                using var voxels = new PicoVoxels(in mesh);

                voxels.CalculateProperties(
                    out var volumeCubicMm,
                    out var boundingBox);

                return new PicoVoxelMetrics(
                    resolvedPath,
                    voxelSizeMm,
                    volumeCubicMm,
                    voxels.nSliceCount(),
                    voxels.nMemUsage(),
                    boundingBox.ToString() ?? string.Empty);
            }
            finally
            {
                mesh.Dispose();
            }
        }, voxelSizeMm);
    }
}