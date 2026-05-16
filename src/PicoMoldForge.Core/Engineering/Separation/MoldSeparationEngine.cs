namespace PicoMoldForge.Core.Engineering.Separation;

public sealed class MoldSeparationEngine
{
    public MoldSeparationEngineResult Split(MoldSeparationEngineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateInput(input);

        var moldLength = GetAxisLength(input.MoldBlockBounds, input.PartingAxis);
        var moldMinimum = GetAxisMinimum(input.MoldBlockBounds, input.PartingAxis);
        var moldMaximum = GetAxisMaximum(input.MoldBlockBounds, input.PartingAxis);
        var crossSectionArea = input.MoldBlockBounds.VolumeMm3 / moldLength;

        var coreLength = Clamp(input.PartingOffsetMm - moldMinimum, 0m, moldLength);
        var cavityLength = Clamp(moldMaximum - input.PartingOffsetMm, 0m, moldLength);

        var coreVolume = crossSectionArea * coreLength;
        var cavityVolume = crossSectionArea * cavityLength;

        var coreVoxelCount = EstimateVoxelCount(coreVolume, input.VoxelResolutionMm);
        var cavityVoxelCount = EstimateVoxelCount(cavityVolume, input.VoxelResolutionMm);

        var validationInput = new CoreCavitySeparationInput(
            PartingAxis: input.PartingAxis,
            PartingOffsetMm: input.PartingOffsetMm,
            CoreVoxelCount: coreVoxelCount,
            CavityVoxelCount: cavityVoxelCount,
            OverlapVoxelCount: 0,
            GapVoxelCount: 0,
            HasCoreSideArtifact: coreVoxelCount > 0,
            HasCavitySideArtifact: cavityVoxelCount > 0,
            HasPartingMetadata: input.HasPartingMetadata,
            HasShutoffStrategy: input.HasShutoffStrategy,
            HasEngineerOverride: input.HasEngineerOverride);

        var validator = new CoreCavitySeparationValidator();
        var validation = validator.Validate(validationInput);
        var summary = validator.Summarize(validationInput);

        return new MoldSeparationEngineResult(
            PartingAxis: input.PartingAxis,
            PartingOffsetMm: input.PartingOffsetMm,
            CoreVoxelCount: coreVoxelCount,
            CavityVoxelCount: cavityVoxelCount,
            OverlapVoxelCount: 0,
            GapVoxelCount: 0,
            CoreApproxVolumeMm3: coreVolume,
            CavityApproxVolumeMm3: cavityVolume,
            Summary: summary,
            ValidationResult: validation);
    }

    private static void ValidateInput(MoldSeparationEngineInput input)
    {
        ValidateBounds(input.MoldBlockBounds, nameof(input.MoldBlockBounds));
        ValidateBounds(input.PartBounds, nameof(input.PartBounds));

        if (input.VoxelResolutionMm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.VoxelResolutionMm), "Voxel resolution must be greater than zero.");
        }

        if (!Contains(input.MoldBlockBounds, input.PartBounds))
        {
            throw new InvalidOperationException("Part bounds must be inside mold block bounds for preliminary separation.");
        }
    }

    private static void ValidateBounds(MoldSeparationBounds bounds, string name)
    {
        if (bounds.SizeXmm <= 0m ||
            bounds.SizeYmm <= 0m ||
            bounds.SizeZmm <= 0m)
        {
            throw new ArgumentOutOfRangeException(name, "Bounds must have positive X, Y, and Z sizes.");
        }
    }

    private static bool Contains(MoldSeparationBounds outer, MoldSeparationBounds inner)
    {
        return inner.MinXmm >= outer.MinXmm &&
            inner.MinYmm >= outer.MinYmm &&
            inner.MinZmm >= outer.MinZmm &&
            inner.MaxXmm <= outer.MaxXmm &&
            inner.MaxYmm <= outer.MaxYmm &&
            inner.MaxZmm <= outer.MaxZmm;
    }

    private static long EstimateVoxelCount(decimal volumeMm3, decimal voxelResolutionMm)
    {
        if (volumeMm3 <= 0m)
        {
            return 0;
        }

        var voxelVolume = voxelResolutionMm * voxelResolutionMm * voxelResolutionMm;

        return (long)Math.Ceiling(volumeMm3 / voxelVolume);
    }

    private static decimal GetAxisLength(MoldSeparationBounds bounds, PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => bounds.SizeXmm,
            PartingAxis.Y => bounds.SizeYmm,
            PartingAxis.Z => bounds.SizeZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static decimal GetAxisMinimum(MoldSeparationBounds bounds, PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => bounds.MinXmm,
            PartingAxis.Y => bounds.MinYmm,
            PartingAxis.Z => bounds.MinZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static decimal GetAxisMaximum(MoldSeparationBounds bounds, PartingAxis axis)
    {
        return axis switch
        {
            PartingAxis.X => bounds.MaxXmm,
            PartingAxis.Y => bounds.MaxYmm,
            PartingAxis.Z => bounds.MaxZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };
    }

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        if (value > maximum)
        {
            return maximum;
        }

        return value;
    }
}