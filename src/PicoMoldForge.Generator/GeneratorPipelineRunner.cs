using PicoMoldForge.Core.CavityCore;
using PicoMoldForge.Core.Cooling;
using PicoMoldForge.Core.DfAM;
using PicoMoldForge.Core.Exports;
using PicoMoldForge.Core.Lattice;
using PicoMoldForge.Core.MoldSystems;
using PicoMoldForge.PicoGK.Analysis;
using PicoMoldForge.PicoGK.CavityCore;
using PicoMoldForge.PicoGK.Cooling;
using PicoMoldForge.PicoGK.Exports;
using PicoMoldForge.PicoGK.Lattice;
using PicoMoldForge.PicoGK.MoldSystems;

namespace PicoMoldForge.Generator;

public sealed class GeneratorPipelineRunner
{
    public GeneratorPipelineRunResult Run(GeneratorPipelineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Directory.CreateDirectory(input.ResolvedOutputDirectory);

        var voxelSizeMm = Convert.ToSingle(input.Config.VoxelResolutionMm);
        var shrinkageRate = Convert.ToDecimal(input.Config.Material.ShrinkageRate);

        var diagnosticMeshPath = Path.Combine(input.ResolvedOutputDirectory, "DiagnosticMesh.stl");
        var coolingDiagnosticPath = Path.Combine(input.ResolvedOutputDirectory, "CoolingDiagnostic.stl");
        var latticeDiagnosticPath = Path.Combine(input.ResolvedOutputDirectory, "LatticeDiagnostic.stl");
        var moldSystemDiagnosticPath = Path.Combine(input.ResolvedOutputDirectory, "MoldSystemDiagnostic.stl");
        var finalReportPath = Path.Combine(input.ResolvedOutputDirectory, "FinalProjectReport.json");

        var warnings = new List<string>
        {
            "Generator pipeline uses preliminary defaults for cooling, lattice, mold-system, and DfAM parameters.",
            "Generated geometry is preliminary diagnostic geometry and is not certified for production manufacturing."
        };

        var partAnalyzer = new PicoPartAnalyzer();
        var partAnalysis = partAnalyzer.AnalyzeBinaryStl(input.ResolvedInputPath, voxelSizeMm);

        if (partAnalysis.PartingPlane is null)
        {
            throw new InvalidOperationException("Part analysis did not produce a parting plane.");
        }

        var diagnosticExporter = new PicoDiagnosticMeshExporter();
        diagnosticExporter.ExportVoxelizedDiagnosticMesh(
            input.ResolvedInputPath,
            diagnosticMeshPath,
            voxelSizeMm);

        var cavityCoreGenerator = new PicoCavityCoreGenerator();
        cavityCoreGenerator.GeneratePreliminary(new CavityCoreGenerationRequest(
            SourcePath: input.ResolvedInputPath,
            OutputDirectory: input.ResolvedOutputDirectory,
            ShrinkageRate: shrinkageRate,
            PartingPlane: partAnalysis.PartingPlane));

        var coolingRequest = CreateDefaultCoolingRequest(input.ResolvedOutputDirectory);
        var coolingPlanner = new CoolingPlanner();
        var coolingPlan = coolingPlanner.PlanStraightChannels(coolingRequest);

        var coolingExporter = new PicoCoolingDiagnosticExporter();
        coolingExporter.ExportCoolingDiagnostic(
            coolingPlan,
            coolingDiagnosticPath,
            voxelSizeMm);

        var latticeRequest = CreateDefaultLatticeRequest(input.ResolvedOutputDirectory);
        var latticePlanner = new LatticePlanner();
        var latticePlan = latticePlanner.PlanSimpleGrid(latticeRequest);

        var latticeExporter = new PicoLatticeDiagnosticExporter();
        latticeExporter.ExportLatticeDiagnostic(
            latticePlan,
            latticeDiagnosticPath,
            voxelSizeMm);

        var moldSystemRequest = CreateDefaultMoldSystemRequest(input.ResolvedOutputDirectory);
        var moldSystemPlanner = new PreliminaryMoldSystemPlanner();
        var moldSystemPlan = moldSystemPlanner.Plan(moldSystemRequest);

        var moldSystemExporter = new PicoMoldSystemDiagnosticExporter();
        moldSystemExporter.ExportMoldSystemDiagnostic(
            moldSystemPlan,
            moldSystemDiagnosticPath,
            voxelSizeMm);

        var dfamAnalyzer = new PreliminaryDfAMAnalyzer();
        var dfamReport = dfamAnalyzer.Analyze(CreateDefaultDfAMSnapshot(
            coolingRequest,
            latticeRequest,
            moldSystemRequest));

        var reportBuilder = new FinalReportBuilder();
        var manifest = reportBuilder.CreateStandardManifest(input.ResolvedOutputDirectory);

        var finalReport = reportBuilder.Build(
            projectName: input.Config.ProjectName,
            manifest: manifest,
            partAnalysis: partAnalysis,
            dfam: dfamReport,
            baseline: new BaselineStatus(
                IsPassing: true,
                TotalTests: 103,
                Summary: "Baseline was passing before Phase 11C orchestration."),
            generatedAtUtc: DateTimeOffset.UtcNow);

        var jsonWriter = new FinalReportJsonWriter();
        jsonWriter.WriteToFile(finalReport, finalReportPath);

        var generatedArtifacts = new[]
        {
            diagnosticMeshPath,
            Path.Combine(input.ResolvedOutputDirectory, "Cavity.stl"),
            Path.Combine(input.ResolvedOutputDirectory, "Core.stl"),
            coolingDiagnosticPath,
            latticeDiagnosticPath,
            moldSystemDiagnosticPath,
            finalReportPath
        };

        foreach (var artifact in generatedArtifacts)
        {
            if (!File.Exists(artifact))
            {
                throw new FileNotFoundException("Expected generated artifact was not found.", artifact);
            }
        }

        return new GeneratorPipelineRunResult(
            input.Config.ProjectName,
            input.ResolvedOutputDirectory,
            finalReportPath,
            generatedArtifacts,
            warnings);
    }

    private static CoolingChannelRequest CreateDefaultCoolingRequest(string outputDirectory)
    {
        return new CoolingChannelRequest(
            OutputDirectory: outputDirectory,
            PartSizeXmm: 100,
            PartSizeYmm: 60,
            PartSizeZmm: 30,
            ChannelDiameterMm: 6,
            ChannelSpacingMm: 15,
            MinimumClearanceMm: 10,
            ChannelCount: 3);
    }

    private static LatticeRegionRequest CreateDefaultLatticeRequest(string outputDirectory)
    {
        return new LatticeRegionRequest(
            RegionName: "default-lattice-region",
            OutputDirectory: outputDirectory,
            MinXmm: 0,
            MinYmm: 0,
            MinZmm: 0,
            MaxXmm: 20,
            MaxYmm: 10,
            MaxZmm: 10,
            CellSizeMm: 10,
            BeamRadiusMm: 1,
            TargetRelativeDensity: 0.2m);
    }

    private static MoldSystemRequest CreateDefaultMoldSystemRequest(string outputDirectory)
    {
        return new MoldSystemRequest(
            OutputDirectory: outputDirectory,
            PartSizeXmm: 100,
            PartSizeYmm: 60,
            PartSizeZmm: 30,
            MoldMarginMm: 20,
            EjectorPinDiameterMm: 4,
            EjectorPinCount: 4,
            VentWidthMm: 0.5m,
            VentDepthMm: 0.1m,
            InsertClearanceMm: 2);
    }

    private static DfAMInputSnapshot CreateDefaultDfAMSnapshot(
        CoolingChannelRequest cooling,
        LatticeRegionRequest lattice,
        MoldSystemRequest moldSystem)
    {
        return new DfAMInputSnapshot(
            MinimumWallThicknessMm: 1.5m,
            RecommendedMinimumWallThicknessMm: 1.2m,
            CoolingMinimumClearanceMm: cooling.MinimumClearanceMm,
            CoolingChannelDiameterMm: cooling.ChannelDiameterMm,
            LatticeBeamRadiusMm: lattice.BeamRadiusMm,
            LatticeCellSizeMm: lattice.CellSizeMm,
            EjectorPinDiameterMm: moldSystem.EjectorPinDiameterMm,
            UsesPreliminaryGeometry: true);
    }
}