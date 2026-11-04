using System.Globalization;
using System.Text.Json;
using PicoMoldForge.Core.Engineering.MoldLayout;
using PicoMoldForge.Core.Engineering.Shrinkage;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Generator;

/// <summary>Choices for a mold project made from a part STL.</summary>
public sealed record GeneratorPartMoldOptions(
    string? ResinName = "ABS",
    decimal? ResinShrinkageRate = null,
    decimal VoxelResolutionMm = 1.0m);

/// <summary>
/// Writes a generator project for one part STL (for example a design of PicoMoldForge Parts): split by geometry, the
/// cavity sized by the resin shrinkage, and a block with room for every feature the layout places. The margin around
/// the cavity is the largest of:
/// <list type="bullet">
/// <item>moldSystem.moldMarginMm (20 mm);</item>
/// <item>the feed beside the cavity: gate land + runner radius + sprue diameter + 10 mm of steel to the block face;</item>
/// <item>a cooling channel behind it: 10 mm to the cavity + 6 mm + 10 mm to the face;</item>
/// <item>a guide pin: 10 mm to the face + its bore + 3 mm to the cavity;</item>
/// </list>
/// rounded up to 5 mm. Cooling, ejector pins and the other sections take the values of the repository sample project.
/// </summary>
public static class GeneratorPartMoldProject
{
    public const string ProjectFileName = "project.json";

    public const string MoldDirectoryName = "mold";

    public static string Write(string partStlPath, string outputDirectory, GeneratorPartMoldOptions? options = null)
    {
        options ??= new GeneratorPartMoldOptions();

        if (string.IsNullOrWhiteSpace(partStlPath))
        {
            throw new ArgumentException("Part STL path is required.", nameof(partStlPath));
        }

        var part = Path.GetFullPath(partStlPath);

        if (!File.Exists(part))
        {
            throw new FileNotFoundException("Part STL file was not found.", part);
        }

        if (!BinaryStlProbe.IsBinaryStl(part))
        {
            throw new InvalidOperationException("The part STL must be a binary STL.");
        }

        if (!(options.VoxelResolutionMm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "VoxelResolutionMm must be greater than zero.");
        }

        var triangles = BinaryStlTriangleReader.Read(part);
        var bounds = MeshBounds.Of(triangles);
        var shrinkage = MoldShrinkageResolver.Resolve(options.ResinName, options.ResinShrinkageRate, 0.011m);
        var scale = 1 + (double)shrinkage.Rate;
        var wall = MeshWallThicknessAnalysis.Analyze(triangles, 1.2m, 0.5m);
        var maxWall = (double)wall.Analysis.Summary.MaximumObservedThicknessMm * scale;
        var layout = new MoldLayoutOptions();
        const double Edge = 10;
        const double CoolingDiameter = 6;
        const double CoolingClearance = 10;

        var runner = Math.Max(maxWall + layout.RunnerOverWallMm, layout.MinimumRunnerDiameterMm);
        var sprue = runner + layout.SprueOverRunnerMm;
        var feedRoom = layout.GateLandMm + (runner / 2) + sprue + Edge + 1;
        var coolingRoom = CoolingClearance + CoolingDiameter + layout.ResolutionMm + Edge + 1;
        var guideRoom = Edge + layout.GuidePinDiameterMm + (2 * Math.Max(0.5, (double)options.VoxelResolutionMm)) + layout.FeatureClearanceMm;
        var margin = Math.Ceiling(Math.Max(20, Math.Max(feedRoom, Math.Max(coolingRoom, guideRoom))) / 5) * 5;

        var center = bounds.Center;
        double Lo(double min, double c) => Math.Floor(c + ((min - c) * scale) - margin);
        double Hi(double max, double c) => Math.Ceiling(c + ((max - c) * scale) + margin);

        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, ProjectFileName);

        var size = new { X = Positive(bounds.SizeX), Y = Positive(bounds.SizeY), Z = Positive(bounds.SizeZ) };
        var project = new Dictionary<string, object?>
        {
            ["projectName"] = "Mold for " + Path.GetFileNameWithoutExtension(part),
            ["inputPath"] = part,
            ["outputDirectory"] = MoldDirectoryName,
            ["mode"] = "Prototype",
            ["standard"] = "Custom",
            ["voxelResolutionMm"] = options.VoxelResolutionMm,
            ["material"] = new { name = "H13 Tool Steel", shrinkageRate = 0.011m },
            ["resin"] = string.IsNullOrWhiteSpace(options.ResinName)
                ? null
                : new { name = options.ResinName, shrinkageRate = shrinkage.Rate },
            ["machine"] = new { name = "Generic LPBF Machine", buildVolumeXmm = 250, buildVolumeYmm = 250, buildVolumeZmm = 300 },
            ["moldBlock"] = new
            {
                minXmm = Lo(bounds.Min.X, center.X),
                minYmm = Lo(bounds.Min.Y, center.Y),
                minZmm = Lo(bounds.Min.Z, center.Z),
                maxXmm = Hi(bounds.Max.X, center.X),
                maxYmm = Hi(bounds.Max.Y, center.Y),
                maxZmm = Hi(bounds.Max.Z, center.Z)
            },
            ["parting"] = new { mode = "Geometry" },
            ["cooling"] = new
            {
                partSizeXmm = size.X,
                partSizeYmm = size.Y,
                partSizeZmm = size.Z,
                channelDiameterMm = CoolingDiameter,
                channelSpacingMm = 15,
                minimumClearanceMm = CoolingClearance,
                channelCount = 3
            },
            ["lattice"] = new
            {
                regionName = "part-bounds",
                minXmm = Round(bounds.Min.X),
                minYmm = Round(bounds.Min.Y),
                minZmm = Round(bounds.Min.Z),
                maxXmm = Round(bounds.Max.X),
                maxYmm = Round(bounds.Max.Y),
                maxZmm = Round(bounds.Max.Z),
                cellSizeMm = 10,
                beamRadiusMm = 1,
                targetRelativeDensity = 0.2
            },
            ["moldSystem"] = new
            {
                partSizeXmm = size.X,
                partSizeYmm = size.Y,
                partSizeZmm = size.Z,
                moldMarginMm = 20,
                ejectorPinDiameterMm = 4,
                ejectorPinCount = 4,
                ventWidthMm = 0.5,
                ventDepthMm = 0.1,
                insertClearanceMm = 2
            },
            ["dfam"] = new
            {
                minimumWallThicknessMm = 1.5,
                recommendedMinimumWallThicknessMm = 1.2,
                usesPreliminaryGeometry = true
            }
        };

        if (project["resin"] is null)
        {
            project.Remove("resin");
        }

        var json = JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(projectPath, json);
        return projectPath;

        static double Positive(double value) => Math.Max(Math.Round(value, 3), 0.001);

        static double Round(double value) => Math.Round(value, 3);
    }

    /// <summary>A one-line description of the project written, for the console.</summary>
    public static string Describe(string projectPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(projectPath));
        var root = document.RootElement;
        var block = root.GetProperty("moldBlock");
        var resin = root.TryGetProperty("resin", out var r) ? r.GetProperty("name").GetString() + " " + r.GetProperty("shrinkageRate").GetDecimal().ToString("0.####", CultureInfo.InvariantCulture) : "none";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"block X {block.GetProperty("minXmm").GetDouble()}..{block.GetProperty("maxXmm").GetDouble()}, Y {block.GetProperty("minYmm").GetDouble()}..{block.GetProperty("maxYmm").GetDouble()}, Z {block.GetProperty("minZmm").GetDouble()}..{block.GetProperty("maxZmm").GetDouble()} mm; parting by geometry; resin {resin}; voxel {root.GetProperty("voxelResolutionMm").GetDecimal()} mm");
    }
}
