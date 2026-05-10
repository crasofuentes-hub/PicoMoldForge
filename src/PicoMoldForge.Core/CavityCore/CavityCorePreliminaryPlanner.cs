namespace PicoMoldForge.Core.CavityCore;

public sealed class CavityCorePreliminaryPlanner
{
    private readonly ShrinkageCompensator shrinkageCompensator;

    public CavityCorePreliminaryPlanner()
        : this(new ShrinkageCompensator())
    {
    }

    public CavityCorePreliminaryPlanner(ShrinkageCompensator shrinkageCompensator)
    {
        this.shrinkageCompensator = shrinkageCompensator;
    }

    public CavityCoreGenerationResult Plan(CavityCoreGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid cavity/core generation request: " + string.Join(" ", validationErrors),
                nameof(request));
        }

        var shrinkage = shrinkageCompensator.CalculateUniformScale(request.ShrinkageRate);

        var cavityPath = Path.Combine(request.OutputDirectory, "Cavity.stl");
        var corePath = Path.Combine(request.OutputDirectory, "Core.stl");

        var artifacts = new[]
        {
            new CavityCoreArtifact(
                CavityCoreArtifactKind.Cavity,
                cavityPath,
                "Planned preliminary cavity STL output path. Geometry is not generated in Phase 5A."),
            new CavityCoreArtifact(
                CavityCoreArtifactKind.Core,
                corePath,
                "Planned preliminary core STL output path. Geometry is not generated in Phase 5A.")
        };

        var warnings = new[]
        {
            "Phase 5A creates deterministic cavity/core contracts only; no PicoGK cavity/core geometry is generated yet.",
            "Shrinkage compensation is represented as a uniform scale factor.",
            "Cavity/Core outputs are preliminary and not certified for production manufacturing."
        };

        return new CavityCoreGenerationResult(
            IsSuccessful: true,
            ShrinkageScaleFactor: shrinkage.ScaleFactor,
            Artifacts: artifacts,
            Warnings: warnings);
    }
}