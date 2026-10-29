using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Core.Tests;

internal sealed class SphereSdf : ISdf
{
    private readonly Vec3 _center;
    private readonly double _radius;

    public SphereSdf(Vec3 center, double radius)
    {
        _center = center;
        _radius = radius;
        Bounds = new Box3(center - new Vec3(radius, radius, radius), center + new Vec3(radius, radius, radius));
    }

    public Box3 Bounds { get; }

    public double Lipschitz => 1.0;

    public double Distance(Vec3 p) => (p - _center).Length - _radius;
}

internal static class RepositoryPaths
{
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PicoMoldForge.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root (PicoMoldForge.sln) not found above the test output folder.");
        }
    }

    public static string Sample(string name) => Path.Combine(Root, "samples", "parts", name + ".json");
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pmf-parts-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
