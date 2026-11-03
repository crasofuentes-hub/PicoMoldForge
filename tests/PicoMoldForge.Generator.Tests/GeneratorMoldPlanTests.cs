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
        Assert.Equal(25m, plan.BlockFit.MinimumMarginMm);
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
    public void Report_OfAPartOutsideTheBlock_SaysSoAndStillCompletes()
    {
        using var project = TestProject.Create(offsetX: 200f);

        var report = project.CreateReport();

        Assert.NotNull(report.MoldBlockFit);
        Assert.False(report.MoldBlockFit!.IsPartInsideBlock);
        Assert.Equal("+X", report.MoldBlockFit.MinimumMarginSide);
        Assert.Equal(-95m, report.MoldBlockFit.MinimumMarginMm);
        Assert.Contains(report.Warnings, w => w.StartsWith("FAIL: the part sticks out of the mold block by 95 mm on +X", StringComparison.Ordinal));
    }

    [Fact]
    public void Report_OfTheEndToEndBox_ScoresAndSplitsTheRealPart()
    {
        using var project = TestProject.Create();

        var report = project.CreateReport();

        Assert.True(report.MoldBlockFit!.MeetsRequiredMargin);
        Assert.NotNull(report.PartingPlane);
        Assert.Contains(report.Warnings, w => w.StartsWith("Mold block margin: at least 25 mm (-X); required 20 mm", StringComparison.Ordinal));

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
