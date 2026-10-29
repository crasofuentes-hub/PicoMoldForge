using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PicoMoldForge.Parts.Pipeline;

public sealed record RunManifestArtifact(string Path, long SizeBytes, string Sha256);

public sealed record RunManifestPart(string Id, string Title, string Status, string Backend);

public sealed record RunManifest(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string Tool,
    string ToolVersion,
    string Command,
    string OutputDirectory,
    IReadOnlyList<RunManifestPart> Parts,
    IReadOnlyList<RunManifestArtifact> Artifacts,
    string Status);

/// <summary>Writes RunManifest.json with the SHA-256 of every artifact (paths relative to the output root).</summary>
public static class RunManifestWriter
{
    public const string FileName = "RunManifest.json";
    public const string Schema = "picomoldforge.parts-run-manifest.v1";

    /// <summary>Status of a manifest written before any part is generated; replaced by the final manifest.</summary>
    public const string IncompleteStatus = "INCOMPLETE";

    /// <param name="status">Overrides the computed status (PASS when every part passed, FAIL otherwise).</param>
    public static string Write(string outputRoot, string command, IReadOnlyList<RunManifestPart> parts, IEnumerable<string> artifactPaths, string? status = null)
    {
        var root = Path.GetFullPath(outputRoot);
        var artifacts = artifactPaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => new RunManifestArtifact(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                new FileInfo(path).Length,
                Sha256(path)))
            .ToArray();

        var manifest = new RunManifest(
            Schema,
            DateTimeOffset.UtcNow,
            "PicoMoldForge.Parts",
            typeof(RunManifestWriter).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            command,
            root,
            parts,
            artifacts,
            status ?? (parts.All(p => p.Status == "PASS") ? "PASS" : "FAIL"));

        var path = Path.Combine(root, FileName);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, ReportJson.Options), new UTF8Encoding(false));
        return path;
    }

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

/// <summary>Refuses output folders that would be dangerous to create or clean (same rules as the mold generator).</summary>
public static class OutputPathGuard
{
    public static string EnsureSafe(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException("Output directory cannot be empty.");
        }

        var resolved = Path.GetFullPath(outputDirectory);

        if (File.Exists(resolved))
        {
            throw new InvalidOperationException("Output directory path points to an existing file.");
        }

        var normalized = Normalize(resolved);
        var root = Path.GetPathRoot(resolved);

        void Refuse(string? candidate, string reason)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && string.Equals(normalized, Normalize(candidate), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing unsafe output directory: " + reason + ".");
            }
        }

        Refuse(root, "filesystem root is not allowed");
        Refuse(Directory.GetCurrentDirectory(), "current working directory is not allowed");
        Refuse(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "user profile root is not allowed");
        Refuse(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "desktop root is not allowed");
        Refuse(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "documents root is not allowed");
        Refuse(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Program Files root is not allowed");
        Refuse(Environment.SystemDirectory, "system directory is not allowed");
        Refuse(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Windows directory is not allowed");
        return resolved;
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
