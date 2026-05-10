using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.Parting;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class PartAnalysisReportPartingTests
{
    [Fact]
    public void PartAnalysisReport_CanIncludePartingPlaneResult()
    {
        var partingPlane = new PartingPlaneResult(
            PartingPlaneMode.Automatic,
            PartingAxis.X,
            new OpeningDirection3(1.0f, 0.0f, 0.0f),
            PlaneOffsetMm: 50.0f,
            Method: "Dominant bounding-box axis with center-plane placement.",
            Warnings: new[] { "Preliminary parting plane." });

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
            Warnings: Array.Empty<PartAnalysisWarning>(),
            PartingPlane: partingPlane);

        Assert.NotNull(report.PartingPlane);
        Assert.Equal(PartingAxis.X, report.PartingPlane.Axis);
        Assert.Equal(50.0f, report.PartingPlane.PlaneOffsetMm);
    }
}