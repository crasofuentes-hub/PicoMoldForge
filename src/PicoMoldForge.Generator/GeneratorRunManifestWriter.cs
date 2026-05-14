using System.Security.Cryptography;
using System.Text.Json;

namespace PicoMoldForge.Generator;

public sealed class GeneratorRunManifestWriter
{
    public string Write(
        GeneratorPipelineInput input,
        GeneratorPipelineRunResult result,
        bool cleanOutput,
        string? outputOverridePath)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(result);

        Directory.CreateDirectory(result.OutputDirectory);

        var resolvedOutputOverride = string.IsNullOrWhiteSpace(outputOverridePath)
            ? null
            : Path.GetFullPath(outputOverridePath);

        var artifacts = result.GeneratedArtifacts
            .Select(CreateArtifact)
            .ToArray();

        var manifest = new GeneratorRunManifest(
            SchemaVersion: "picomoldforge.run-manifest.v1",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            ProjectName: result.ProjectName,
            ConfigPath: Path.GetFullPath(input.ConfigPath),
            OutputDirectory: Path.GetFullPath(result.OutputDirectory),
            FinalReportPath: Path.GetFullPath(result.FinalReportPath),
            CleanOutput: cleanOutput,
            UsedOutputOverride: resolvedOutputOverride is not null,
            OutputOverridePath: resolvedOutputOverride,
            Artifacts: artifacts,
            Warnings: result.Warnings);

        var manifestPath = Path.Combine(result.OutputDirectory, "RunManifest.json");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options));

        return manifestPath;
    }

    private static GeneratorRunManifestArtifact CreateArtifact(string path)
    {
        var resolvedPath = Path.GetFullPath(path);
        var fileInfo = new FileInfo(resolvedPath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Cannot add missing artifact to run manifest.", resolvedPath);
        }

        return new GeneratorRunManifestArtifact(
            FileName: fileInfo.Name,
            Path: resolvedPath,
            SizeBytes: fileInfo.Length,
            Sha256: ComputeSha256(resolvedPath));
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hashBytes = SHA256.HashData(stream);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}