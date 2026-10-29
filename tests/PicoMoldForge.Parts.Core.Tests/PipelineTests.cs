using System.Text;
using System.Text.Json;
using PicoMoldForge.Parts.Export;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Meshing;
using PicoMoldForge.Parts.Pipeline;
using PicoMoldForge.Parts.Recipes;
using PicoMoldForge.Parts.Templates;

namespace PicoMoldForge.Parts.Core.Tests;

public sealed class TemplatePipelineTests
{
    // Coarse resolutions keep the suite fast; every check still has to pass (tolerances scale with resolution).
    [Theory]
    [InlineData("spur-gear", 0.4, 1)]
    [InlineData("helical-gear", 0.4, 1)]
    [InlineData("piston", 0.6, 2)]
    [InlineData("monoblock", 1.2, 5)]
    [InlineData("flange", 0.6, 8)]
    public void Sample_GeneratesAndPassesEveryCheck(string sample, double resolution, int genus)
    {
        using var temp = new TempDirectory();
        var part = RecipeLoader.LoadFile(RepositoryPaths.Sample(sample));

        var result = new PartGenerator().Generate(part, temp.Path, new PartGenerationOptions(resolution, DecimateOverride: false, SurfaceSamples: 12_000));

        Assert.True(result.Report.Passed, string.Join(Environment.NewLine, result.Report.Checks.Where(c => !c.Passed).Select(c => $"{c.Id}: expected {c.Expected}, actual {c.Actual}")));
        Assert.Equal(genus, result.Report.PrintMesh.Genus);
        Assert.True(result.Report.Lattice?.ReplayMatchesSolid);
        Assert.Equal(PartReport.SchemaVersion, result.Report.Schema);

        foreach (var file in new[] { sample + ".stl", PartGenerator.PreviewFileName, PartGenerator.FeaturesFileName, PartGenerator.LatticeFileName, PartGenerator.ReportFileName })
        {
            Assert.True(File.Exists(Path.Combine(temp.Path, file)), file + " was not written");
        }

        var stl = File.ReadAllBytes(Path.Combine(temp.Path, sample + ".stl"));
        Assert.Equal(84 + (50L * result.PrintMesh.TriangleCount), stl.Length);
        Assert.Equal((uint)result.PrintMesh.TriangleCount, BitConverter.ToUInt32(stl, 80));
    }

    [Fact]
    public void GearTemplateChecks_FailWhenTheGeometryIsWrong()
    {
        // The template checks read the distance field, not the parameters: a gear built with a thickness
        // allowance must fail the nominal pitch-thickness check of an unmodified gear of the same size.
        var nominal = GearTemplate.Build("nominal", null, new GearParameters(), RecipeLoader.DefaultMesh(GearTemplate.SpurType), helical: false);
        var thinned = GearTemplate.Build("thinned", null, new GearParameters { ThicknessAllowance = 0.2 }, RecipeLoader.DefaultMesh(GearTemplate.SpurType), helical: false);
        var check = nominal.Expectations.Checks.Single(c => c.Id == "gear.pitch-thickness");

        Assert.True(check.Evaluate(new Verification.PartCheckContext(nominal.Sdf, 0.12)).Passed);
        Assert.False(check.Evaluate(new Verification.PartCheckContext(thinned.Sdf, 0.12)).Passed);
    }

    [Fact]
    public void Generation_IsDeterministic()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        var part = RecipeLoader.LoadFile(RepositoryPaths.Sample("spur-gear"));
        var options = new PartGenerationOptions(0.5, SurfaceSamples: 4_000);

        new PartGenerator().Generate(part, first.Path, options);
        new PartGenerator().Generate(part, second.Path, options);

        foreach (var file in new[] { "spur-gear.stl", PartGenerator.PreviewFileName, PartGenerator.FeaturesFileName, PartGenerator.LatticeFileName })
        {
            Assert.Equal(RunManifestWriter.Sha256(Path.Combine(first.Path, file)), RunManifestWriter.Sha256(Path.Combine(second.Path, file)));
        }
    }
}

public sealed class ExportTests
{
    [Fact]
    public void Stl_IsBinaryWithoutTimestamps_AndDeterministic()
    {
        var mesh = SdfMesher.Mesh(new SphereSdf(Vec3.Zero, 5), new MeshingOptions(1.0));
        using var a = new MemoryStream();
        using var b = new MemoryStream();

        StlWriter.Write(mesh, a, "PicoMoldForge Parts test solid");
        StlWriter.Write(mesh, b, "PicoMoldForge Parts test solid");

        var bytes = a.ToArray();
        Assert.Equal(bytes, b.ToArray());
        Assert.Equal(84 + (50 * mesh.TriangleCount), bytes.Length);
        // ASCII STL readers look for a leading "solid": binary files must not start with it.
        Assert.DoesNotContain("solid", Encoding.ASCII.GetString(bytes, 0, 80), StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewMesh_HasTheDocumentedHeader()
    {
        using var temp = new TempDirectory();
        var mesh = SdfMesher.Mesh(new SphereSdf(Vec3.Zero, 5), new MeshingOptions(1.0));
        var path = Path.Combine(temp.Path, "preview.bin");

        PreviewMeshWriter.Write(mesh, path);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(PreviewMeshWriter.Magic, Encoding.ASCII.GetString(bytes, 0, 8));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal((uint)mesh.Vertices.Length, BitConverter.ToUInt32(bytes, 12));
        Assert.Equal((uint)mesh.TriangleCount, BitConverter.ToUInt32(bytes, 16));
        Assert.Equal(44 + (6L * mesh.Vertices.Length) + (12L * mesh.TriangleCount), bytes.Length);
    }

    [Fact]
    public void Features_DescribeEveryFeatureWithItsFrame()
    {
        using var temp = new TempDirectory();
        var part = RecipeLoader.LoadFile(RepositoryPaths.Sample("flange"));
        var path = Path.Combine(temp.Path, "features.json");

        FeatureExporter.Write(part, path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Assert.Equal(FeatureExporter.Schema, root.GetProperty("schema").GetString());
        var features = root.GetProperty("features").EnumerateArray().ToArray();
        Assert.Equal(part.Features.Select(f => f.Id), features.Select(f => f.GetProperty("id").GetString()));
        Assert.Equal(6, features.Single(f => f.GetProperty("id").GetString() == "bolt-holes").GetProperty("instances").GetArrayLength());
    }

    [Fact]
    public void Lattice_ReplayMatchesTheSolid()
    {
        using var temp = new TempDirectory();
        var part = RecipeLoader.LoadFile(RepositoryPaths.Sample("flange"));
        var path = Path.Combine(temp.Path, "lattice.bin");

        var info = LatticeExporter.Write(part, path);

        Assert.True(info.ReplayMatchesSolid);
        Assert.True(info.PointsInside > 0);
        Assert.Equal(LatticeExporter.Magic, Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 8));
    }
}

public sealed class RunManifestTests
{
    [Fact]
    public void Manifest_ListsRelativePathsWithHashes()
    {
        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, "part", "data.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "abc");

        var manifestPath = RunManifestWriter.Write(temp.Path, "test", new[] { new RunManifestPart("part", "Part", "PASS", "reference") }, new[] { file });

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var artifact = document.RootElement.GetProperty("artifacts")[0];
        Assert.Equal("part/data.txt", artifact.GetProperty("path").GetString());
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", artifact.GetProperty("sha256").GetString());
        Assert.Equal("PASS", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void PathGuard_RefusesDangerousFolders()
    {
        Assert.Throws<InvalidOperationException>(() => OutputPathGuard.EnsureSafe(Path.GetPathRoot(Path.GetTempPath())!));
        Assert.Throws<InvalidOperationException>(() => OutputPathGuard.EnsureSafe(Directory.GetCurrentDirectory()));
        Assert.Throws<InvalidOperationException>(() => OutputPathGuard.EnsureSafe(" "));

        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, "file.txt");
        File.WriteAllText(file, "x");
        Assert.Throws<InvalidOperationException>(() => OutputPathGuard.EnsureSafe(file));
        Assert.Equal(Path.GetFullPath(Path.Combine(temp.Path, "out")), OutputPathGuard.EnsureSafe(Path.Combine(temp.Path, "out")));
    }
}

public sealed class PlacementTests
{
    [Fact]
    public void CircularPattern_SpreadsCopiesEvenlyAboutC()
    {
        var copies = Placement.Circular(Placement.Single(new Vec3(38, 0, 0)), Axes.RotaryAxis.C, 6, 0, 360, Vec3.Zero);

        Assert.Equal(6, copies.Count);

        for (var i = 0; i < 6; i++)
        {
            var angle = i * Math.PI / 3;
            var position = copies[i].Apply(Vec3.Zero);
            Assert.True((position - new Vec3(38 * Math.Cos(angle), 38 * Math.Sin(angle), 0)).Length < 1e-9, $"copy {i} at {position}");
        }
    }

    [Fact]
    public void LinearPattern_StepsAlongTheAxis()
    {
        var copies = Placement.Linear(Placement.Single(Vec3.Zero), Axes.LinearAxis.X, 3, 52);

        Assert.Equal(new[] { 0.0, 52.0, 104.0 }, copies.Select(c => c.Apply(Vec3.Zero).X));
    }
}
