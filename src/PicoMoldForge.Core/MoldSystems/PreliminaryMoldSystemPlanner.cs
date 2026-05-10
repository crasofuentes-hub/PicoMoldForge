namespace PicoMoldForge.Core.MoldSystems;

public sealed class PreliminaryMoldSystemPlanner
{
    public MoldSystemPlan Plan(MoldSystemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid mold system request: " + string.Join(" ", validationErrors),
                nameof(request));
        }

        var moldBase = new MoldBaseEnvelope(
            WidthMm: request.PartSizeXmm + (request.MoldMarginMm * 2.0m),
            DepthMm: request.PartSizeYmm + (request.MoldMarginMm * 2.0m),
            HeightMm: request.PartSizeZmm + (request.MoldMarginMm * 2.0m),
            MarginMm: request.MoldMarginMm);

        var ejectors = PlanEjectors(request, moldBase);
        var vents = PlanVents(request, moldBase);
        var inserts = PlanInserts(request, moldBase);

        var warnings = new[]
        {
            "Phase 8A creates deterministic mold-system contracts only; no PicoGK mold-system geometry is generated yet.",
            "Ejector, vent, insert, and mold-base outputs are preliminary planning data.",
            "This plan does not evaluate collisions, draft, shutoffs, side actions, machining access, or production manufacturability."
        };

        return new MoldSystemPlan(
            IsSuccessful: true,
            MoldBase: moldBase,
            Ejectors: ejectors,
            Vents: vents,
            Inserts: inserts,
            Warnings: warnings);
    }

    private static EjectorPinPlan PlanEjectors(MoldSystemRequest request, MoldBaseEnvelope moldBase)
    {
        var pins = new List<EjectorPin>();

        var minX = moldBase.MarginMm;
        var maxX = moldBase.WidthMm - moldBase.MarginMm;
        var centerY = moldBase.DepthMm / 2.0m;

        for (var index = 0; index < request.EjectorPinCount; index++)
        {
            var centerX = request.EjectorPinCount == 1
                ? moldBase.WidthMm / 2.0m
                : minX + (((maxX - minX) * index) / (request.EjectorPinCount - 1));

            pins.Add(new EjectorPin(
                Id: $"ejector-pin-{index + 1:000}",
                CenterXmm: centerX,
                CenterYmm: centerY,
                StartZmm: 0.0m,
                EndZmm: moldBase.MarginMm,
                DiameterMm: request.EjectorPinDiameterMm));
        }

        var warnings = new[]
        {
            "Ejector pins are preliminary vertical planning references only.",
            "No collision, mark, plate, or ejector-mechanism validation is performed in Phase 8A."
        };

        return new EjectorPinPlan(
            IsSuccessful: true,
            Pins: pins,
            Warnings: warnings);
    }

    private static VentPlan PlanVents(MoldSystemRequest request, MoldBaseEnvelope moldBase)
    {
        var centerZ = moldBase.HeightMm / 2.0m;

        var channels = new[]
        {
            new VentChannel(
                Id: "vent-channel-001",
                StartXmm: moldBase.MarginMm,
                StartYmm: moldBase.MarginMm / 2.0m,
                StartZmm: centerZ,
                EndXmm: moldBase.WidthMm - moldBase.MarginMm,
                EndYmm: moldBase.MarginMm / 2.0m,
                EndZmm: centerZ,
                WidthMm: request.VentWidthMm,
                DepthMm: request.VentDepthMm),
            new VentChannel(
                Id: "vent-channel-002",
                StartXmm: moldBase.MarginMm,
                StartYmm: moldBase.DepthMm - (moldBase.MarginMm / 2.0m),
                StartZmm: centerZ,
                EndXmm: moldBase.WidthMm - moldBase.MarginMm,
                EndYmm: moldBase.DepthMm - (moldBase.MarginMm / 2.0m),
                EndZmm: centerZ,
                WidthMm: request.VentWidthMm,
                DepthMm: request.VentDepthMm)
        };

        var warnings = new[]
        {
            "Vent channels are preliminary planning references only.",
            "No gas-flow, flash, machining, or shutoff validation is performed in Phase 8A."
        };

        return new VentPlan(
            IsSuccessful: true,
            Channels: channels,
            Warnings: warnings);
    }

    private static InsertPlan PlanInserts(MoldSystemRequest request, MoldBaseEnvelope moldBase)
    {
        var pocket = new InsertPocket(
            Id: "insert-pocket-001",
            MinXmm: moldBase.MarginMm - request.InsertClearanceMm,
            MinYmm: moldBase.MarginMm - request.InsertClearanceMm,
            MinZmm: moldBase.MarginMm - request.InsertClearanceMm,
            MaxXmm: moldBase.MarginMm + request.PartSizeXmm + request.InsertClearanceMm,
            MaxYmm: moldBase.MarginMm + request.PartSizeYmm + request.InsertClearanceMm,
            MaxZmm: moldBase.MarginMm + request.PartSizeZmm + request.InsertClearanceMm,
            ClearanceMm: request.InsertClearanceMm);

        var warnings = new[]
        {
            "Insert pocket is a preliminary envelope reference only.",
            "No insert locking, machining, tolerance stack-up, or thermal expansion validation is performed in Phase 8A."
        };

        return new InsertPlan(
            IsSuccessful: true,
            Pockets: new[] { pocket },
            Warnings: warnings);
    }
}