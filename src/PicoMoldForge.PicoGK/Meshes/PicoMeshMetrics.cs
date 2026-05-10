namespace PicoMoldForge.PicoGK.Meshes;

public sealed record PicoMeshMetrics(
    string SourcePath,
    int TriangleCount,
    int VertexCount,
    string BoundingBox);