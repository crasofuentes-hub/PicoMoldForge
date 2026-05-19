using System.Numerics;
using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.Engineering.DraftAnalysis;
using PicoMoldForge.PicoGK.Runtime;
using PicoLibrary = global::PicoGK.Library;
using PicoMesh = global::PicoGK.Mesh;

namespace PicoMoldForge.PicoGK.Analysis;

public sealed class PicoDraftGeometryAnalyzer
{
    public DraftBasicGeometryAnalysisResult AnalyzeBinaryStl(
        string stlPath,
        OpeningDirection3 openingDirection,
        decimal minimumRequiredDraftDeg = 1.0m,
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

        if (minimumRequiredDraftDeg < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRequiredDraftDeg),
                "Minimum required draft angle cannot be negative.");
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
                var samples = new List<DraftFaceSample>();
                var totalTriangles = mesh.nTriangleCount();

                for (var index = 0; index < totalTriangles; index++)
                {
                    mesh.GetTriangle(index, out var a, out var b, out var c);

                    var edge1 = b - a;
                    var edge2 = c - a;
                    var rawNormal = Vector3.Cross(edge1, edge2);
                    var areaMm2 = rawNormal.Length() / 2.0f;

                    if (rawNormal.LengthSquared() <= 0.000001f)
                    {
                        samples.Add(new DraftFaceSample(
                            FaceId: $"triangle-{index}",
                            NormalX: 0m,
                            NormalY: 0m,
                            NormalZ: 0m,
                            SurfaceAreaMm2: 0m));

                        continue;
                    }

                    var normal = Vector3.Normalize(rawNormal);

                    samples.Add(new DraftFaceSample(
                        FaceId: $"triangle-{index}",
                        NormalX: Convert.ToDecimal(normal.X),
                        NormalY: Convert.ToDecimal(normal.Y),
                        NormalZ: Convert.ToDecimal(normal.Z),
                        SurfaceAreaMm2: Convert.ToDecimal(areaMm2)));
                }

                var analyzer = new DraftBasicGeometryAnalyzer();

                return analyzer.Analyze(new DraftBasicGeometryAnalysisInput(
                    PullDirectionX: Convert.ToDecimal(openingDirection.X),
                    PullDirectionY: Convert.ToDecimal(openingDirection.Y),
                    PullDirectionZ: Convert.ToDecimal(openingDirection.Z),
                    MinimumRequiredDraftDeg: minimumRequiredDraftDeg,
                    Faces: samples));
            }
            finally
            {
                mesh.Dispose();
            }
        }, voxelSizeMm);
    }
}