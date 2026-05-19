using PicoMoldForge.Core.Engineering.GateSystem;

namespace PicoMoldForge.Generator;

public static class GeneratorGateRunnerSpruePlanFactory
{
    public static GateRunnerSprueGenerationInput CreateInput(GeneratorPipelineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var bounds = new GateSystemBounds(
            input.BooleanMoldBlockBounds.MinXmm,
            input.BooleanMoldBlockBounds.MinYmm,
            input.BooleanMoldBlockBounds.MinZmm,
            input.BooleanMoldBlockBounds.MaxXmm,
            input.BooleanMoldBlockBounds.MaxYmm,
            input.BooleanMoldBlockBounds.MaxZmm);

        return new GateRunnerSprueGenerationInput(
            MoldBounds: bounds,
            Segments: CreateSegments(input, bounds),
            RequiredCavityClearanceMm: input.Cooling.MinimumClearanceMm,
            RequiredMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm);
    }

    public static GateRunnerSprueGenerationResult Plan(GateRunnerSprueGenerationInput input)
    {
        var generator = new GateRunnerSprueGenerator();

        return generator.Plan(input);
    }

    private static IReadOnlyList<GateRunnerSprueSegment> CreateSegments(
        GeneratorPipelineInput input,
        GateSystemBounds bounds)
    {
        var centerX = Midpoint(bounds.MinXmm, bounds.MaxXmm);
        var centerY = Midpoint(bounds.MinYmm, bounds.MaxYmm);
        var centerZ = Midpoint(bounds.MinZmm, bounds.MaxZmm);

        var runnerHalfLength = Math.Max(1m, Math.Min(input.MoldSystem.PartSizeXmm / 2m, bounds.SizeXmm / 4m));
        var gateLength = Math.Max(1m, Math.Min(input.MoldSystem.PartSizeYmm / 4m, bounds.SizeYmm / 4m));

        var feedZ = centerZ;
        var topZ = bounds.MaxZmm - Math.Max(1m, input.Cooling.MinimumClearanceMm);

        if (topZ <= feedZ)
        {
            topZ = centerZ + Math.Max(1m, bounds.SizeZmm / 4m);
        }

        if (topZ >= bounds.MaxZmm)
        {
            topZ = bounds.MaxZmm - 0.5m;
        }

        var sprueDiameter = Math.Max(0.5m, input.Cooling.ChannelDiameterMm);
        var runnerDiameter = Math.Max(0.5m, input.Cooling.ChannelDiameterMm * 0.75m);
        var gateDiameter = Math.Max(0.25m, input.Cooling.ChannelDiameterMm * 0.40m);

        return new[]
        {
            new GateRunnerSprueSegment(
                FeatureId: "generator-sprue-1",
                FeatureType: GateSystemFeatureType.Sprue,
                Start: new GateSystemPoint(centerX, centerY, topZ),
                End: new GateSystemPoint(centerX, centerY, feedZ),
                HydraulicDiameterMm: sprueDiameter,
                FlowAreaMm2: CircleArea(sprueDiameter),
                MinimumCavityClearanceMm: input.Cooling.MinimumClearanceMm,
                MinimumMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm,
                IsCriticalToQuality: true),

            new GateRunnerSprueSegment(
                FeatureId: "generator-runner-1",
                FeatureType: GateSystemFeatureType.Runner,
                Start: new GateSystemPoint(centerX - runnerHalfLength, centerY, feedZ),
                End: new GateSystemPoint(centerX + runnerHalfLength, centerY, feedZ),
                HydraulicDiameterMm: runnerDiameter,
                FlowAreaMm2: CircleArea(runnerDiameter),
                MinimumCavityClearanceMm: input.Cooling.MinimumClearanceMm,
                MinimumMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm),

            new GateRunnerSprueSegment(
                FeatureId: "generator-gate-1",
                FeatureType: GateSystemFeatureType.Gate,
                Start: new GateSystemPoint(centerX + runnerHalfLength, centerY, feedZ),
                End: new GateSystemPoint(centerX + runnerHalfLength, centerY - gateLength, feedZ),
                HydraulicDiameterMm: gateDiameter,
                FlowAreaMm2: CircleArea(gateDiameter),
                MinimumCavityClearanceMm: input.Cooling.MinimumClearanceMm,
                MinimumMoldEdgeClearanceMm: input.Cooling.MinimumClearanceMm,
                IsCriticalToQuality: true)
        };
    }

    private static decimal CircleArea(decimal diameterMm)
    {
        var radius = Math.Max(0m, diameterMm) / 2m;

        return 3.1415926535897932384626433833m * radius * radius;
    }

    private static decimal Midpoint(decimal minimum, decimal maximum)
    {
        return minimum + ((maximum - minimum) / 2m);
    }
}