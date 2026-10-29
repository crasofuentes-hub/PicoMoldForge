using System.Globalization;
using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Verification;

namespace PicoMoldForge.Parts.Templates;

/// <summary>Simplified piston parameters (millimeters). Z = piston axis, crown at Z = Height, pin along X.</summary>
public sealed record PistonParameters
{
    public double BoreDiameter { get; init; } = 56.0;

    /// <summary>Diametral running clearance: piston diameter = bore - clearance.</summary>
    public double Clearance { get; init; } = 0.05;

    public double Height { get; init; } = 52.0;

    /// <summary>Distance from the pin axis to the crown top.</summary>
    public double CompressionHeight { get; init; } = 30.0;

    public double PinDiameter { get; init; } = 14.0;

    public double PinBossDiameter { get; init; } = 22.0;

    /// <summary>Free distance between the two pin bosses (room for the connecting rod small end).</summary>
    public double PinBossGap { get; init; } = 22.0;

    public double CrownThickness { get; init; } = 6.0;

    public double SkirtWall { get; init; } = 3.5;

    public double RingBeltWall { get; init; } = 6.0;

    public double TopLand { get; init; } = 5.0;

    /// <summary>Ring groove widths from the crown down (compression, second, oil).</summary>
    public IReadOnlyList<double> RingGrooveWidths { get; init; } = new[] { 1.2, 1.2, 2.5 };

    /// <summary>Land heights between consecutive grooves (count = grooves - 1).</summary>
    public IReadOnlyList<double> RingLands { get; init; } = new[] { 3.5, 3.5 };

    public double GrooveDepth { get; init; } = 2.6;

    /// <summary>Depth of the flats machined on the pin sides of the skirt (0 = none).</summary>
    public double SkirtReliefDepth { get; init; } = 1.2;
}

/// <summary>
/// Piston as rotary and linear sweeps: body turned about C, cavity turned about C (subtract), pin bosses
/// along X (add, mirrored by a C pattern), pin bore drilled along X, ring grooves turned about C, skirt
/// reliefs dropped along Z on the pin sides.
/// </summary>
public static class PistonTemplate
{
    public const string Type = "piston";

    public static PartDefinition Build(string id, string? title, PistonParameters p, MeshSettings mesh)
    {
        ArgumentNullException.ThrowIfNull(p);

        var errors = new List<string>();
        var radius = (p.BoreDiameter - p.Clearance) / 2;
        var skirtInner = radius - p.SkirtWall;
        var beltInner = radius - p.RingBeltWall;
        var pinZ = p.Height - p.CompressionHeight;
        var bossRadius = p.PinBossDiameter / 2;
        var pinRadius = p.PinDiameter / 2;
        var crownUnder = p.Height - p.CrownThickness;

        if (!(p.BoreDiameter >= 10 && p.BoreDiameter <= 500))
        {
            errors.Add("boreDiameter must be between 10 and 500 mm.");
        }

        if (!(p.Clearance >= 0 && p.Clearance < 0.02 * p.BoreDiameter))
        {
            errors.Add("clearance must be between 0 and 2% of the bore.");
        }

        if (p.RingGrooveWidths.Count == 0 || p.RingLands.Count != p.RingGrooveWidths.Count - 1)
        {
            errors.Add("ringLands must have exactly one value less than ringGrooveWidths.");
        }

        if (p.RingGrooveWidths.Any(w => !(w > 0)) || p.RingLands.Any(l => !(l > 0)))
        {
            errors.Add("Ring groove widths and lands must be positive.");
        }

        var beltHeight = p.TopLand + p.RingGrooveWidths.Sum() + p.RingLands.Sum();
        var beltBottom = p.Height - beltHeight;
        var transitionTop = beltBottom - 1.0;
        var transitionBottom = transitionTop - 3.0;

        if (!(p.SkirtWall > 0 && p.RingBeltWall >= p.SkirtWall && p.RingBeltWall < radius * 0.5))
        {
            errors.Add("Walls must satisfy 0 < skirtWall <= ringBeltWall < 0.5 * piston radius.");
        }

        if (!(p.GrooveDepth > 0 && p.GrooveDepth < p.RingBeltWall - 1.0))
        {
            errors.Add("grooveDepth must be positive and leave at least 1 mm of ring belt wall.");
        }

        if (!(p.CrownThickness > 1 && crownUnder > transitionTop + 1))
        {
            errors.Add("Crown thickness must be > 1 mm and the crown underside must stay above the ring belt transition.");
        }

        if (!(transitionBottom > pinZ + bossRadius - 4))
        {
            errors.Add("The ring belt is too deep for the pin position (increase compressionHeight or reduce the belt).");
        }

        if (!(pinRadius > 0 && bossRadius > pinRadius + 2))
        {
            errors.Add("pinBossDiameter must exceed pinDiameter by more than 4 mm.");
        }

        if (!(pinZ - bossRadius > 2))
        {
            errors.Add("Pin bosses must stay above the skirt bottom (increase height or compressionHeight).");
        }

        if (!(p.PinBossGap > 0 && p.PinBossGap / 2 < skirtInner - 2))
        {
            errors.Add("pinBossGap must be positive and smaller than the skirt inner diameter minus 4 mm.");
        }

        var bossEnd = skirtInner + 1.0;
        var bossCorner = Math.Sqrt((bossEnd * bossEnd) + (bossRadius * bossRadius));

        if (!(bossCorner < radius - 0.1))
        {
            errors.Add("Pin bosses would break through the piston wall (reduce pinBossDiameter).");
        }

        var reliefPlane = radius - p.SkirtReliefDepth;

        if (p.SkirtReliefDepth < 0 || (p.SkirtReliefDepth > 0 && !(reliefPlane > bossEnd + 0.5 && reliefPlane > skirtInner + 1.0)))
        {
            errors.Add("skirtReliefDepth must leave at least 1 mm of skirt wall and clear the pin bosses.");
        }

        if (errors.Count > 0)
        {
            throw new PartDefinitionException(errors);
        }

        var features = new List<FeatureDefinition>
        {
            new(
                "body",
                "Cuerpo: perfil que gira en C",
                FeatureOperation.Add,
                new RotarySweepDefinition(RotaryAxis.C),
                new RectangleProfile(new Vec2(-radius, 0), new Vec2(radius, p.Height)),
                Placement.Single(Vec3.Zero)),
            new(
                "cavity",
                "Cavidad interior: perfil que gira en C, restando",
                FeatureOperation.Subtract,
                new RotarySweepDefinition(RotaryAxis.C),
                PolygonProfile.FromHalfSection(new[]
                {
                    new Vec2(0, -1),
                    new Vec2(skirtInner, -1),
                    new Vec2(skirtInner, transitionBottom),
                    new Vec2(beltInner, transitionTop),
                    new Vec2(beltInner, crownUnder),
                    new Vec2(0, crownUnder)
                }),
                Placement.Single(Vec3.Zero)),
            new(
                "pin-bosses",
                "Mazas del perno: círculo que avanza en X, espejado con C 180°",
                FeatureOperation.Add,
                new LinearSweepDefinition(LinearAxis.X, p.PinBossGap / 2, bossEnd),
                new CircleProfile(new Vec2(0, pinZ), bossRadius),
                Placement.Circular(Placement.Single(Vec3.Zero), RotaryAxis.C, 2, 0, 360, Vec3.Zero)),
            new(
                "pin-bore",
                "Barreno del perno: taladro en X",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.X, -radius - 1, radius + 1),
                new CircleProfile(new Vec2(0, pinZ), pinRadius),
                Placement.Single(Vec3.Zero))
        };

        var grooveTop = p.Height - p.TopLand;

        for (var i = 0; i < p.RingGrooveWidths.Count; i++)
        {
            var width = p.RingGrooveWidths[i];
            features.Add(new FeatureDefinition(
                $"ring-groove-{i + 1}",
                FormattableString.Invariant($"Ranura de anillo {i + 1}: perfil que gira en C, restando"),
                FeatureOperation.Subtract,
                new RotarySweepDefinition(RotaryAxis.C),
                new RectangleProfile(new Vec2(radius - p.GrooveDepth, grooveTop - width), new Vec2(radius + 1, grooveTop)),
                Placement.Single(Vec3.Zero)));

            if (i < p.RingLands.Count)
            {
                grooveTop -= width + p.RingLands[i];
            }
        }

        if (p.SkirtReliefDepth > 0)
        {
            var reliefTop = beltBottom - 2.0;
            features.Add(new FeatureDefinition(
                "skirt-reliefs",
                "Rebajes de falda: rectángulo que cae en Z, espejado con C 180°",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.Z, reliefTop, -1),
                new RectangleProfile(new Vec2(reliefPlane, -radius - 1), new Vec2(radius + 1, radius + 1)),
                Placement.Circular(Placement.Single(Vec3.Zero), RotaryAxis.C, 2, 0, 360, Vec3.Zero)));
        }

        var firstGrooveMid = p.Height - p.TopLand - (p.RingGrooveWidths[0] / 2);
        var checks = new List<IPartCheck>
        {
            new SurfacePointCheck("piston.diameter", "Diámetro del pistón = " + Mm(2 * radius) + " (cilindro − holgura)", new Vec3(0, radius, p.Height - (p.TopLand / 2))),
            new SurfacePointCheck("piston.crown-top", "Corona a Z = " + Mm(p.Height), new Vec3(0, 0, p.Height)),
            new SurfacePointCheck("piston.crown-thickness", "Espesor de corona = " + Mm(p.CrownThickness), new Vec3(0, 0, crownUnder)),
            new SurfacePointCheck("piston.groove-root", "Fondo de la ranura 1 a " + Mm(radius - p.GrooveDepth) + " del eje", new Vec3(0, radius - p.GrooveDepth, firstGrooveMid)),
            new SurfacePointCheck("piston.skirt-inner", "Radio interior de falda = " + Mm(skirtInner), new Vec3(0, skirtInner, pinZ)),
            new SurfacePointCheck("piston.pin-bore", "Diámetro del perno = " + Mm(p.PinDiameter), new Vec3((skirtInner + reliefPlane) / 2, 0, pinZ + pinRadius)),
            new ContainmentCheck("piston.pin-boss", "Hay material de maza alrededor del perno", new Vec3((p.PinBossGap / 2) + 1, 0, pinZ + ((pinRadius + bossRadius) / 2)), inside: true),
            new ContainmentCheck("piston.rod-gap", "Hueco libre entre mazas para la biela", new Vec3(0, 0, pinZ), inside: false)
        };

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("Diámetro de cilindro", Mm(p.BoreDiameter)),
            new("Diámetro del pistón", Mm(2 * radius)),
            new("Altura total", Mm(p.Height)),
            new("Altura de compresión", Mm(p.CompressionHeight)),
            new("Perno", Mm(p.PinDiameter)),
            new("Mazas", Mm(p.PinBossDiameter)),
            new("Corona", Mm(p.CrownThickness)),
            new("Ranuras", string.Join(" / ", p.RingGrooveWidths.Select(w => w.ToString("0.##", CultureInfo.InvariantCulture))) + " mm"),
            new("Profundidad de ranura", Mm(p.GrooveDepth)),
            new("Pared de falda / anillos", FormattableString.Invariant($"{p.SkirtWall:0.##} / {p.RingBeltWall:0.##} mm"))
        };

        return new PartDefinition(
            id,
            title ?? FormattableString.Invariant($"Pistón Ø{p.BoreDiameter:0.##} mm"),
            Type,
            parameters,
            features,
            new PartExpectations(
                new Box3(new Vec3(-radius, -radius, 0), new Vec3(radius, radius, p.Height)),
                null,
                Genus: 2,
                checks),
            mesh);
    }

    private static string Mm(double value) => value.ToString("0.####", CultureInfo.InvariantCulture) + " mm";
}
