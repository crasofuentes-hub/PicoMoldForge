using PicoMoldForge.Core.Parting;

namespace PicoMoldForge.Core.Analysis;

public sealed record PartAnalysisReport(
    string SourcePath,
    int TriangleCount,
    int VertexCount,
    string MeshBoundingBox,
    float VoxelSizeMm,
    float VoxelizedVolumeCubicMm,
    int VoxelSliceCount,
    long VoxelMemoryUsageBytes,
    string VoxelBoundingBox,
    IReadOnlyList<PartAnalysisWarning> Warnings,
    PartingPlaneResult? PartingPlane = null);