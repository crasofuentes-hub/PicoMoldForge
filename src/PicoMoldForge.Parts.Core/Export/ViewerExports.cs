using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Sdf;

namespace PicoMoldForge.Parts.Export;

/// <summary>
/// Data the viewer needs to replay the "modo malla" construction: every feature's profile outline and its
/// trajectory (exported from the same feature definitions the solid is built from).
/// </summary>
public static class FeatureExporter
{
    public const string Schema = "picomoldforge.parts-features.v1";

    public static void Write(PartDefinition part, string path)
    {
        ArgumentNullException.ThrowIfNull(part);

        var bounds = part.Sdf.Bounds;
        var document = new FeatureDocument(
            Schema,
            part.Id,
            part.Title,
            part.Template,
            AxisConvention.Name,
            AxisConvention.OrientationOrder,
            new BoundsDto(ToArray(bounds.Min), ToArray(bounds.Max)),
            part.Parameters.Select(p => new ParameterDto(p.Key, p.Value)).ToArray(),
            part.Features.Select((feature, index) => ToDto(feature, index)).ToArray());

        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions.Indented), new UTF8Encoding(false));
    }

    private static FeatureDto ToDto(FeatureDefinition feature, int index)
    {
        var profileBounds = feature.Profile.Bounds;
        var size = Math.Max(profileBounds.Size.X, profileBounds.Size.Y);
        var step = Math.Max(size / 500.0, 0.01);
        var outlines = ProfileOutline.Trace(feature.Profile, step, step / 4);
        SweepDto sweep;
        AxisFrame frame;

        switch (feature.Sweep)
        {
            case LinearSweepDefinition linear:
                frame = AxisConvention.Frame(linear.Axis);
                sweep = new SweepDto("linear", linear.Axis.ToString(), linear.From, linear.To, linear.TwistDeg, null, null);
                break;

            case RotarySweepDefinition rotary:
                frame = AxisConvention.Frame(AxisConvention.AxisOf(rotary.Axis));
                sweep = new SweepDto("rotary", rotary.Axis.ToString(), null, null, null, rotary.StartDeg, rotary.EndDeg);
                outlines = ClipToNonNegativeRadius(outlines);
                break;

            default:
                throw new InvalidOperationException("Unknown sweep type.");
        }

        return new FeatureDto(
            index,
            feature.Id,
            feature.Label,
            feature.Operation.ToString().ToLowerInvariant(),
            sweep,
            new FrameDto(ToArray(frame.U), ToArray(frame.V), ToArray(frame.W)),
            feature.Instances.Select(i => new InstanceDto(
                new[]
                {
                    Round(i.Rotation.M00), Round(i.Rotation.M01), Round(i.Rotation.M02),
                    Round(i.Rotation.M10), Round(i.Rotation.M11), Round(i.Rotation.M12),
                    Round(i.Rotation.M20), Round(i.Rotation.M21), Round(i.Rotation.M22)
                },
                ToArray(i.Translation))).ToArray(),
            outlines.Select(loop => loop.Select(p => new[] { Round(p.X), Round(p.Y) }).ToArray()).ToArray());
    }

    /// <summary>Rotary profiles are full sections; the viewer only needs the half with radius >= 0.</summary>
    private static IReadOnlyList<IReadOnlyList<Vec2>> ClipToNonNegativeRadius(IReadOnlyList<IReadOnlyList<Vec2>> loops)
    {
        var result = new List<IReadOnlyList<Vec2>>();

        foreach (var loop in loops)
        {
            if (loop.All(p => p.X >= -1e-9))
            {
                result.Add(loop.Concat(new[] { loop[0] }).ToList());
                continue;
            }

            var count = loop.Count;
            var startIndex = Enumerable.Range(0, count).FirstOrDefault(i => loop[i].X < 0, 0);
            var current = new List<Vec2>();

            for (var k = 1; k <= count; k++)
            {
                var a = loop[(startIndex + k - 1) % count];
                var b = loop[(startIndex + k) % count];

                if (a.X >= 0 && b.X >= 0)
                {
                    current.Add(b);
                }
                else if (a.X < 0 && b.X >= 0)
                {
                    current = new List<Vec2> { Lerp(a, b), b };
                }
                else if (a.X >= 0 && b.X < 0)
                {
                    current.Add(Lerp(a, b));

                    if (current.Count >= 2)
                    {
                        result.Add(current);
                    }

                    current = new List<Vec2>();
                }
            }

            if (current.Count >= 2)
            {
                result.Add(current);
            }
        }

        return result;

        static Vec2 Lerp(Vec2 a, Vec2 b)
        {
            var t = a.X / (a.X - b.X);
            return new Vec2(0, a.Y + ((b.Y - a.Y) * t));
        }
    }

    internal static double[] ToArray(Vec3 v) => new[] { Round(v.X), Round(v.Y), Round(v.Z) };

    internal static double Round(double value) => Math.Round(value, 4);

    private sealed record FeatureDocument(
        string Schema,
        string Id,
        string Title,
        string Template,
        string AxisConvention,
        string OrientationOrder,
        BoundsDto Bounds,
        ParameterDto[] Parameters,
        FeatureDto[] Features);

    private sealed record BoundsDto(double[] Min, double[] Max);

    private sealed record ParameterDto(string Name, string Value);

    private sealed record FeatureDto(
        int Index,
        string Id,
        string Label,
        string Op,
        SweepDto Sweep,
        FrameDto Frame,
        InstanceDto[] Instances,
        double[][][] Outlines);

    private sealed record SweepDto(
        string Kind,
        string Axis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? From,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? To,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? TwistDeg,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? StartDeg,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? EndDeg);

    private sealed record FrameDto(double[] U, double[] V, double[] W);

    private sealed record InstanceDto(double[] R, double[] T);
}

/// <summary>
/// Display lattice ("la malla" of the video) with, for every feature, the sweep progress at which each lattice
/// point is reached by the moving profile, plus the final inside/outside state of each point. Replaying the
/// features in order with add/subtract/intersect on these points reproduces the final state exactly.
/// Format "PMFLAT01": uint32 version, uint32 nx, ny, nz, uint32 featureCount, float32 origin x, y, z,
/// float32 step, then featureCount blocks of nx*ny*nz bytes (255 = not reached, 0..254 = progress), then
/// nx*ny*nz bytes final state (1 = material). Point index = i + nx * (j + ny * k).
/// </summary>
public static class LatticeExporter
{
    public const string Magic = "PMFLAT01";
    private static readonly double[] NiceSteps = { 0.25, 0.5, 1, 2, 2.5, 5, 10, 20, 25, 50, 100 };

    public static LatticeInfo Write(PartDefinition part, string path, int targetPointsAlongLongestAxis = 24)
    {
        ArgumentNullException.ThrowIfNull(part);

        var bounds = part.Sdf.Bounds;
        var longest = Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z));
        var step = NiceSteps.FirstOrDefault(s => longest / s <= targetPointsAlongLongestAxis, NiceSteps[^1]);
        // A tiny irrational offset keeps lattice points off exact feature surfaces (distance exactly 0), where the
        // boolean replay and the distance-field sign could otherwise disagree on a measure-zero set.
        var jitter = new Vec3(0.70710678, 0.57735027, 0.44721360) * (step * 1e-4);
        var origin = new Vec3(
            Math.Floor(bounds.Min.X / step) * step,
            Math.Floor(bounds.Min.Y / step) * step,
            Math.Floor(bounds.Min.Z / step) * step) + jitter;
        var nx = (int)Math.Floor((bounds.Max.X - origin.X) / step) + 1;
        var ny = (int)Math.Floor((bounds.Max.Y - origin.Y) / step) + 1;
        var nz = (int)Math.Floor((bounds.Max.Z - origin.Z) / step) + 1;
        var count = nx * ny * nz;
        var features = part.Features;
        var progress = new byte[features.Count][];

        for (var f = 0; f < features.Count; f++)
        {
            var feature = features[f];
            var local = feature.Sweep.BuildSdf(feature.Profile);
            var data = new byte[count];
            progress[f] = data;

            Parallel.For(0, count, index =>
            {
                var p = Point(origin, step, nx, ny, index);
                var best = double.PositiveInfinity;

                foreach (var instance in feature.Instances)
                {
                    var q = instance.ApplyInverse(p);

                    if (local.Distance(q) < 0)
                    {
                        best = Math.Min(best, Progress(feature.Sweep, q));
                    }
                }

                data[index] = double.IsPositiveInfinity(best) ? (byte)255 : (byte)Math.Clamp(Math.Round(best * 254), 0, 254);
            });
        }

        var final = new byte[count];
        var sdf = part.Sdf;
        Parallel.For(0, count, index => final[index] = sdf.Distance(Point(origin, step, nx, ny, index)) < 0 ? (byte)1 : (byte)0);

        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream, Encoding.ASCII))
        {
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            writer.Write(1u);
            writer.Write((uint)nx);
            writer.Write((uint)ny);
            writer.Write((uint)nz);
            writer.Write((uint)features.Count);
            writer.Write((float)origin.X);
            writer.Write((float)origin.Y);
            writer.Write((float)origin.Z);
            writer.Write((float)step);

            foreach (var data in progress)
            {
                writer.Write(data);
            }

            writer.Write(final);
        }

        var replay = Replay(part.Features, progress, count);
        var consistent = replay.AsSpan().SequenceEqual(final);
        return new LatticeInfo(nx, ny, nz, step, final.Count(b => b == 1), consistent);
    }

    /// <summary>Boolean replay of the features on the lattice (what the viewer animation does).</summary>
    internal static byte[] Replay(IReadOnlyList<FeatureDefinition> features, byte[][] progress, int count)
    {
        var state = new byte[count];

        for (var f = 0; f < features.Count; f++)
        {
            for (var i = 0; i < count; i++)
            {
                var inside = progress[f][i] != 255;

                state[i] = features[f].Operation switch
                {
                    FeatureOperation.Add => inside ? (byte)1 : state[i],
                    FeatureOperation.Subtract => inside ? (byte)0 : state[i],
                    _ => inside ? state[i] : (byte)0
                };
            }
        }

        return state;
    }

    private static Vec3 Point(Vec3 origin, double step, int nx, int ny, int index)
    {
        var i = index % nx;
        var j = (index / nx) % ny;
        var k = index / (nx * ny);
        return origin + new Vec3(i * step, j * step, k * step);
    }

    private static double Progress(SweepDefinition sweep, Vec3 local)
    {
        switch (sweep)
        {
            case LinearSweepDefinition linear:
            {
                var frame = AxisConvention.Frame(linear.Axis);
                var w = Vec3.Dot(local, frame.W);
                return Math.Clamp((w - linear.From) / (linear.To - linear.From), 0, 1);
            }

            case RotarySweepDefinition rotary:
            {
                var frame = AxisConvention.Frame(AxisConvention.AxisOf(rotary.Axis));
                var angle = AxisConvention.RadiansToDegrees(Math.Atan2(Vec3.Dot(local, frame.V), Vec3.Dot(local, frame.U)));
                var start = Math.Min(rotary.StartDeg, rotary.EndDeg);
                var span = Math.Abs(rotary.EndDeg - rotary.StartDeg);
                var relative = angle - start;
                relative -= 360 * Math.Floor(relative / 360);
                return Math.Clamp(relative / span, 0, 1);
            }

            default:
                return 0;
        }
    }
}

public sealed record LatticeInfo(int Nx, int Ny, int Nz, double StepMm, int PointsInside, bool ReplayMatchesSolid);

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
