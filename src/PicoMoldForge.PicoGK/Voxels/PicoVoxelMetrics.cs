namespace PicoMoldForge.PicoGK.Voxels;

public sealed record PicoVoxelMetrics(
    string SourcePath,
    float VoxelSizeMm,
    float VolumeCubicMm,
    int SliceCount,
    long MemoryUsageBytes,
    string BoundingBox);