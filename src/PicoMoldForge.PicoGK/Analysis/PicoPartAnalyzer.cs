using PicoMoldForge.Core.Analysis;
using PicoMoldForge.PicoGK.Meshes;
using PicoMoldForge.PicoGK.Parting;
using PicoMoldForge.PicoGK.Voxels;

namespace PicoMoldForge.PicoGK.Analysis;

public sealed class PicoPartAnalyzer
{
    private readonly PicoMeshService meshService;
    private readonly PicoVoxelService voxelService;
    private readonly PicoUndercutHeuristicAnalyzer undercutAnalyzer;
    private readonly PicoPartingPlaneAnalyzer partingPlaneAnalyzer;

    public PicoPartAnalyzer()
        : this(
            new PicoMeshService(),
            new PicoVoxelService(),
            new PicoUndercutHeuristicAnalyzer(),
            new PicoPartingPlaneAnalyzer())
    {
    }

    public PicoPartAnalyzer(
        PicoMeshService meshService,
        PicoVoxelService voxelService,
        PicoUndercutHeuristicAnalyzer undercutAnalyzer,
        PicoPartingPlaneAnalyzer partingPlaneAnalyzer)
    {
        this.meshService = meshService;
        this.voxelService = voxelService;
        this.undercutAnalyzer = undercutAnalyzer;
        this.partingPlaneAnalyzer = partingPlaneAnalyzer;
    }

    public PartAnalysisReport AnalyzeBinaryStl(string stlPath, float voxelSizeMm = 1.0f)
    {
        return AnalyzeBinaryStl(stlPath, OpeningDirection3.PositiveZ, voxelSizeMm);
    }

    public PartAnalysisReport AnalyzeBinaryStl(
        string stlPath,
        OpeningDirection3 openingDirection,
        float voxelSizeMm = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(stlPath))
        {
            throw new ArgumentException("STL path is required.", nameof(stlPath));
        }

        if (voxelSizeMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelSizeMm), "Voxel size must be greater than zero.");
        }

        var meshMetrics = meshService.LoadStlMetrics(stlPath);
        var voxelMetrics = voxelService.LoadStlAsVoxelMetrics(stlPath, voxelSizeMm);
        var undercutResult = undercutAnalyzer.AnalyzeBinaryStl(stlPath, openingDirection, voxelSizeMm);
        var partingPlane = partingPlaneAnalyzer.AnalyzeBinaryStl(stlPath, voxelSizeMm);

        var warnings = new List<PartAnalysisWarning>
        {
            new(
                "PRELIMINARY_ANALYSIS",
                "Info",
                "This report contains preliminary geometric metrics only. It does not certify manufacturability."),
            new(
                "BINARY_STL_REQUIRED",
                "Warning",
                "The current PicoGK adapter path requires binary STL input."),
            new(
                "PRELIMINARY_PARTING_PLANE",
                "Info",
                "The parting plane is preliminary and based on deterministic bounding-box analysis.")
        };

        if (undercutResult.HasPotentialUndercutRisk)
        {
            warnings.Add(new PartAnalysisWarning(
                "UNDERCUT_HEURISTIC_RISK",
                "Warning",
                $"Preliminary undercut heuristic found {undercutResult.OpposingNormalTriangleCount} opposing-normal triangles out of {undercutResult.TotalTriangleCount}. This is a risk indicator, not certified undercut detection."));
        }

        return new PartAnalysisReport(
            meshMetrics.SourcePath,
            meshMetrics.TriangleCount,
            meshMetrics.VertexCount,
            meshMetrics.BoundingBox,
            voxelMetrics.VoxelSizeMm,
            voxelMetrics.VolumeCubicMm,
            voxelMetrics.SliceCount,
            voxelMetrics.MemoryUsageBytes,
            voxelMetrics.BoundingBox,
            warnings,
            partingPlane);
    }
}