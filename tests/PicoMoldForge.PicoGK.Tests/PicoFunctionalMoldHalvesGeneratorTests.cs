using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Parting;
using PicoMoldForge.PicoGK.BooleanGeometry;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoFunctionalMoldHalvesGeneratorTests
{
    [Fact]
    public void Generate_WithAChannelAGuidePinAndItsBore_CutsAndAddsTheirSteelAndKeepsTheHalvesApart()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"picomoldforge-functional-halves-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "cavity.stl");
        WriteBinaryBoxStl(sourcePath, 20f, 10f, 5f);

        try
        {
            // Box 0..20 x 0..10 x 0..5 in a 60 x 50 x 40 block split at Z = 0; the + half moves. A 6 mm channel through
            // the + half at Z = 12; an 8 mm guide pin on the + half entering 12 mm into a 9 mm bore of the - half.
            var features = new[]
            {
                new MoldHalfFeature("cooling-channel-001", "Cooling", 1, -21, 5, 12, 41, 5, 12, 3, RoundEnds: false, AddsSteel: false),
                new MoldHalfFeature("guide-bore-001", "GuideBores", -1, -10, -10, 1, -10, -10, -12.5, 4.5, RoundEnds: false, AddsSteel: false),
                new MoldHalfFeature("guide-pin-001", "GuidePins", 1, -10, -10, 1, -10, -10, -12, 4, RoundEnds: false, AddsSteel: true)
            };

            var result = new PicoFunctionalMoldHalvesGenerator().Generate(new FunctionalMoldHalvesRequest(
                CavitySourcePath: sourcePath,
                OutputDirectory: Path.Combine(tempDir, "output"),
                BlockBounds: new MoldBlockBounds(-20, -20, -20, 40, 30, 20),
                SplitAxis: PartingAxis.Z,
                PartingPlaneOffsetMm: 0,
                MovingHalf: 1,
                Features: features,
                VoxelSizeMm: 0.5f));

            Assert.True(File.Exists(result.Moving.OutputPath));
            Assert.True(File.Exists(result.Fixed.OutputPath));
            Assert.EndsWith(FunctionalMoldHalvesRequest.MovingHalfFileName, result.Moving.OutputPath, StringComparison.Ordinal);

            // The channel crosses the 60 mm block: pi x 9 x 60 mm3, within half a voxel of its surface.
            var channel = Assert.Single(result.Moving.Systems, s => s.System == "Cooling");
            Assert.InRange(channel.RemovedVolumeMm3, (Math.PI * 9 * 60) - (0.25 * 2 * Math.PI * 3 * 60), (Math.PI * 9 * 60) + (0.25 * 2 * Math.PI * 3 * 60));

            // The pin adds pi x 16 x 12 mm3 below the plane; the bore removes pi x 20.25 x 12.5 mm3.
            var pin = Assert.Single(result.Moving.Systems, s => s.System == "GuidePins");
            Assert.InRange(pin.AddedVolumeMm3, 0.8 * Math.PI * 16 * 12, 1.2 * Math.PI * 16 * 12);
            var bore = Assert.Single(result.Fixed.Systems, s => s.System == "GuideBores");
            Assert.InRange(bore.RemovedVolumeMm3, 0.8 * Math.PI * 20.25 * 12.5, 1.2 * Math.PI * 20.25 * 12.5);

            // The pin sits in its bore with 0.5 mm of play: the halves share (almost) no steel, and only the pin
            // reaches across the parting plane.
            Assert.True(result.OverlapVolumeMm3 < 0.05 * 0.5 * 60 * 50, $"overlap {result.OverlapVolumeMm3} mm3");
            Assert.InRange(result.Moving.MinAlongAxisMm, -12.5, -11.5);
            Assert.InRange(result.Fixed.MaxAlongAxisMm, -0.5, 0.5);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Generate_WithAFeatureOfNoRadius_Throws()
    {
        var request = new FunctionalMoldHalvesRequest(
            "cavity.stl",
            "output",
            new MoldBlockBounds(-20, -20, -20, 40, 30, 20),
            PartingAxis.Z,
            0,
            1,
            new[] { new MoldHalfFeature("bad", "Cooling", 1, 0, 0, 0, 1, 0, 0, 0, false, false) },
            1.0f);

        var error = Assert.Throws<ArgumentException>(() => new PicoFunctionalMoldHalvesGenerator().Generate(request));
        Assert.Contains("Feature bad must have a positive radius.", error.Message, StringComparison.Ordinal);
    }

    private static void WriteBinaryBoxStl(string path, float sizeX, float sizeY, float sizeZ)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]);
        writer.Write((uint)12);

        void Triangle(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz)
        {
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(0f);
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

        var (x, y, z) = (sizeX, sizeY, sizeZ);
        Triangle(0, 0, 0, x, y, 0, x, 0, 0);
        Triangle(0, 0, 0, 0, y, 0, x, y, 0);
        Triangle(0, 0, z, x, 0, z, x, y, z);
        Triangle(0, 0, z, x, y, z, 0, y, z);
        Triangle(0, 0, 0, x, 0, 0, x, 0, z);
        Triangle(0, 0, 0, x, 0, z, 0, 0, z);
        Triangle(0, y, 0, x, y, z, x, y, 0);
        Triangle(0, y, 0, 0, y, z, x, y, z);
        Triangle(0, 0, 0, 0, 0, z, 0, y, z);
        Triangle(0, 0, 0, 0, y, z, 0, y, 0);
        Triangle(x, 0, 0, x, y, 0, x, y, z);
        Triangle(x, 0, 0, x, y, z, x, 0, z);
    }
}
