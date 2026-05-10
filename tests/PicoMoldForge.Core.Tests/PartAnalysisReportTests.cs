using PicoMoldForge.Core.Analysis;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class PartAnalysisReportTests
{
    [Fact]
    public void PartAnalysisReport_CanRepresentPreliminaryMetrics()
    {
        var report = new PartAnalysisReport(
            SourcePath: "part.stl",
            TriangleCount: 12,
            VertexCount: 8,
            MeshBoundingBox: "mesh bbox",
            VoxelSizeMm: 1.0f,
            VoxelizedVolumeCubicMm: 1000.0f,
            VoxelSliceCount: 10,
            VoxelMemoryUsageBytes: 1024,
            VoxelBoundingBox: "voxel bbox",
            Warnings: new[]
            {
                new PartAnalysisWarning(
                    "PRELIMINARY_ANALYSIS",
                    "Info",
                    "Preliminary metrics only.")
            });

        Assert.Equal("part.stl", report.SourcePath);
        Assert.Equal(12, report.TriangleCount);
        Assert.Single(report.Warnings);
    }
}