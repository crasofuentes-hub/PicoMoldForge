using System.Buffers.Binary;
using PicoMoldForge.PicoGK.Meshes;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoMeshServiceTests
{
    [Fact]
    public void LoadStlMetrics_WithBinaryTriangleStl_ReturnsMeshMetrics()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-picogk-mesh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var stlPath = Path.Combine(tempDir, "triangle-binary.stl");

        WriteBinaryTriangleStl(stlPath);

        try
        {
            var service = new PicoMeshService();

            var metrics = service.LoadStlMetrics(stlPath);

            Assert.Equal(Path.GetFullPath(stlPath), metrics.SourcePath);
            Assert.True(metrics.TriangleCount >= 1);
            Assert.True(metrics.VertexCount >= 3);
            Assert.False(string.IsNullOrWhiteSpace(metrics.BoundingBox));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteBinaryTriangleStl(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var header = new byte[80];
        var headerText = "PicoMoldForge binary STL test"u8.ToArray();
        Array.Copy(headerText, header, headerText.Length);
        writer.Write(header);

        writer.Write((uint)1);

        WriteFloat(writer, 0f);
        WriteFloat(writer, 0f);
        WriteFloat(writer, 1f);

        WriteFloat(writer, 0f);
        WriteFloat(writer, 0f);
        WriteFloat(writer, 0f);

        WriteFloat(writer, 10f);
        WriteFloat(writer, 0f);
        WriteFloat(writer, 0f);

        WriteFloat(writer, 0f);
        WriteFloat(writer, 10f);
        WriteFloat(writer, 0f);

        writer.Write((ushort)0);
    }

    private static void WriteFloat(BinaryWriter writer, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        writer.Write(buffer);
    }
}