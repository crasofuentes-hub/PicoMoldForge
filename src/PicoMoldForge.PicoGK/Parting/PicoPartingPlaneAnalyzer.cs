using PicoMoldForge.Core.Parting;
using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoMesh = global::PicoGK.Mesh;

namespace PicoMoldForge.PicoGK.Parting;

public sealed class PicoPartingPlaneAnalyzer
{
    private readonly PartingPlaneEngine engine;

    public PicoPartingPlaneAnalyzer()
        : this(new PartingPlaneEngine())
    {
    }

    public PicoPartingPlaneAnalyzer(PartingPlaneEngine engine)
    {
        this.engine = engine;
    }

    public PartingPlaneResult AnalyzeBinaryStl(string stlPath, float voxelSizeMm = 1.0f)
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
            var mesh = PicoMesh.mshFromStlFile(
                resolvedPath,
                default,
                1.0f,
                null,
                PicoLibrary.oLibrary());

            try
            {
                var vertexCount = mesh.nVertexCount();

                if (vertexCount <= 0)
                {
                    throw new InvalidOperationException("Mesh contains no vertices.");
                }

                var first = mesh.vecVertexAt(0);

                var minX = first.X;
                var minY = first.Y;
                var minZ = first.Z;
                var maxX = first.X;
                var maxY = first.Y;
                var maxZ = first.Z;

                for (var index = 1; index < vertexCount; index++)
                {
                    var vertex = mesh.vecVertexAt(index);

                    minX = Math.Min(minX, vertex.X);
                    minY = Math.Min(minY, vertex.Y);
                    minZ = Math.Min(minZ, vertex.Z);

                    maxX = Math.Max(maxX, vertex.X);
                    maxY = Math.Max(maxY, vertex.Y);
                    maxZ = Math.Max(maxZ, vertex.Z);
                }

                var boundingBox = new PartingBoundingBox(
                    minX,
                    minY,
                    minZ,
                    maxX,
                    maxY,
                    maxZ);

                return engine.CalculateAutomatic(boundingBox);
            }
            finally
            {
                mesh.Dispose();
            }
        }, voxelSizeMm);
    }
}