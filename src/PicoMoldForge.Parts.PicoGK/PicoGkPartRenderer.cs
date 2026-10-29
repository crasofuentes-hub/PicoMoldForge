using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using PicoGK;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.PicoGK;

/// <summary>Exposes a PicoMoldForge Parts distance field to PicoGK (thread-safe, immutable).</summary>
public sealed class SdfImplicit : IImplicit
{
    private readonly ISdf _sdf;

    public SdfImplicit(ISdf sdf)
    {
        _sdf = sdf ?? throw new ArgumentNullException(nameof(sdf));
    }

    public float fSignedDistance(in Vector3 vec) => (float)_sdf.Distance(new Vec3(vec.X, vec.Y, vec.Z));
}

/// <summary>Whether the PicoGK native runtime can be loaded on this machine (it ships for win-x64 and osx-arm64 only).</summary>
public static class PicoGkRuntimeAvailability
{
    private static readonly Lazy<(bool Available, string Reason)> Probe = new(Detect);

    public static bool IsAvailable => Probe.Value.Available;

    public static string Reason => Probe.Value.Reason;

    private static (bool, string) Detect()
    {
        var supported =
            (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64) ||
            (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64);

        if (!supported)
        {
            return (false, $"the PicoGK native runtime ({Config.strPicoGKLib}) only ships for Windows x64 and macOS arm64 (this is {RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}).");
        }

        if (NativeLibrary.TryLoad(Config.strPicoGKLib, typeof(Library).Assembly, DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory, out _))
        {
            return (true, "PicoGK native runtime loaded.");
        }

        return (false, $"The PicoGK native runtime '{Config.strPicoGKLib}' was not found next to the application.");
    }
}

public sealed record PicoGkRenderResult(
    string StlPath,
    double VoxelSizeMm,
    double VolumeMm3,
    Vec3 BoundsMin,
    Vec3 BoundsMax,
    int Triangles,
    int Vertices,
    double Seconds);

/// <summary>
/// Renders a part with PicoGK in a single headless Library session: distance field to voxels
/// (Voxels(Library, IImplicit, BBox3)), voxels to mesh, mesh to binary STL in millimeters.
/// </summary>
public static class PicoGkPartRenderer
{
    public static PicoGkRenderResult Render(PartDefinition part, double voxelSizeMm, string stlPath)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (!(voxelSizeMm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(voxelSizeMm), "Voxel size must be positive.");
        }

        if (!PicoGkRuntimeAvailability.IsAvailable)
        {
            throw new PlatformNotSupportedException(PicoGkRuntimeAvailability.Reason);
        }

        var watch = Stopwatch.StartNew();
        var bounds = part.Sdf.Bounds.Expand(3 * voxelSizeMm);
        var box = new BBox3(
            (float)bounds.Min.X, (float)bounds.Min.Y, (float)bounds.Min.Z,
            (float)bounds.Max.X, (float)bounds.Max.Y, (float)bounds.Max.Z);

        using var library = new Library((float)voxelSizeMm);
        using var voxels = new Voxels(library, new SdfImplicit(part.Sdf), box);
        voxels.CalculateProperties(out var volume, out var voxelBounds);
        using var mesh = voxels.mshAsMesh();
        mesh.SaveToStlFile(stlPath, Mesh.EStlUnit.MM);

        return new PicoGkRenderResult(
            Path.GetFullPath(stlPath),
            voxelSizeMm,
            volume,
            new Vec3(voxelBounds.vecMin.X, voxelBounds.vecMin.Y, voxelBounds.vecMin.Z),
            new Vec3(voxelBounds.vecMax.X, voxelBounds.vecMax.Y, voxelBounds.vecMax.Z),
            mesh.nTriangleCount(),
            mesh.nVertexCount(),
            watch.Elapsed.TotalSeconds);
    }

    /// <summary>Voxel size that keeps the dense evaluation box under ~8 million voxels (and never finer than 2x the mesh resolution).</summary>
    public static double SuggestVoxelSize(PartDefinition part)
    {
        ArgumentNullException.ThrowIfNull(part);

        var size = part.Sdf.Bounds.Size;
        var volume = size.X * size.Y * size.Z;
        var byBudget = Math.Cbrt(volume / 8_000_000.0);
        return Math.Round(Math.Max(Math.Max(2 * part.Mesh.ResolutionMm, byBudget), 0.1), 3);
    }
}

/// <summary>Opens the PicoGK viewer window with the part (blocks until the window is closed).</summary>
public static class PicoGkViewer
{
    public static void Show(PartDefinition part, double voxelSizeMm, string logPath)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (!PicoGkRuntimeAvailability.IsAvailable)
        {
            throw new PlatformNotSupportedException(PicoGkRuntimeAvailability.Reason);
        }

        var bounds = part.Sdf.Bounds.Expand(3 * voxelSizeMm);
        var box = new BBox3(
            (float)bounds.Min.X, (float)bounds.Min.Y, (float)bounds.Min.Z,
            (float)bounds.Max.X, (float)bounds.Max.Y, (float)bounds.Max.Z);

        Library.Go(
            (float)voxelSizeMm,
            () =>
            {
                var voxels = new Voxels(Library.oLibrary(), new SdfImplicit(part.Sdf), box);
                Library.oViewer().SetGroupMaterial(0, "E8A33D", 0.35f, 0.55f);
                Library.oViewer().Add(voxels, 0);
                Library.Log($"PicoMoldForge Parts: {part.Title} ({part.Id}) rendered at {voxelSizeMm} mm voxels. Close the window to finish.");
            },
            logPath,
            false,
            "PicoMoldForge Parts - " + part.Title);
    }
}
