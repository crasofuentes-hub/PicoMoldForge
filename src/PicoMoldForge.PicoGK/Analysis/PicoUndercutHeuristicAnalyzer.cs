using System.Numerics;
using PicoMoldForge.Core.Analysis;
using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoMesh = global::PicoGK.Mesh;

namespace PicoMoldForge.PicoGK.Analysis;

public sealed class PicoUndercutHeuristicAnalyzer
{
    private const float OpposingDotThreshold = -0.15f;

    public UndercutHeuristicResult AnalyzeBinaryStl(
        string stlPath,
        OpeningDirection3 openingDirection,
        float voxelSizeMm = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(stlPath))
        {
            throw new ArgumentException("STL path is required.", nameof(stlPath));
        }

        if (openingDirection.IsZero)
        {
            throw new ArgumentException("Opening direction cannot be zero.", nameof(openingDirection));
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
                var opening = Vector3.Normalize(new Vector3(
                    openingDirection.X,
                    openingDirection.Y,
                    openingDirection.Z));

                var totalTriangles = mesh.nTriangleCount();
                var opposingTriangles = 0;

                for (var index = 0; index < totalTriangles; index++)
                {
                    mesh.GetTriangle(index, out var a, out var b, out var c);

                    var edge1 = b - a;
                    var edge2 = c - a;
                    var normal = Vector3.Cross(edge1, edge2);

                    if (normal.LengthSquared() <= 0.000001f)
                    {
                        continue;
                    }

                    normal = Vector3.Normalize(normal);

                    var dot = Vector3.Dot(normal, opening);

                    if (dot < OpposingDotThreshold)
                    {
                        opposingTriangles++;
                    }
                }

                var ratio = totalTriangles == 0
                    ? 0.0f
                    : (float)opposingTriangles / totalTriangles;

                return new UndercutHeuristicResult(
                    openingDirection,
                    totalTriangles,
                    opposingTriangles,
                    ratio,
                    opposingTriangles > 0,
                    "Triangle normal opposition heuristic",
                    "This is a preliminary deterministic risk heuristic. It does not prove true mold undercuts and does not perform accessibility, shadow, draft, or toolpath analysis.");
            }
            finally
            {
                mesh.Dispose();
            }
        }, voxelSizeMm);
    }
}