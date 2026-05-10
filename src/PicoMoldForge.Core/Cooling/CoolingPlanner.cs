namespace PicoMoldForge.Core.Cooling;

public sealed class CoolingPlanner
{
    public CoolingChannelPlan PlanStraightChannels(CoolingChannelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid cooling channel request: " + string.Join(" ", validationErrors),
                nameof(request));
        }

        var segments = new List<CoolingChannelSegment>();

        var usableYMin = request.MinimumClearanceMm;
        var usableYMax = request.PartSizeYmm - request.MinimumClearanceMm;

        if (usableYMax <= usableYMin)
        {
            throw new ArgumentException(
                "Invalid cooling channel request: PartSizeYmm is too small for the requested clearance.",
                nameof(request));
        }

        var usableZ = request.PartSizeZmm / 2.0m;

        for (var index = 0; index < request.ChannelCount; index++)
        {
            var y = CalculateChannelY(
                index,
                request.ChannelCount,
                usableYMin,
                usableYMax,
                request.ChannelSpacingMm);

            segments.Add(new CoolingChannelSegment(
                Id: $"cooling-channel-{index + 1:000}",
                StartXmm: request.MinimumClearanceMm,
                StartYmm: y,
                StartZmm: usableZ,
                EndXmm: request.PartSizeXmm - request.MinimumClearanceMm,
                EndYmm: y,
                EndZmm: usableZ,
                DiameterMm: request.ChannelDiameterMm,
                Description: "Preliminary straight cooling channel. Geometry is not generated in Phase 6A."));
        }

        var warnings = new[]
        {
            "Phase 6A creates deterministic cooling contracts only; no PicoGK cooling geometry is generated yet.",
            "Cooling channels are preliminary straight-line centerline segments.",
            "This plan does not evaluate thermal performance, pressure drop, manufacturability, drilling access, or conformal cooling feasibility."
        };

        return new CoolingChannelPlan(
            IsSuccessful: true,
            Segments: segments,
            Warnings: warnings);
    }

    private static decimal CalculateChannelY(
        int index,
        int channelCount,
        decimal usableYMin,
        decimal usableYMax,
        decimal spacing)
    {
        if (channelCount == 1)
        {
            return usableYMin + ((usableYMax - usableYMin) / 2.0m);
        }

        var centeredSpan = spacing * (channelCount - 1);
        var center = usableYMin + ((usableYMax - usableYMin) / 2.0m);
        var first = center - (centeredSpan / 2.0m);

        return first + (spacing * index);
    }
}