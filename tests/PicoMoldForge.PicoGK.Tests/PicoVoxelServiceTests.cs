using PicoMoldForge.PicoGK.Voxels;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoVoxelServiceTests
{
    [Fact]
    public void LoadStlAsVoxelMetrics_WithBinaryCubeStl_ReturnsVoxelMetrics()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-picogk-voxels-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "cube-binary.stl");

        WriteBinaryCubeStl(stlPath);

        try
        {
            var service = new PicoVoxelService();

            var metrics = service.LoadStlAsVoxelMetrics(stlPath, voxelSizeMm: 1.0f);

            Assert.Equal(Path.GetFullPath(stlPath), metrics.SourcePath);
            Assert.Equal(1.0f, metrics.VoxelSizeMm);
            Assert.True(metrics.VolumeCubicMm > 0);
            Assert.True(metrics.SliceCount > 0);
            Assert.True(metrics.MemoryUsageBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(metrics.BoundingBox));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteBinaryCubeStl(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge binary cube STL test"u8.ToArray();
        Array.Copy(headerText, header, headerText.Length);
        writer.Write(header);

        writer.Write((uint)12);

        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, 10, 10, 0, 10, 0, 0);
        WriteTriangle(writer, 0, 0, -1, 0, 0, 0, 0, 10, 0, 10, 10, 0);

        WriteTriangle(writer, 0, 0, 1, 0, 0, 10, 10, 0, 10, 10, 10, 10);
        WriteTriangle(writer, 0, 0, 1, 0, 0, 10, 10, 10, 10, 0, 10, 10);

        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, 10, 0, 0, 10, 0, 10);
        WriteTriangle(writer, 0, -1, 0, 0, 0, 0, 10, 0, 10, 0, 0, 10);

        WriteTriangle(writer, 0, 1, 0, 0, 10, 0, 10, 10, 10, 10, 10, 0);
        WriteTriangle(writer, 0, 1, 0, 0, 10, 0, 0, 10, 10, 10, 10, 10);

        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, 0, 10, 0, 10, 10);
        WriteTriangle(writer, -1, 0, 0, 0, 0, 0, 0, 10, 10, 0, 10, 0);

        WriteTriangle(writer, 1, 0, 0, 10, 0, 0, 10, 10, 0, 10, 10, 10);
        WriteTriangle(writer, 1, 0, 0, 10, 0, 0, 10, 10, 10, 10, 0, 10);
    }

    private static void WriteTriangle(
        BinaryWriter writer,
        float nx,
        float ny,
        float nz,
        float ax,
        float ay,
        float az,
        float bx,
        float by,
        float bz,
        float cx,
        float cy,
        float cz)
    {
        writer.Write(nx);
        writer.Write(ny);
        writer.Write(nz);

        writer.Write(ax);
        writer.Write(ay);
        writer.Write(az);

        writer.Write(bx);
        writer.Write(by);
        writer.Write(bz);

        writer.Write(cx);
        writer.Write(cy);
        writer.Write(cz);

        writer.Write((ushort)0);
    }
}