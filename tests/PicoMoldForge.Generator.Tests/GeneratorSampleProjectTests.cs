using PicoMoldForge.Generator;
using Xunit;

namespace PicoMoldForge.Generator.Tests;

public sealed class GeneratorSampleProjectTests
{
    [Fact]
    public void Run_WithRepositorySampleProject_GeneratesFullOutputPackage()
    {
        var repoRoot = FindRepositoryRoot();
        var configPath = Path.Combine(repoRoot, "samples", "generator-valid-project.json");
        var outputDirectory = Path.Combine(repoRoot, "samples", "generated", "generator-sample");

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

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
            Assert.Contains("Artifacts generated: 7", output.ToString());

            AssertArtifact(outputDirectory, "DiagnosticMesh.stl");
            AssertArtifact(outputDirectory, "Cavity.stl");
            AssertArtifact(outputDirectory, "Core.stl");
            AssertArtifact(outputDirectory, "CoolingDiagnostic.stl");
            AssertArtifact(outputDirectory, "LatticeDiagnostic.stl");
            AssertArtifact(outputDirectory, "MoldSystemDiagnostic.stl");
            AssertArtifact(outputDirectory, "FinalProjectReport.json");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static void AssertArtifact(string outputDirectory, string fileName)
    {
        var path = Path.Combine(outputDirectory, fileName);

        Assert.True(File.Exists(path), $"Expected artifact was not generated: {path}");
        Assert.True(new FileInfo(path).Length > 0, $"Expected artifact was empty: {path}");
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