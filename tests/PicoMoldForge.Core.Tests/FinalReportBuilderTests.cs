using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.DfAM;
using PicoMoldForge.Core.Exports;
using PicoMoldForge.Core.Parting;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class FinalReportBuilderTests
{
    [Fact]
    public void CreateStandardManifest_ReturnsExpectedArtifacts()
    {
        var builder = new FinalReportBuilder();

        var manifest = builder.CreateStandardManifest("output");

        Assert.Equal(6, manifest.Artifacts.Count);
        Assert.Contains(manifest.Artifacts, artifact => artifact.Kind == ExportArtifactKind.DiagnosticMesh && artifact.Path.EndsWith("DiagnosticMesh.stl", StringComparison.Ordinal));
        Assert.Contains(manifest.Artifacts, artifact => artifact.Kind == ExportArtifactKind.Cavity && artifact.Path.EndsWith("Cavity.stl", StringComparison.Ordinal));
        Assert.Contains(manifest.Artifacts, artifact => artifact.Kind == ExportArtifactKind.Core && artifact.Path.EndsWith("Core.stl", StringComparison.Ordinal));
        Assert.Contains(manifest.Artifacts, artifact => artifact.Kind == ExportArtifactKind.CoolingDiagnostic && artifact.Path.EndsWith("CoolingDiagnostic.stl", StringComparison.Ordinal));
        Assert.Contains(manifest.Artifacts, artifact => artifact.Kind == ExportArtifactKind.LatticeDiagnostic && artifact.Path.EndsWith("LatticeDiagnostic.stl", StringComparison.Ordinal));
        Assert.Contains(manifest.Artifacts, artifact => artifact.Kind == ExportArtifactKind.MoldSystemDiagnostic && artifact.Path.EndsWith("MoldSystemDiagnostic.stl", StringComparison.Ordinal));
        Assert.Empty(manifest.Validate());
    }

    [Fact]
    public void Build_WithCompleteInputs_ReturnsFinalProjectReport()
    {
        var builder = new FinalReportBuilder();

        var manifest = builder.CreateStandardManifest("output");
        var partAnalysis = CreatePartAnalysisReport();
        var dfam = CreateDfAMReport(isSuccessful: true);
        var baseline = new BaselineStatus(
            IsPassing: true,
            TotalTests: 82,
            Summary: "Baseline passed.");

        var generatedAtUtc = new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero);

        var report = builder.Build(
            "PicoMoldForge Test Project",
            manifest,
            partAnalysis,
            dfam,
            baseline,
            generatedAtUtc);

        Assert.Equal("PicoMoldForge Test Project", report.ProjectName);
        Assert.Equal(generatedAtUtc, report.GeneratedAtUtc);
        Assert.Same(manifest, report.Manifest);
        Assert.Same(partAnalysis, report.PartAnalysis);
        Assert.Same(dfam, report.DfAM);
        Assert.True(report.Baseline.IsPassing);
        Assert.Contains(report.Warnings, warning => warning.Contains("does not certify", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(report.Validate());
    }

    [Fact]
    public void Build_WithMissingPartAnalysis_AddsWarning()
    {
        var builder = new FinalReportBuilder();

        var report = builder.Build(
            "PicoMoldForge Test Project",
            builder.CreateStandardManifest("output"),
            partAnalysis: null,
            dfam: CreateDfAMReport(isSuccessful: true),
            baseline: new BaselineStatus(true, 82, "Baseline passed."),
            generatedAtUtc: new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero));

        Assert.Contains(report.Warnings, warning => warning.Contains("PartAnalysisReport was not provided", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WithMissingDfAM_AddsWarning()
    {
        var builder = new FinalReportBuilder();

        var report = builder.Build(
            "PicoMoldForge Test Project",
            builder.CreateStandardManifest("output"),
            partAnalysis: CreatePartAnalysisReport(),
            dfam: null,
            baseline: new BaselineStatus(true, 82, "Baseline passed."),
            generatedAtUtc: new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero));

        Assert.Contains(report.Warnings, warning => warning.Contains("DfAMReport was not provided", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WithFailingBaseline_AddsReleaseWarning()
    {
        var builder = new FinalReportBuilder();

        var report = builder.Build(
            "PicoMoldForge Test Project",
            builder.CreateStandardManifest("output"),
            CreatePartAnalysisReport(),
            CreateDfAMReport(isSuccessful: true),
            new BaselineStatus(false, 82, "Baseline failed."),
            new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero));

        Assert.Contains(report.Warnings, warning => warning.Contains("Baseline is not passing", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WithInvalidProjectName_Throws()
    {
        var builder = new FinalReportBuilder();

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Build(
                string.Empty,
                builder.CreateStandardManifest("output"),
                CreatePartAnalysisReport(),
                CreateDfAMReport(isSuccessful: true),
                new BaselineStatus(true, 82, "Baseline passed."),
                new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero)));

        Assert.Contains("ProjectName is required", exception.Message);
    }

    [Fact]
    public void Build_WithNonUtcTimestamp_Throws()
    {
        var builder = new FinalReportBuilder();

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Build(
                "PicoMoldForge Test Project",
                builder.CreateStandardManifest("output"),
                CreatePartAnalysisReport(),
                CreateDfAMReport(isSuccessful: true),
                new BaselineStatus(true, 82, "Baseline passed."),
                new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.FromHours(-7))));

        Assert.Contains("GeneratedAtUtc must use UTC offset", exception.Message);
    }

    [Fact]
    public void ExportArtifact_WithMissingPath_ReturnsValidationError()
    {
        var artifact = new ExportArtifact(
            ExportArtifactKind.Cavity,
            Path: string.Empty,
            Description: "Cavity export.",
            IsRequired: true);

        var errors = artifact.Validate();

        Assert.Contains(errors, error => error.Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportManifest_WithNoArtifacts_ReturnsValidationError()
    {
        var manifest = new ExportManifest(
            Artifacts: Array.Empty<ExportArtifact>(),
            Warnings: Array.Empty<string>());

        var errors = manifest.Validate();

        Assert.Contains(errors, error => error.Contains("at least one artifact", StringComparison.OrdinalIgnoreCase));
    }

    private static PartAnalysisReport CreatePartAnalysisReport()
    {
        var partingPlane = new PartingPlaneResult(
            PartingPlaneMode.Automatic,
            PartingAxis.X,
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

    private static DfAMReport CreateDfAMReport(bool isSuccessful)
    {
        return new DfAMReport(
            IsSuccessful: isSuccessful,
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