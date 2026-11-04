using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.Cooling;
using PicoMoldForge.Core.DfAM;
using PicoMoldForge.Core.Exports;
using PicoMoldForge.Core.Parting;
using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

/// <summary>
/// The mold plan and the alpha report factory on the end-to-end box, without the PicoGK runtime: the steps that
/// need it (part analysis, mold halves) are replaced by their pure .NET equivalents.
/// </summary>
public sealed class GeneratorMoldPlanTests
{
    [Fact]
    public void Plan_OfTheEndToEndBox_MeasuresThePartAndItsMargin()
    {
        using var project = TestProject.Create();

        var plan = GeneratorMoldPlanner.Plan(project.Input, TestProject.AutomaticPlane(project.Input));

        Assert.Equal(0m, plan.PartBounds.MinXmm);
        Assert.Equal(20m, plan.PartBounds.MaxXmm);
        Assert.Equal(10m, plan.PartBounds.MaxYmm);
        Assert.Equal(5m, plan.PartBounds.MaxZmm);
        Assert.True(plan.BlockFit.IsPartInsideBlock);
        Assert.True(plan.BlockFit.MeetsRequiredMargin);

        // The fit is checked on the cavity: the box scaled by 1.011 about its centre reaches x = -0.11.
        Assert.Equal(24.89m, plan.BlockFit.MinimumMarginMm);
        Assert.Equal("-X", plan.BlockFit.MinimumMarginSide);
        Assert.Equal(PartingAxis.X, plan.SplitAxis);
        Assert.Equal(10m, plan.PartingOffsetMm);
        Assert.Equal("Auto (bounding-box centre)", plan.PartingSource);
        Assert.Contains(plan.Notes, n => n.StartsWith("Part bounds measured on the input STL: X 0 to 20, Y 0 to 10, Z 0 to 5 mm", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_WithManualParting_UsesTheOverride()
    {
        using var project = TestProject.Create(extraJson: "\"parting\": { \"mode\": \"Manual\", \"axis\": \"Z\", \"offsetMm\": 2.5 },\n          ");

        var plan = GeneratorMoldPlanner.Plan(project.Input, TestProject.AutomaticPlane(project.Input));

        Assert.Equal(PartingAxis.Z, plan.SplitAxis);
        Assert.Equal(2.5m, plan.PartingOffsetMm);
        Assert.Equal("Manual", plan.PartingSource);
    }

    [Fact]
    public void Plan_Auto_ReportsThePlaneFoundByGeometryNextToThePlaneInUse()
    {
        using var project = TestProject.Create();

        var plan = GeneratorMoldPlanner.Plan(project.Input, TestProject.AutomaticPlane(project.Input));

        Assert.Equal(PartingAxis.X, plan.SplitAxis);
        Assert.Contains("Parting plane by geometry: Z = 0 mm (flat face level): 0.0 % of the surface trapped, 42.9 % with less than 1 deg of draft.", plan.Notes);
        Assert.Contains("Plane in use: X = 10 mm (Auto (bounding-box centre)): 0.0 % trapped, 85.7 % with less than 1 deg of draft.", plan.Notes);
        Assert.DoesNotContain(plan.Notes, n => n.StartsWith("Set \"parting\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_Geometry_SplitsAtThePlaneFoundOnTheMesh()
    {
        using var project = TestProject.Create(extraJson: "\"parting\": { \"mode\": \"Geometry\" },\n          ");

        var plan = GeneratorMoldPlanner.Plan(project.Input, TestProject.AutomaticPlane(project.Input));
        var report = project.CreateReport();

        Assert.True(project.Input.PartingByGeometry);
        Assert.Equal(PartingAxis.Z, plan.SplitAxis);
        Assert.Equal(0m, plan.PartingOffsetMm);
        Assert.Equal("Geometry", plan.PartingSource);
        Assert.Contains("Split by geometry (parting.mode Geometry).", plan.Notes);

        // The bottom face of the cavity is at z = 2.5 - 2.5 x 1.011: the mold split moves with it.
        Assert.Equal(-0.0275m, plan.MoldPartingOffsetMm);
        Assert.Contains("Mold split at Z = -0.0275 mm: the part split at 0 mm, moved with the scaled cavity.", plan.Notes);

        // Split on the bottom face: top and bottom open along their normals, the four walls lack draft.
        Assert.Equal(4, report.DraftGeometry!.PositiveDraftCount);
        Assert.Equal(300m, report.DraftGeometry.RiskySurfaceAreaMm2);
        Assert.Equal(0m, report.UndercutRisk!.RiskySurfaceAreaMm2);
        Assert.Contains(report.Warnings, w => w.StartsWith("Draft and undercut measured on the input STL for a two-half mold split by the plane Z = 0 mm", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_ScalesTheCavityByTheMaterialShrinkage_WhenNoResinIsGiven()
    {
        using var project = TestProject.Create();

        var plan = GeneratorMoldPlanner.Plan(project.Input, TestProject.AutomaticPlane(project.Input));

        Assert.Equal(0.011m, plan.Shrinkage.Rate);
        Assert.Equal("material.shrinkageRate", plan.Shrinkage.Source);
        Assert.Equal(1.011m, plan.ShrinkageScale);
        Assert.Equal(-0.11m, plan.CavityBounds.MinXmm);
        Assert.Equal(20.11m, plan.CavityBounds.MaxXmm);
        Assert.Equal(-0.055m, plan.CavityBounds.MinYmm);
        Assert.Equal(10.055m, plan.CavityBounds.MaxYmm);
        Assert.Equal(-0.0275m, plan.CavityBounds.MinZmm);
        Assert.Equal(5.0275m, plan.CavityBounds.MaxZmm);
        Assert.Equal(10m, plan.MoldPartingOffsetMm);
        Assert.Contains("Cavity: the part scaled by 1.011 about its bounding-box centre, X -0.11 to 20.11, Y -0.055 to 10.055, Z -0.0275 to 5.0275 mm (20.22 x 10.11 x 5.055 mm); the mold halves are cut around it.", plan.Notes);
    }

    [Theory]
    [InlineData("\"resin\": { \"name\": \"ABS\" },\n          ", 0.005, "resin (recommended rate)")]
    [InlineData("\"resin\": { \"name\": \"PP\", \"shrinkageRate\": 0.015 },\n          ", 0.015, "resin")]
    public void Plan_UsesTheResinShrinkage(string resinJson, double rate, string source)
    {
        using var project = TestProject.Create(extraJson: resinJson);

        var plan = GeneratorMoldPlanner.Plan(project.Input, TestProject.AutomaticPlane(project.Input));

        Assert.Equal((decimal)rate, plan.Shrinkage.Rate);
        Assert.Equal(source, plan.Shrinkage.Source);
        Assert.Equal(1m + (decimal)rate, plan.ShrinkageScale);
    }

    [Fact]
    public void Validator_RejectsAnUnknownResinWithoutRate()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            TestProject.Create(extraJson: "\"resin\": { \"name\": \"Mystery\" },\n          ").Dispose());

        Assert.Contains("resin.shrinkageRate is required for resin 'Mystery'", error.Message);
    }

    [Fact]
    public void Report_OfAPartOutsideTheBlock_SaysSoAndStillCompletes()
    {
        using var project = TestProject.Create(offsetX: 200f);

        var report = project.CreateReport();

        Assert.NotNull(report.MoldBlockFit);
        Assert.False(report.MoldBlockFit!.IsPartInsideBlock);
        Assert.Equal("+X", report.MoldBlockFit.MinimumMarginSide);
        Assert.Equal(-95.11m, report.MoldBlockFit.MinimumMarginMm);
        Assert.Contains(report.Warnings, w => w.StartsWith("FAIL: the cavity sticks out of the mold block by 95.11 mm on +X", StringComparison.Ordinal));

        // No feature is placed around a cavity that does not fit in the block.
        Assert.Null(report.MoldLayout);
        Assert.Equal(0, report.GateRunnerSprue!.SegmentCount);
        Assert.Equal(0, report.EjectorCandidates!.CandidateCount);
        Assert.Equal(0, report.CoolingChannels!.ChannelCount);
        Assert.Contains(report.Warnings, w => w.StartsWith("Mold layout skipped: the cavity does not fit inside the mold block", StringComparison.Ordinal));
    }

    [Fact]
    public void Report_OfTheEndToEndBox_PlacesTheFeedPinsAndCoolingAroundTheCavity()
    {
        using var project = TestProject.Create();

        var report = project.CreateReport();
        var layout = report.MoldLayout!;

        // Split X = 10: the halves hold the part alike, so the - half moves; its 10.11 x 5.055 mm end face takes
        // one 4 mm pin with its margin. The block leaves 24.89 mm of steel behind the cavity in the - half, less
        // than a channel needs (10 + 6 + 0.1 + 10 mm); the + half takes two of three (the third would cross the sprue).
        Assert.Equal("-X", layout.MovingHalf);
        Assert.True(layout.FeedIsFeasible);
        Assert.Equal(1, layout.EjectorPinCount);
        Assert.Equal(4, layout.RequestedEjectorPinCount);
        Assert.Equal(2, layout.CoolingChannelCount);
        Assert.True(layout.MinimumFeedCavityClearanceMm >= 0.5m);
        Assert.True(layout.MinimumCoolingCavityClearanceMm >= 10m);
        Assert.Contains(report.Warnings, w => w.StartsWith("Mold halves: the moving half is BooleanCoreSide.stl (the - side of the split)", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, w => w.StartsWith("Feed: edge gate on the parting plane", StringComparison.Ordinal));

        // The evaluators judge the measured layout.
        Assert.Equal(3, report.GateRunnerSprue!.SegmentCount);
        Assert.Equal(3, report.GateRunnerSprue.GeneratableSegmentCount);
        Assert.Equal(1, report.EjectorCandidates!.AcceptedCandidateCount);
        Assert.Equal(2, report.CoolingChannels!.SubtractableChannelCount);
        Assert.Equal(6, report.ClearanceMatrix!.FeatureCount);
        Assert.Equal(12, report.ClearanceMatrix.PairCount);
        Assert.Equal(0, report.ClearanceMatrix.CollisionRiskPairCount);
        Assert.True(report.ClearanceMatrix.MinimumClearanceMarginMm >= 0m);

        var gate = layout.Features.Single(f => f.Kind == "Gate");
        var sprue = layout.Features.Single(f => f.Kind == "Sprue");
        Assert.Equal(10, gate.Start.X, 9);
        Assert.Equal(125, sprue.End.X, 9);
        Assert.Equal(1, sprue.Half);
    }

    [Fact]
    public void Report_Geometry_MovesTheHalfThatHoldsThePocket()
    {
        using var project = TestProject.Create(extraJson: "\"parting\": { \"mode\": \"Geometry\" },\n          ");

        var report = project.CreateReport();
        var layout = report.MoldLayout!;

        // Split on the bottom face: the + half holds the pocket and moves; its 20.22 x 10.11 mm top face takes three
        // 4 mm pins 8 mm apart. The sprue goes down through the flat - half.
        Assert.Equal("+Z", layout.MovingHalf);
        Assert.Equal(3, layout.EjectorPinCount);
        Assert.All(layout.Features.Where(f => f.Kind == "EjectorPin"), f => Assert.Equal(5.0275, f.Start.Z, 6));
        Assert.Equal(-25, layout.Features.Single(f => f.Kind == "Sprue").End.Z, 9);
        Assert.Contains(report.Warnings, w => w.StartsWith("Mold halves: the moving half is BooleanCavitySide.stl (the + side of the split)", StringComparison.Ordinal));
        Assert.Equal(3, report.EjectorCandidates!.AcceptedCandidateCount);
        Assert.Equal(0, report.ClearanceMatrix!.CollisionRiskPairCount);
    }

    [Fact]
    public void Report_OfTheEndToEndBox_ScoresAndSplitsTheRealPart()
    {
        using var project = TestProject.Create();

        var report = project.CreateReport();

        Assert.True(report.MoldBlockFit!.MeetsRequiredMargin);
        Assert.NotNull(report.PartingPlane);
        Assert.Contains(report.Warnings, w => w.StartsWith("Mold block margin around the cavity: at least 24.89 mm (-X); required 20 mm", StringComparison.Ordinal));

        // The scorer works on the measured part (x 0 to 20 mm), not on the configured 100 x 60 x 30 mm part.
        var candidate = report.PartingPlane!.Candidate;
        Assert.DoesNotContain(report.Warnings, w => w.Contains("selected X at 50 mm", StringComparison.Ordinal));
        Assert.True(candidate.OffsetMm >= 0m && candidate.OffsetMm <= 20m, $"candidate {candidate.Axis} at {candidate.OffsetMm}");
    }

    internal sealed class TestProject : IDisposable
    {
        private readonly string _directory;

        private TestProject(string directory, GeneratorPipelineInput input)
        {
            _directory = directory;
            Input = input;
        }

        public GeneratorPipelineInput Input { get; }

        public static TestProject Create(string extraJson = "", float offsetX = 0f)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-plan-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var stl = Path.Combine(directory, "part-binary.stl");
            var config = Path.Combine(directory, "project.json");

            GeneratorEndToEndPipelineTests.WriteBinaryBoxStl(stl, 20f, 10f, 5f);

            if (offsetX != 0f)
            {
                Translate(stl, offsetX);
            }

            GeneratorEndToEndPipelineTests.WriteProjectConfig(config, "part-binary.stl", "output", extraJson);
            return new TestProject(directory, new GeneratorPipelineInputValidator().Validate(config));
        }

        /// <summary>The automatic plane the PicoGK part analysis gives for the STL: dominant axis, centre.</summary>
        public static PartingPlaneResult AutomaticPlane(GeneratorPipelineInput input)
        {
            var bounds = Core.Engineering.WallThickness.MeshBounds.Of(
                Core.Engineering.WallThickness.BinaryStlTriangleReader.Read(input.ResolvedInputPath));
            return new PartingPlaneEngine().CalculateAutomatic(new PartingBoundingBox(
                (float)bounds.Min.X, (float)bounds.Min.Y, (float)bounds.Min.Z,
                (float)bounds.Max.X, (float)bounds.Max.Y, (float)bounds.Max.Z));
        }

        public FunctionalMoldAlphaReport CreateReport()
        {
            var plane = AutomaticPlane(Input);
            var part = new PartAnalysisReport(
                Input.ResolvedInputPath, 12, 8, string.Empty, 1f, 1000f, 5, 0, string.Empty,
                Array.Empty<PartAnalysisWarning>(), plane);
            var cooling = new CoolingPlanner().PlanStraightChannels(new CoolingChannelRequest(
                OutputDirectory: Input.ResolvedOutputDirectory,
                PartSizeXmm: Input.Cooling.PartSizeXmm,
                PartSizeYmm: Input.Cooling.PartSizeYmm,
                PartSizeZmm: Input.Cooling.PartSizeZmm,
                ChannelDiameterMm: Input.Cooling.ChannelDiameterMm,
                ChannelSpacingMm: Input.Cooling.ChannelSpacingMm,
                MinimumClearanceMm: Input.Cooling.MinimumClearanceMm,
                ChannelCount: Input.Cooling.ChannelCount));
            var dfam = new DfAMReport(true, Array.Empty<DfAMCheckResult>(), Array.Empty<string>());

            return GeneratorFunctionalMoldAlphaReportFactory.Create(Input, part, cooling, dfam);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Moves every vertex of a binary STL along X, in place.</summary>
        private static void Translate(string path, float offsetX)
        {
            var bytes = File.ReadAllBytes(path);
            var count = BitConverter.ToUInt32(bytes, 80);

            for (var i = 0; i < count; i++)
            {
                for (var v = 0; v < 3; v++)
                {
                    var at = 84 + (50 * i) + 12 + (12 * v);
                    var x = BitConverter.ToSingle(bytes, at) + offsetX;
                    BitConverter.GetBytes(x).CopyTo(bytes, at);
                }
            }

            File.WriteAllBytes(path, bytes);
        }
    }
}
