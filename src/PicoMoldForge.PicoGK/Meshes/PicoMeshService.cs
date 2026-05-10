using PicoGK;
using PicoMoldForge.PicoGK.Runtime;

namespace PicoMoldForge.PicoGK.Meshes;

public sealed class PicoMeshService
{
    public PicoMeshMetrics LoadStlMetrics(string stlPath)
    {
        if (string.IsNullOrWhiteSpace(stlPath))
        {
            throw new ArgumentException("STL path is required.", nameof(stlPath));
        }

        var resolvedPath = Path.GetFullPath(stlPath);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("STL file was not found.", resolvedPath);
        }

        return PicoGkTaskRunner.Run(() =>
        {
            using var mesh = Mesh.mshFromStlFile(
                resolvedPath,
                default,
                1.0f,
                null,
                Library.oLibrary());

            return new PicoMeshMetrics(
                resolvedPath,
                mesh.nTriangleCount(),
                mesh.nVertexCount(),
                mesh.oBoundingBox().ToString() ?? string.Empty);
        });
    }
}