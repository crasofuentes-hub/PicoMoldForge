using PicoMoldForge.Core.Cooling;
using PicoMoldForge.PicoGK.Cooling;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoCoolingDiagnosticExporterTests
{
    [Fact]
    public void ExportCoolingDiagnostic_WithStraightChannelPlan_WritesDiagnosticStl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-cooling-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var outputPath = Path.Combine(tempDir, "CoolingDiagnostic.stl");

        try
        {
            var planner = new CoolingPlanner();
            var request = new CoolingChannelRequest(
                OutputDirectory: tempDir,
                PartSizeXmm: 100,
                PartSizeYmm: 60,
                PartSizeZmm: 30,
                ChannelDiameterMm: 6,
                ChannelSpacingMm: 15,
                MinimumClearanceMm: 10,
                ChannelCount: 3);

            var plan = planner.PlanStraightChannels(request);
            var exporter = new PicoCoolingDiagnosticExporter();

            var result = exporter.ExportCoolingDiagnostic(
                plan,
                outputPath,
                voxelSizeMm: 1.0f);

            Assert.Equal(Path.GetFullPath(outputPath), result.OutputPath);
            Assert.True(File.Exists(outputPath));
            Assert.True(result.OutputFileSizeBytes > 84);
            Assert.Equal(3, result.SegmentCount);
            Assert.True(result.DiagnosticTriangleCount > 0);
            Assert.True(result.DiagnosticVertexCount > 0);
            Assert.Contains(result.Warnings, warning => warning.Contains("preliminary", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ExportCoolingDiagnostic_WithEmptyPlan_Throws()
    {
        var exporter = new PicoCoolingDiagnosticExporter();

        var plan = new CoolingChannelPlan(
            IsSuccessful: true,
            Segments: Array.Empty<CoolingChannelSegment>(),
            Warnings: Array.Empty<string>());

        var exception = Assert.Throws<ArgumentException>(() =>
            exporter.ExportCoolingDiagnostic(plan, "CoolingDiagnostic.stl"));

        Assert.Contains("at least one segment", exception.Message);
    }
}