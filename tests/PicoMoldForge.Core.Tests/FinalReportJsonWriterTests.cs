using System.Text.Json;
using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.DfAM;
using PicoMoldForge.Core.Exports;
using PicoMoldForge.Core.Engineering.DraftAnalysis;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Parting;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class FinalReportJsonWriterTests
{
    [Fact]
    public void Serialize_WithValidReport_ReturnsIndentedJson()
    {
        var writer = new FinalReportJsonWriter();
        var report = CreateFinalReport();

        var json = writer.Serialize(report);

        Assert.Contains("\"ProjectName\": \"PicoMoldForge JSON Test\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Kind\": \"DiagnosticMesh\"", json, StringComparison.Ordinal);
        Assert.Contains("\"IsPassing\": true", json, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_WithValidReport_CanBeParsed()
    {
        var writer = new FinalReportJsonWriter();
        var report = CreateFinalReport();

        var json = writer.Serialize(report);

        using var document = JsonDocument.Parse(json);

        Assert.Equal("PicoMoldForge JSON Test", document.RootElement.GetProperty("ProjectName").GetString());
        Assert.True(document.RootElement.GetProperty("Baseline").GetProperty("IsPassing").GetBoolean());
        Assert.Equal(91, document.RootElement.GetProperty("Baseline").GetProperty("TotalTests").GetInt32());
    }

    [Fact]
    public void WriteToFile_WithValidReport_WritesFinalProjectReportJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-final-report-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var outputPath = Path.Combine(tempDir, "FinalProjectReport.json");

        try
        {
            var writer = new FinalReportJsonWriter();
            var report = CreateFinalReport();

            writer.WriteToFile(report, outputPath);

            Assert.True(File.Exists(outputPath));

            var json = File.ReadAllText(outputPath);

            Assert.Contains("\"ProjectName\": \"PicoMoldForge JSON Test\"", json, StringComparison.Ordinal);
            Assert.True(new FileInfo(outputPath).Length > 0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WriteToFile_WithMissingOutputPath_Throws()
    {
        var writer = new FinalReportJsonWriter();

        var exception = Assert.Throws<ArgumentException>(() =>
            writer.WriteToFile(CreateFinalReport(), string.Empty));

        Assert.Contains("Output path is required", exception.Message);
    }

    [Fact]
    public void Serialize_WithInvalidReport_Throws()
    {
        var writer = new FinalReportJsonWriter();

        var invalidReport = CreateFinalReport() with
        {
            ProjectName = string.Empty
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            writer.Serialize(invalidReport));

        Assert.Contains("Invalid final project report", exception.Message);
    }

    private static FinalProjectReport CreateFinalReport()
    {
        var builder = new FinalReportBuilder();

        return builder.Build(
            projectName: "PicoMoldForge JSON Test",
            manifest: builder.CreateStandardManifest("output"),
            partAnalysis: CreatePartAnalysisReport(),
            dfam: CreateDfAMReport(),
            baseline: new BaselineStatus(
                IsPassing: true,
                TotalTests: 91,
                Summary: "Baseline passed."),
            generatedAtUtc: new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero),
            functionalMoldAlpha: CreateFunctionalMoldAlphaReport());
    }

    private static FunctionalMoldAlphaReport CreateFunctionalMoldAlphaReport()
    {
        return new FunctionalMoldAlphaReport(
            Separation: new CoreCavitySeparationSummary(
                TotalHalfVoxelCount: 2000,
                OverlapRatio: 0.001m,
                GapRatio: 0.002m,
                BalanceRatio: 0.98m,
                QualityScore: 0.96m),
            Shutoff: new ShutoffStrategySummary(
                RegionCount: 1,
                UndefinedRegionCount: 0,
                CriticalRegionCount: 1,
                MaximumGapMm: 0.01m,
                MaximumOverlapMm: 0.01m,
                QualityScore: 0.90m),
            DraftGeometry: new DraftBasicGeometryAnalysisSummary(
                FaceCount: 10,
                PositiveDraftCount: 8,
                LowDraftCount: 1,
                ZeroDraftCount: 1,
                NegativeDraftCount: 0,
                InvalidNormalCount: 0,
                RiskySurfaceAreaMm2: 12m,
                MinimumObservedDraftDeg: 0.5m),
            Warnings: new[] { "Functional Mold Alpha metrics are preliminary." });
    }
    private static PartAnalysisReport CreatePartAnalysisReport()
    {
        var partingPlane = new PartingPlaneResult(
            PartingPlaneMode.Automatic,
            PicoMoldForge.Core.Parting.PartingAxis.X,
            new OpeningDirection3(1.0f, 0.0f, 0.0f),
            PlaneOffsetMm: 50.0f,
            Method: "Dominant bounding-box axis with center-plane placement.",
            Warnings: new[] { "Preliminary parting plane." });

        return new PartAnalysisReport(
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
                    "Preliminary analysis only.")
            },
            PartingPlane: partingPlane);
    }

    private static DfAMReport CreateDfAMReport()
    {
        return new DfAMReport(
            IsSuccessful: true,
            Checks: new[]
            {
                new DfAMCheckResult(
                    new DfAMRule(
                        "NON_CERTIFICATION_NOTICE",
                        DfAMRuleSeverity.Info,
                        "Not certification."),
                    IsPassed: true,
                    Message: "Informational only.")
            },
            Warnings: new[] { "DfAM report is preliminary." });
    }
}