using PicoMoldForge.Core.MoldSystems;
using PicoMoldForge.PicoGK.MoldSystems;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoMoldSystemDiagnosticExporterTests
{
    [Fact]
    public void ExportMoldSystemDiagnostic_WithPreliminaryPlan_WritesDiagnosticStl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-mold-system-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var outputPath = Path.Combine(tempDir, "MoldSystemDiagnostic.stl");

        try
        {
            var planner = new PreliminaryMoldSystemPlanner();
            var request = new MoldSystemRequest(
                OutputDirectory: tempDir,
                PartSizeXmm: 100,
                PartSizeYmm: 60,
                PartSizeZmm: 30,
                MoldMarginMm: 20,
                EjectorPinDiameterMm: 4,
                EjectorPinCount: 4,
                VentWidthMm: 0.5m,
                VentDepthMm: 0.1m,
                InsertClearanceMm: 2);

            var plan = planner.Plan(request);
            var exporter = new PicoMoldSystemDiagnosticExporter();

            var result = exporter.ExportMoldSystemDiagnostic(
                plan,
                outputPath,
                voxelSizeMm: 1.0f);

            Assert.Equal(Path.GetFullPath(outputPath), result.OutputPath);
            Assert.True(File.Exists(outputPath));
            Assert.True(result.OutputFileSizeBytes > 84);
            Assert.Equal(4, result.EjectorPinCount);
            Assert.Equal(2, result.VentChannelCount);
            Assert.Equal(1, result.InsertPocketCount);
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
    public void ExportMoldSystemDiagnostic_WithUnsuccessfulPlan_Throws()
    {
        var exporter = new PicoMoldSystemDiagnosticExporter();

        var plan = new MoldSystemPlan(
            IsSuccessful: false,
            MoldBase: new MoldBaseEnvelope(1, 1, 1, 1),
            Ejectors: new EjectorPinPlan(true, Array.Empty<EjectorPin>(), Array.Empty<string>()),
            Vents: new VentPlan(true, Array.Empty<VentChannel>(), Array.Empty<string>()),
            Inserts: new InsertPlan(true, Array.Empty<InsertPocket>(), Array.Empty<string>()),
            Warnings: Array.Empty<string>());

        var exception = Assert.Throws<ArgumentException>(() =>
            exporter.ExportMoldSystemDiagnostic(plan, "MoldSystemDiagnostic.stl"));

        Assert.Contains("must be successful", exception.Message);
    }

    [Fact]
    public void ExportMoldSystemDiagnostic_WithMissingOutputPath_Throws()
    {
        var planner = new PreliminaryMoldSystemPlanner();
        var plan = planner.Plan(new MoldSystemRequest(
            OutputDirectory: "output",
            PartSizeXmm: 100,
            PartSizeYmm: 60,
            PartSizeZmm: 30,
            MoldMarginMm: 20,
            EjectorPinDiameterMm: 4,
            EjectorPinCount: 4,
            VentWidthMm: 0.5m,
            VentDepthMm: 0.1m,
            InsertClearanceMm: 2));

        var exporter = new PicoMoldSystemDiagnosticExporter();

        var exception = Assert.Throws<ArgumentException>(() =>
            exporter.ExportMoldSystemDiagnostic(plan, string.Empty));

        Assert.Contains("Output STL path is required", exception.Message);
    }
}