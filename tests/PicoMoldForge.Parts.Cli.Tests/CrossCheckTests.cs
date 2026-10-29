using System.Text.Json.Nodes;
using PicoMoldForge.Parts.Cli;
using PicoMoldForge.Parts.Export;
using PicoMoldForge.Parts.Meshing;
using PicoMoldForge.Parts.PicoGK;
using PicoMoldForge.Parts.Pipeline;
using PicoMoldForge.Parts.Recipes;

namespace PicoMoldForge.Parts.Cli.Tests;

/// <summary>
/// The cross-check logic without the PicoGK runtime: a reference mesh stands in for the PicoGK rendering.
/// </summary>
public sealed class CrossCheckTests
{
    private const double Resolution = 0.8;

    [Fact]
    public void SameSolid_Passes()
    {
        using var temp = new TempDirectory();
        var flange = Generate(LoadFlange(), Path.Combine(temp.Path, "flange"));

        var cross = PartsRunner.CompareWithReference(flange.Report, FakeRender(flange), StlReader.ReadBinary(flange.Stl), 0.4, "flange.picogk.stl");

        Assert.Equal("PASS", cross.Status);
        Assert.True(cross.Topology!.Matches);
        Assert.Equal(8, cross.Topology.Genus);
    }

    [Fact]
    public void MissingSmallHole_FailsOnTopologyAlthoughVolumeAndBoundsAgree()
    {
        using var temp = new TempDirectory();
        var flange = Generate(LoadFlange(), Path.Combine(temp.Path, "flange"));
        var recipe = LoadFlange();
        var features = recipe["features"]!.AsArray();
        features.Remove(features.Single(f => (string?)f!["id"] == "grease-hole"));
        var expect = recipe["expect"]!.AsObject();
        expect["genus"] = 7;
        var points = expect["surfacePoints"]!.AsArray();
        points.Remove(points.Single(p => (string?)p!["id"] == "flange.grease-hole"));
        var withoutHole = Generate(recipe, Path.Combine(temp.Path, "flange-without-grease-hole"));

        var cross = PartsRunner.CompareWithReference(flange.Report, FakeRender(withoutHole), StlReader.ReadBinary(withoutHole.Stl), 0.4, "flange.picogk.stl");

        Assert.Equal("FAIL", cross.Status);
        Assert.True(cross.VolumeDifference <= cross.VolumeTolerance, "the missing hole is inside the volume tolerance");
        Assert.True(cross.BoundsDeviationMm <= cross.BoundsTolerance, "the missing hole does not change the bounding box");
        Assert.Equal(7, cross.Topology!.Genus);
        Assert.Contains("genero 7 contra 1 y 8", cross.Reason, StringComparison.Ordinal);
    }

    private static JsonObject LoadFlange()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PicoMoldForge.sln")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "samples", "parts", "flange.json");
        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }

    private static (PartReport Report, string Stl, MeshReport Mesh) Generate(JsonObject recipe, string folder)
    {
        var part = RecipeLoader.Parse(recipe.ToJsonString(), "flange.json");
        var result = new PartGenerator().Generate(part, folder, new PartGenerationOptions(Resolution, DecimateOverride: false, WriteViewerData: false, SurfaceSamples: 2_000));
        Assert.True(result.Report.Passed, string.Join("; ", result.Report.Checks.Where(c => !c.Passed).Select(c => c.Id)));
        return (result.Report, Path.Combine(folder, part.Id + ".stl"), MeshAnalysis.Analyze(result.PrintMesh));
    }

    private static PicoGkRenderResult FakeRender((PartReport Report, string Stl, MeshReport Mesh) part) =>
        new(part.Stl, 0.4, part.Mesh.VolumeMm3, part.Mesh.BoundsMin, part.Mesh.BoundsMax, part.Mesh.Triangles, part.Mesh.Vertices, 0);
}
