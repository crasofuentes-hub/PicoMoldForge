using System.Text.Json;
using PicoMoldForge.Core.Analysis;
using PicoMoldForge.Core.DfAM;
using PicoMoldForge.Core.Engineering.Verification;
using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

/// <summary>The mold project written for a part STL, checked without the PicoGK runtime.</summary>
public sealed class GeneratorPartMoldProjectTests
{
    [Fact]
    public void Write_ForTheBox_ValidatesAndLeavesRoomForEveryFeature()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-part-mold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var stl = Path.Combine(directory, "box.stl");
            GeneratorEndToEndPipelineTests.WriteBinaryBoxStl(stl, 20f, 10f, 5f);

            var projectPath = GeneratorPartMoldProject.Write(stl, Path.Combine(directory, "box-mold"));
            var input = new GeneratorPipelineInputValidator().Validate(projectPath);

            // ABS (0.5 % recommended), split by geometry, 30 mm around the cavity (a channel behind it needs 27.1 mm).
            Assert.True(input.PartingByGeometry);
            Assert.Equal("ABS", input.Resin!.Name);
            Assert.Equal(0.005m, input.Resin.ShrinkageRate);
            Assert.Equal(-31m, input.BooleanMoldBlockBounds.MinXmm);
            Assert.Equal(51m, input.BooleanMoldBlockBounds.MaxXmm);
            Assert.Equal(Path.Combine(directory, "box-mold", GeneratorPartMoldProject.MoldDirectoryName), input.ResolvedOutputDirectory);

            var plane = GeneratorMoldPlanTests.TestProject.AutomaticPlane(input);
            var plan = GeneratorMoldPlanner.Plan(input, plane);
            var layout = plan.Layout!;
            Assert.True(layout.Feed!.IsFeasible);
            Assert.Equal(3, layout.Pins.Count);
            Assert.Contains(layout.Channels, c => c.Half > 0);
            Assert.Contains(layout.Channels, c => c.Half < 0);
            Assert.Equal(4, layout.GuidePins.Count);

            var part = new PartAnalysisReport(input.ResolvedInputPath, 12, 8, string.Empty, 1f, 1000f, 5, 0, string.Empty, Array.Empty<PartAnalysisWarning>(), plane);
            var report = GeneratorFunctionalMoldAlphaReportFactory.Create(
                input, part, GeneratorMoldLayout.CoolingPlan(layout), new DfAMReport(true, Array.Empty<DfAMCheckResult>(), Array.Empty<string>()), plan);

            Assert.Equal(MoldCheckStatus.Pass, report.Verification!.Status);
            Assert.DoesNotContain(report.Verification.Checks, c => c.Status is MoldCheckStatus.Fail or MoldCheckStatus.Warn);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_WithoutResin_KeepsTheMaterialShrinkage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-part-mold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var stl = Path.Combine(directory, "box.stl");
            GeneratorEndToEndPipelineTests.WriteBinaryBoxStl(stl, 20f, 10f, 5f);

            var projectPath = GeneratorPartMoldProject.Write(stl, Path.Combine(directory, "box-mold"), new GeneratorPartMoldOptions(ResinName: null));

            using var document = JsonDocument.Parse(File.ReadAllText(projectPath));
            Assert.False(document.RootElement.TryGetProperty("resin", out _));
            Assert.Null(new GeneratorPipelineInputValidator().Validate(projectPath).Resin);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Write_WithAnAsciiStl_IsRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"picomoldforge-part-mold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var stl = Path.Combine(directory, "ascii.stl");
            File.WriteAllText(stl, "solid part\nendsolid part\n");

            Assert.Throws<InvalidOperationException>(() => GeneratorPartMoldProject.Write(stl, Path.Combine(directory, "mold")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
