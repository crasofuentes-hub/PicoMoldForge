namespace PicoMoldForge.Generator;

public static class GeneratorOutputPathGuard
{
    public static void EnsureSafeForGeneration(GeneratorPipelineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        EnsureSafeOutputDirectory(
            input.ResolvedOutputDirectory,
            input.ConfigPath);
    }

    private static void EnsureSafeOutputDirectory(string outputDirectory, string configPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException("Output directory cannot be empty.");
        }

        var resolvedOutputDirectory = Path.GetFullPath(outputDirectory);

        if (File.Exists(resolvedOutputDirectory))
        {
            throw new InvalidOperationException("Output directory path points to an existing file.");
        }

        var normalizedOutputDirectory = NormalizeDirectory(resolvedOutputDirectory);
        var root = Path.GetPathRoot(resolvedOutputDirectory);

        if (!string.IsNullOrWhiteSpace(root) &&
            string.Equals(normalizedOutputDirectory, NormalizeDirectory(root), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing unsafe output directory: filesystem root is not allowed.");
        }

        var currentDirectory = NormalizeDirectory(Directory.GetCurrentDirectory());

        if (string.Equals(normalizedOutputDirectory, currentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing unsafe output directory: current working directory is not allowed.");
        }

        var resolvedConfigPath = Path.GetFullPath(configPath);
        var configDirectory = Path.GetDirectoryName(resolvedConfigPath);

        if (!string.IsNullOrWhiteSpace(configDirectory) &&
            string.Equals(normalizedOutputDirectory, NormalizeDirectory(configDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing unsafe output directory: config directory is not allowed.");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrWhiteSpace(userProfile) &&
            string.Equals(normalizedOutputDirectory, NormalizeDirectory(userProfile), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing unsafe output directory: user profile root is not allowed.");
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (!string.IsNullOrWhiteSpace(programFiles) &&
            string.Equals(normalizedOutputDirectory, NormalizeDirectory(programFiles), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing unsafe output directory: Program Files root is not allowed.");
        }

        var systemDirectory = Environment.SystemDirectory;

        if (!string.IsNullOrWhiteSpace(systemDirectory) &&
            string.Equals(normalizedOutputDirectory, NormalizeDirectory(systemDirectory), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing unsafe output directory: system directory is not allowed.");
        }
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}