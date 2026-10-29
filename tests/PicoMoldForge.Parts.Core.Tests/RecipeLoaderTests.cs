using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Recipes;

namespace PicoMoldForge.Parts.Core.Tests;

public sealed class RecipeLoaderTests
{
    private const string Header = "\"schemaVersion\": \"picomoldforge.part-recipe.v1\", \"name\": \"test-part\"";

    private const string MinimalFeatures =
        "\"features\": [ { \"id\": \"body\", \"op\": \"add\", \"sweep\": { \"kind\": \"linear\", \"axis\": \"Z\", \"from\": 0, \"to\": 10 }, \"profile\": { \"type\": \"circle\", \"diameter\": 20 } } ]";

    public static TheoryData<string> SampleNames => new() { "spur-gear", "helical-gear", "piston", "monoblock", "flange" };

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Samples_Load(string name)
    {
        var part = RecipeLoader.LoadFile(RepositoryPaths.Sample(name));

        Assert.Equal(name, part.Id);
        Assert.NotEmpty(part.Features);
        Assert.Equal(FeatureOperation.Add, part.Features[0].Operation);
        Assert.Empty(part.Mesh.Validate());
    }

    [Fact]
    public void FeatureRecipe_LoadsWithItsExpectations()
    {
        var part = RecipeLoader.LoadFile(RepositoryPaths.Sample("flange"));

        Assert.Equal("features", part.Template);
        Assert.Equal(new[] { "body", "bore", "bolt-holes", "grease-hole" }, part.Features.Select(f => f.Id));
        Assert.Equal(6, part.Features.Single(f => f.Id == "bolt-holes").Instances.Count);
        Assert.Equal(8, part.Expectations.Genus);
        Assert.Equal(5, part.Expectations.Checks.Count);
    }

    [Fact]
    public void MinimalFeatureRecipe_Parses()
    {
        var part = RecipeLoader.Parse("{" + Header + ", " + MinimalFeatures + "}", "minimal.json");

        Assert.Single(part.Features);
        Assert.True(part.Sdf.Distance(new Geometry.Vec3(0, 0, 5)) < 0);
        Assert.True(part.Sdf.Distance(new Geometry.Vec3(11, 0, 5)) > 0);
    }

    [Theory]
    [InlineData("\"unexpected\": 1", "$.unexpected: unknown property")]
    [InlineData("\"units\": \"inch\"", "$.units")]
    [InlineData("\"axisConvention\": \"KUKA\"", "$.axisConvention")]
    [InlineData("\"template\": { \"type\": \"spurGear\" }", "exactly one of 'template' or 'features'")]
    [InlineData("\"mesh\": { \"resolutionMm\": 0.001 }", "mesh.resolutionMm")]
    public void InvalidRoot_ReportsThePath(string extra, string expectedFragment)
    {
        var json = "{" + Header + ", " + MinimalFeatures + ", " + extra + "}";

        var error = Assert.Throws<RecipeException>(() => RecipeLoader.Parse(json, "bad.json"));

        Assert.Contains(error.Errors, e => e.Contains(expectedFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void WrongSchemaVersion_IsRejected()
    {
        var json = "{ \"schemaVersion\": \"v0\", \"name\": \"x\", " + MinimalFeatures + "}";

        var error = Assert.Throws<RecipeException>(() => RecipeLoader.Parse(json, "bad.json"));

        Assert.Contains(error.Errors, e => e.StartsWith("$.schemaVersion", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{ \"id\": \"cut\", \"op\": \"subtract\", \"sweep\": { \"kind\": \"linear\", \"axis\": \"Z\", \"from\": 0, \"to\": 1 }, \"profile\": { \"type\": \"circle\", \"diameter\": 2 } }", "first feature must be 'add'")]
    [InlineData("{ \"id\": \"body\", \"op\": \"add\", \"sweep\": { \"kind\": \"linear\", \"axis\": \"C\", \"from\": 0, \"to\": 1 }, \"profile\": { \"type\": \"circle\", \"diameter\": 2 } }", "linear sweeps use X, Y or Z")]
    [InlineData("{ \"id\": \"body\", \"op\": \"add\", \"sweep\": { \"kind\": \"rotary\", \"axis\": \"Z\" }, \"profile\": { \"type\": \"circle\", \"diameter\": 2 } }", "rotary sweeps use A (about X), B (about Y) or C (about Z)")]
    [InlineData("{ \"id\": \"body\", \"op\": \"weld\", \"sweep\": { \"kind\": \"linear\", \"axis\": \"Z\", \"from\": 0, \"to\": 1 }, \"profile\": { \"type\": \"circle\", \"diameter\": 2 } }", "use 'add', 'subtract' or 'intersect'")]
    [InlineData("{ \"id\": \"bad id\", \"op\": \"add\", \"sweep\": { \"kind\": \"linear\", \"axis\": \"Z\", \"from\": 0, \"to\": 1 }, \"profile\": { \"type\": \"circle\", \"diameter\": 2 } }", ".id: use only ASCII letters")]
    [InlineData("{ \"id\": \"body\", \"op\": \"add\", \"sweep\": { \"kind\": \"linear\", \"axis\": \"Z\", \"from\": 1, \"to\": 1 }, \"profile\": { \"type\": \"circle\", \"diameter\": 2 } }", "'from' and 'to' must differ")]
    public void InvalidFeature_IsRejected(string feature, string expectedFragment)
    {
        var json = "{" + Header + ", \"features\": [" + feature + "] }";

        var error = Assert.Throws<RecipeException>(() => RecipeLoader.Parse(json, "bad.json"));

        Assert.Contains(error.Errors, e => e.Contains(expectedFragment, StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateFeatureIds_AreRejected()
    {
        var feature = "{ \"id\": \"body\", \"op\": \"add\", \"sweep\": { \"kind\": \"linear\", \"axis\": \"Z\", \"from\": 0, \"to\": 1 }, \"profile\": { \"type\": \"circle\", \"diameter\": 2 } }";
        var json = "{" + Header + ", \"features\": [" + feature + ", " + feature + "] }";

        var error = Assert.Throws<RecipeException>(() => RecipeLoader.Parse(json, "bad.json"));

        Assert.Contains(error.Errors, e => e.Contains("duplicate feature id 'body'", StringComparison.Ordinal));
    }

    [Fact]
    public void ExpectOnTemplates_IsRejected()
    {
        var json = "{" + Header + ", \"template\": { \"type\": \"spurGear\" }, \"expect\": { \"genus\": 1 } }";

        var error = Assert.Throws<RecipeException>(() => RecipeLoader.Parse(json, "bad.json"));

        Assert.Contains(error.Errors, e => e.StartsWith("$.expect", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidJson_IsARecipeError()
    {
        var error = Assert.Throws<RecipeException>(() => RecipeLoader.Parse("{ not json", "broken.json"));

        Assert.Equal("broken.json", error.Source);
        Assert.NotEmpty(error.Errors);
    }

    [Fact]
    public void ImpossibleGear_IsReportedAsARecipeError()
    {
        var json = "{" + Header + ", \"template\": { \"type\": \"spurGear\", \"module\": 2, \"teeth\": 3 } }";

        var error = Assert.Throws<RecipeException>(() => RecipeLoader.Parse(json, "bad.json"));

        Assert.NotEmpty(error.Errors);
    }
}
