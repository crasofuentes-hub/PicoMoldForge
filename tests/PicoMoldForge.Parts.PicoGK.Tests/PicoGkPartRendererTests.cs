using PicoMoldForge.Parts.Meshing;
using PicoMoldForge.Parts.PicoGK;
using PicoMoldForge.Parts.Recipes;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PicoMoldForge.Parts.PicoGK.Tests;

/// <summary>
/// Native PicoGK tests are opt-in so the default <c>dotnet test</c> never depends on the native runtime:
/// set PICOMOLDFORGE_PARTS_PICOGK_TESTS=1 (scripts/run-parts-demo.ps1 does). Once opted in, a missing
/// runtime is a failure, not a skip.
/// </summary>
public sealed class PicoGkTheoryAttribute : TheoryAttribute
{
    public const string EnvironmentVariable = "PICOMOLDFORGE_PARTS_PICOGK_TESTS";

    public PicoGkTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
        {
            Skip = $"Opt-in: set {EnvironmentVariable}=1 to run the PicoGK native tests.";
        }
    }
}

public sealed class PicoGkPartRendererTests
{
    [Fact]
    public void RuntimeAvailability_ExplainsItself()
    {
        Assert.False(string.IsNullOrWhiteSpace(PicoGkRuntimeAvailability.Reason));
    }

    [Fact]
    public void SuggestedVoxel_IsCoarserThanTheMeshAndBoundsTheVoxelCount()
    {
        foreach (var sample in new[] { "spur-gear", "piston", "monoblock" })
        {
            var part = RecipeLoader.LoadFile(RepositoryPaths.Sample(sample));
            var voxel = PicoGkPartRenderer.SuggestVoxelSize(part);
            var size = part.Sdf.Bounds.Size;

            Assert.True(voxel >= 2 * part.Mesh.ResolutionMm - 1e-9, $"{sample}: {voxel}");
            Assert.True(size.X * size.Y * size.Z / (voxel * voxel * voxel) <= 8_000_000 * 1.01, $"{sample}: too many voxels");
        }
    }

    [PicoGkTheory]
    [InlineData("spur-gear", 0.3)]
    [InlineData("flange", 0.4)]
    public void Render_MatchesTheReferenceMesher(string sample, double voxel)
    {
        Assert.True(PicoGkRuntimeAvailability.IsAvailable, PicoGkRuntimeAvailability.Reason);

        var part = RecipeLoader.LoadFile(RepositoryPaths.Sample(sample));
        var reference = MeshAnalysis.Analyze(SdfMesher.Mesh(part.Sdf, new MeshingOptions(voxel / 2)));
        var stl = Path.Combine(Path.GetTempPath(), $"pmf-parts-picogk-{sample}-{Guid.NewGuid():N}.stl");

        try
        {
            var render = PicoGkPartRenderer.Render(part, voxel, stl);

            Assert.True(render.Triangles > 0);
            Assert.True(new FileInfo(stl).Length == 84 + (50L * render.Triangles), "binary STL size does not match the triangle count");

            var volumeTolerance = Math.Max(0.03, 0.25 * reference.AreaMm2 * voxel / reference.VolumeMm3);
            Assert.InRange(Math.Abs(render.VolumeMm3 - reference.VolumeMm3) / reference.VolumeMm3, 0, volumeTolerance);

            var worst = new[]
            {
                Math.Abs(render.BoundsMin.X - reference.BoundsMin.X), Math.Abs(render.BoundsMin.Y - reference.BoundsMin.Y), Math.Abs(render.BoundsMin.Z - reference.BoundsMin.Z),
                Math.Abs(render.BoundsMax.X - reference.BoundsMax.X), Math.Abs(render.BoundsMax.Y - reference.BoundsMax.Y), Math.Abs(render.BoundsMax.Z - reference.BoundsMax.Z)
            }.Max();
            Assert.InRange(worst, 0, 1.5 * voxel);
        }
        finally
        {
            if (File.Exists(stl))
            {
                File.Delete(stl);
            }
        }
    }
}

internal static class RepositoryPaths
{
    public static string Sample(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PicoMoldForge.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root (PicoMoldForge.sln) not found above the test output folder.");
        }

        return Path.Combine(directory.FullName, "samples", "parts", name + ".json");
    }
}
