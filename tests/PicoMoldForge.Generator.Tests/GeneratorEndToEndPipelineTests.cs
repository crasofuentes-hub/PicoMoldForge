using System.Text.Json;
using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class GeneratorEndToEndPipelineTests
{
    [Fact]
    public void Run_WithGenerateAllAndValidBinaryStlConfig_GeneratesFullOutputPackage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-generator-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "part-binary.stl");
        var configPath = Path.Combine(tempDir, "project.json");
        var outputDirectory = Path.Combine(tempDir, "output");

        WriteBinaryBoxStl(stlPath, sizeX: 20.0f, sizeY: 10.0f, sizeZ: 5.0f);
        WriteProjectConfig(configPath, "part-binary.stl", "output");

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = GeneratorCommandLineApplication.Run(
                new[] { "--config", configPath, "--generate-all" },
                output,
                error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("Generation pipeline: PASS", output.ToString());
            Assert.Contains("Artifacts generated: 10", output.ToString());

            Assert.True(File.Exists(Path.Combine(outputDirectory, "DiagnosticMesh.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Cavity.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "BooleanCavity.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "BooleanCoreSide.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "BooleanCavitySide.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Core.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "CoolingDiagnostic.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "LatticeDiagnostic.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "MoldSystemDiagnostic.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "GateRunnerSprueDiagnostic.stl")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "FinalProjectReport.json")));

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "FinalProjectReport.json")));

            Assert.Equal("Generator E2E Test", document.RootElement.GetProperty("ProjectName").GetString());
            Assert.True(document.RootElement.GetProperty("Baseline").GetProperty("IsPassing").GetBoolean());

            var alpha = document.RootElement.GetProperty("FunctionalMoldAlpha");

            Assert.True(alpha.GetProperty("IsAlphaComplete").GetBoolean());
            Assert.True(alpha.GetProperty("OverallReadinessScore").GetDecimal() >= 0m);
            Assert.True(alpha.GetProperty("Separation").GetProperty("QualityScore").GetDecimal() >= 0m);
            var wallThickness = alpha.GetProperty("WallThickness");

            Assert.Equal(3, wallThickness.GetProperty("SampleCount").GetInt32());
            Assert.True(wallThickness.GetProperty("MinimumObservedThicknessMm").GetDecimal() > 0m);
            Assert.True(wallThickness.GetProperty("MaximumObservedThicknessMm").GetDecimal() > 0m);
            Assert.True(wallThickness.GetProperty("RiskySurfaceAreaMm2").GetDecimal() >= 0m);
            var draftGeometry = alpha.GetProperty("DraftGeometry");

            Assert.Equal(12, draftGeometry.GetProperty("FaceCount").GetInt32());
            Assert.True(draftGeometry.GetProperty("RiskySurfaceAreaMm2").GetDecimal() >= 0m);
            Assert.True(draftGeometry.GetProperty("MinimumObservedDraftDeg").GetDecimal() >= 0m);
            var undercutRisk = alpha.GetProperty("UndercutRisk");

            Assert.Equal(12, undercutRisk.GetProperty("FaceCount").GetInt32());
            Assert.True(undercutRisk.GetProperty("RiskySurfaceAreaMm2").GetDecimal() >= 0m);
            Assert.True(undercutRisk.GetProperty("MaximumTrapDepthMm").GetDecimal() >= 0m);
            Assert.True(
                undercutRisk.GetProperty("ClearPullCount").GetInt32() +
                undercutRisk.GetProperty("LowPullClearanceCount").GetInt32() +
                undercutRisk.GetProperty("SideActionCandidateCount").GetInt32() +
                undercutRisk.GetProperty("UndercutCount").GetInt32() +
                undercutRisk.GetProperty("InvalidNormalCount").GetInt32() ==
                undercutRisk.GetProperty("FaceCount").GetInt32());
            var partingPlane = alpha.GetProperty("PartingPlane");

            Assert.True(partingPlane.GetProperty("QualityScore").GetDecimal() >= 0m);
            Assert.True(partingPlane.GetProperty("NormalizedPosition").GetDecimal() >= 0m);
            Assert.True(partingPlane.TryGetProperty("Candidate", out var selectedCandidate));
            Assert.False(string.IsNullOrWhiteSpace(selectedCandidate.GetProperty("Source").GetString()));
            var coolingChannels = alpha.GetProperty("CoolingChannels");

            Assert.True(coolingChannels.GetProperty("ChannelCount").GetInt32() > 0);
            Assert.True(coolingChannels.GetProperty("SubtractableChannelCount").GetInt32() > 0);
            Assert.Equal(0, coolingChannels.GetProperty("BlockedChannelCount").GetInt32());
            Assert.True(coolingChannels.GetProperty("TotalEstimatedRemovedVolumeMm3").GetDecimal() > 0m);
            Assert.True(alpha.GetProperty("EjectorCandidates").GetProperty("CandidateCount").GetInt32() > 0);
            var gateRunnerSprue = alpha.GetProperty("GateRunnerSprue");

            Assert.Equal(3, gateRunnerSprue.GetProperty("SegmentCount").GetInt32());
            Assert.Equal(1, gateRunnerSprue.GetProperty("SprueCount").GetInt32());
            Assert.Equal(1, gateRunnerSprue.GetProperty("RunnerCount").GetInt32());
            Assert.Equal(1, gateRunnerSprue.GetProperty("GateCount").GetInt32());
            Assert.Equal(3, gateRunnerSprue.GetProperty("GeneratableSegmentCount").GetInt32());
            Assert.Equal(0, gateRunnerSprue.GetProperty("BlockedSegmentCount").GetInt32());
            Assert.True(gateRunnerSprue.GetProperty("TotalFlowLengthMm").GetDecimal() > 0m);
            Assert.True(gateRunnerSprue.GetProperty("TotalEstimatedVolumeMm3").GetDecimal() > 0m);
            var gateRunnerSprue = alpha.GetProperty("GateRunnerSprue");

            Assert.Equal(3, gateRunnerSprue.GetProperty("SegmentCount").GetInt32());
            Assert.Equal(1, gateRunnerSprue.GetProperty("SprueCount").GetInt32());
            Assert.Equal(1, gateRunnerSprue.GetProperty("RunnerCount").GetInt32());
            Assert.Equal(1, gateRunnerSprue.GetProperty("GateCount").GetInt32());
            Assert.Equal(3, gateRunnerSprue.GetProperty("GeneratableSegmentCount").GetInt32());
            Assert.Equal(0, gateRunnerSprue.GetProperty("BlockedSegmentCount").GetInt32());
            Assert.True(gateRunnerSprue.GetProperty("TotalFlowLengthMm").GetDecimal() > 0m);
            Assert.True(gateRunnerSprue.GetProperty("TotalEstimatedVolumeMm3").GetDecimal() > 0m);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteProjectConfig(string path, string inputPath, string outputDirectory)
    {
        File.WriteAllText(path, $$"""
        {
          "projectName": "Generator E2E Test",
          "inputPath": "{{inputPath}}",
          "outputDirectory": "{{outputDirectory}}",
          "mode": "Prototype",
          "standard": "Custom",
          "voxelResolutionMm": 1.0,
          "material": {
            "name": "H13 Tool Steel",
            "shrinkageRate": 0.011
          },
          "machine": {
            "name": "Generic LPBF Machine",
            "buildVolumeXmm": 250,
            "buildVolumeYmm": 250,
            "buildVolumeZmm": 300
          },
          "moldBlock": {
            "minXmm": -25,
            "minYmm": -25,
            "minZmm": -25,
            "maxXmm": 125,
            "maxYmm": 85,
            "maxZmm": 55
          },
          "cooling": {
            "partSizeXmm": 100,
            "partSizeYmm": 60,
            "partSizeZmm": 30,
            "channelDiameterMm": 6,
            "channelSpacingMm": 15,
            "minimumClearanceMm": 10,
            "channelCount": 3
          },
          "lattice": {
            "regionName": "default-lattice-region",
            "minXmm": 0,
            "minYmm": 0,
            "minZmm": 0,
            "maxXmm": 20,
            "maxYmm": 10,
            "maxZmm": 10,
            "cellSizeMm": 10,
            "beamRadiusMm": 1,
            "targetRelativeDensity": 0.2
          },
          "moldSystem": {
            "partSizeXmm": 100,
            "partSizeYmm": 60,
            "partSizeZmm": 30,
            "moldMarginMm": 20,
            "ejectorPinDiameterMm": 4,
            "ejectorPinCount": 4,
            "ventWidthMm": 0.5,
            "ventDepthMm": 0.1,
            "insertClearanceMm": 2
          },
          "dfam": {
            "minimumWallThicknessMm": 1.5,
            "recommendedMinimumWallThicknessMm": 1.2,
            "usesPreliminaryGeometry": true
          }
        }
        """);
    }

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge generator E2E binary box STL test"u8.ToArray();
        Array.Copy(headerText, header, headerText.Length);
        writer.Write(header);

        writer.Write((uint)12);

        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, sizeX, sizeY, 0, sizeX, 0, 0);
        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, 0, sizeY, 0, sizeX, sizeY, 0);

        WriteTriangle(writer, 0, 0, 1, 0, 0, sizeZ, sizeX, 0, sizeZ, sizeX, sizeY, sizeZ);
        WriteTriangle(writer, 0, 0, 1, 0, 0, sizeZ, sizeX, sizeY, sizeZ, 0, sizeY, sizeZ);

        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, sizeX, 0, 0, sizeX, 0, sizeZ);
        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, sizeX, 0, sizeZ, 0, 0, sizeZ);

        WriteTriangle(writer, 0, 1, 0, 0, sizeY, 0, sizeX, sizeY, sizeZ, sizeX, sizeY, 0);
        WriteTriangle(writer, 0, 1, 0, 0, sizeY, 0, 0, sizeY, sizeZ, sizeX, sizeY, sizeZ);

        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, 0, sizeZ, 0, sizeY, sizeZ);
        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, sizeY, sizeZ, 0, sizeY, 0);

        WriteTriangle(writer, 1, 0, 0, sizeX, 0, 0, sizeX, sizeY, 0, sizeX, sizeY, sizeZ);
        WriteTriangle(writer, 1, 0, 0, sizeX, 0, 0, sizeX, sizeY, sizeZ, sizeX, 0, sizeZ);
    }

    private static void WriteTriangle(
        BinaryWriter writer,
        float nx,
        float ny,
        float nz,
        float ax,
        float ay,
        float az,
        float bx,
        float by,
        float bz,
        float cx,
        float cy,
        float cz)
    {
        writer.Write(nx);
        writer.Write(ny);
        writer.Write(nz);

        writer.Write(ax);
        writer.Write(ay);
        writer.Write(az);

        writer.Write(bx);
        writer.Write(by);
        writer.Write(bz);

        writer.Write(cx);
        writer.Write(cy);
        writer.Write(cz);

        writer.Write((ushort)0);
    }
}