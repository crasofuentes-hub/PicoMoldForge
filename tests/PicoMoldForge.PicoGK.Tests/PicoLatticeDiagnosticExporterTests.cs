using PicoMoldForge.Core.Lattice;
using PicoMoldForge.PicoGK.Lattice;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoLatticeDiagnosticExporterTests
{
    [Fact]
    public void ExportLatticeDiagnostic_WithSimpleGridPlan_WritesDiagnosticStl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-lattice-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var outputPath = Path.Combine(tempDir, "LatticeDiagnostic.stl");

        try
        {
            var planner = new LatticePlanner();
            var request = new LatticeRegionRequest(
                RegionName: "test-lattice-region",
                OutputDirectory: tempDir,
                MinXmm: 0,
                MinYmm: 0,
                MinZmm: 0,
                MaxXmm: 20,
                MaxYmm: 10,
                MaxZmm: 10,
                CellSizeMm: 10,
                BeamRadiusMm: 1,
                TargetRelativeDensity: 0.2m);

            var plan = planner.PlanSimpleGrid(request);
            var exporter = new PicoLatticeDiagnosticExporter();

            var result = exporter.ExportLatticeDiagnostic(
                plan,
                outputPath,
                voxelSizeMm: 1.0f);

            Assert.Equal(Path.GetFullPath(outputPath), result.OutputPath);
            Assert.Equal("test-lattice-region", result.RegionName);
            Assert.Equal(plan.Beams.Count, result.BeamCount);
            Assert.True(File.Exists(outputPath));
            Assert.True(result.OutputFileSizeBytes > 84);
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
    public void ExportLatticeDiagnostic_WithEmptyPlan_Throws()
    {
        var exporter = new PicoLatticeDiagnosticExporter();

        var plan = new LatticeCellPlan(
            IsSuccessful: true,
            RegionName: "empty-region",
            XNodeCount: 0,
            YNodeCount: 0,
            ZNodeCount: 0,
            Beams: Array.Empty<LatticeBeamSegment>(),
            Warnings: Array.Empty<string>());

        var exception = Assert.Throws<ArgumentException>(() =>
            exporter.ExportLatticeDiagnostic(plan, "LatticeDiagnostic.stl"));

        Assert.Contains("at least one beam", exception.Message);
    }

    [Fact]
    public void ExportLatticeDiagnostic_WithUnsuccessfulPlan_Throws()
    {
        var exporter = new PicoLatticeDiagnosticExporter();

        var beam = new LatticeBeamSegment(
            Id: "lattice-beam-000001",
            Axis: LatticeBeamAxis.X,
            StartXmm: 0,
            StartYmm: 0,
            StartZmm: 0,
            EndXmm: 10,
            EndYmm: 0,
            EndZmm: 0,
            BeamRadiusMm: 1);

        var plan = new LatticeCellPlan(
            IsSuccessful: false,
            RegionName: "failed-region",
            XNodeCount: 2,
            YNodeCount: 1,
            ZNodeCount: 1,
            Beams: new[] { beam },
            Warnings: Array.Empty<string>());

        var exception = Assert.Throws<ArgumentException>(() =>
            exporter.ExportLatticeDiagnostic(plan, "LatticeDiagnostic.stl"));

        Assert.Contains("must be successful", exception.Message);
    }
}