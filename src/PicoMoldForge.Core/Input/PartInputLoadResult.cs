namespace PicoMoldForge.Core.Input;

public sealed record PartInputLoadResult(
    bool IsSuccessful,
    PartInputFormat Format,
    string InputPath,
    string? ResolvedPath,
    string? Error)
{
    public static PartInputLoadResult Success(PartInputFormat format, string inputPath, string resolvedPath)
    {
        return new PartInputLoadResult(true, format, inputPath, resolvedPath, null);
    }

    public static PartInputLoadResult Failure(PartInputFormat format, string inputPath, string error)
    {
        return new PartInputLoadResult(false, format, inputPath, null, error);
    }
}