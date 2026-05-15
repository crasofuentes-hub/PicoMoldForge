using System.Text.Json;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class JsonSchemaContractTests
{
    [Fact]
    public void SchemaFiles_AreValidJsonAndDeclareDraft2020()
    {
        var repoRoot = FindRepositoryRoot();

        var schemaPaths = new[]
        {
            Path.Combine(repoRoot, "docs", "schemas", "picomoldforge.project-config.schema.json"),
            Path.Combine(repoRoot, "docs", "schemas", "picomoldforge.final-project-report.schema.json"),
            Path.Combine(repoRoot, "docs", "schemas", "picomoldforge.run-manifest.schema.json")
        };

        foreach (var schemaPath in schemaPaths)
        {
            Assert.True(File.Exists(schemaPath), $"Missing schema file: {schemaPath}");

            using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
            var root = document.RootElement;

            Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
            Assert.Equal("object", root.GetProperty("type").GetString());
        }
    }

    [Fact]
    public void ProjectConfigSchema_MatchesSampleConfigTopLevelShape()
    {
        var repoRoot = FindRepositoryRoot();
        var sampleConfigPath = Path.Combine(repoRoot, "samples", "generator-valid-project.json");

        Assert.True(File.Exists(sampleConfigPath), $"Missing sample config: {sampleConfigPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(sampleConfigPath));
        var root = document.RootElement;

        foreach (var propertyName in new[]
        {
            "projectName",
            "inputPath",
            "outputDirectory",
            "mode",
            "standard",
            "voxelResolutionMm",
            "material",
            "machine",
            "moldBlock",
            "cooling",
            "lattice",
            "moldSystem",
            "dfam"
        })
        {
            Assert.True(root.TryGetProperty(propertyName, out _), $"Sample config is missing {propertyName}.");
        }
    }

    [Fact]
    public void RunManifestSchema_RequiresArtifactSha256()
    {
        var repoRoot = FindRepositoryRoot();
        var schemaPath = Path.Combine(repoRoot, "docs", "schemas", "picomoldforge.run-manifest.schema.json");

        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = document.RootElement;

        var artifactRequired = root
            .GetProperty("properties")
            .GetProperty("Artifacts")
            .GetProperty("items")
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("FileName", artifactRequired);
        Assert.Contains("Path", artifactRequired);
        Assert.Contains("SizeBytes", artifactRequired);
        Assert.Contains("Sha256", artifactRequired);

        var shaPattern = root
            .GetProperty("properties")
            .GetProperty("Artifacts")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("Sha256")
            .GetProperty("pattern")
            .GetString();

        Assert.Equal("^[a-f0-9]{64}$", shaPattern);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PicoMoldForge.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}