using System.Globalization;
using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Gears;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Verification;

namespace PicoMoldForge.Parts.Templates;

/// <summary>Parameters of an involute cylindrical gear (spur when HelixAngleDeg = 0). Millimeters and degrees.</summary>
public sealed record GearParameters
{
    public double Module { get; init; } = 2.0;

    public int Teeth { get; init; } = 24;

    public double PressureAngleDeg { get; init; } = 20.0;

    /// <summary>Positive = right-hand helix. 0 = spur gear.</summary>
    public double HelixAngleDeg { get; init; }

    public double FaceWidth { get; init; } = 12.0;

    /// <summary>0 = no bore.</summary>
    public double BoreDiameter { get; init; } = 12.0;

    /// <summary>0 = no keyway.</summary>
    public double KeywayWidth { get; init; } = 4.0;

    /// <summary>Hub keyway depth measured from the bore surface (t2).</summary>
    public double KeywayDepth { get; init; } = 1.8;

    public double ProfileShift { get; init; }

    public double ThicknessAllowance { get; init; }
}

/// <summary>
/// Gear as two features: the transverse tooth profile dropped along Z (with simultaneous C rotation when the
/// gear is helical), then the bore and keyway drilled along Z. Tooth 0 is centered on +X at the top face.
/// </summary>
public static class GearTemplate
{
    public const string SpurType = "spurGear";
    public const string HelicalType = "helicalGear";

    public static PartDefinition Build(string id, string? title, GearParameters p, MeshSettings mesh, bool helical)
    {
        ArgumentNullException.ThrowIfNull(p);

        var spec = new GearSpec(p.Module, p.Teeth, p.PressureAngleDeg, p.HelixAngleDeg, p.ProfileShift, p.ThicknessAllowance, GearToothSystem.Iso53ProfileA);
        var errors = new List<string>(spec.Validate());

        if (helical && p.HelixAngleDeg == 0)
        {
            errors.Add("helicalGear needs a non-zero helixAngleDeg (use spurGear for straight teeth).");
        }

        if (!helical && p.HelixAngleDeg != 0)
        {
            errors.Add("spurGear must have helixAngleDeg = 0 (use helicalGear).");
        }

        if (!(p.FaceWidth > 0 && p.FaceWidth <= 500))
        {
            errors.Add("faceWidth must be between 0 and 500 mm.");
        }

        if (p.BoreDiameter < 0 || p.KeywayWidth < 0 || p.KeywayDepth < 0)
        {
            errors.Add("boreDiameter, keywayWidth and keywayDepth cannot be negative.");
        }

        var rimLimit = spec.RootRadius - (1.5 * p.Module);
        var keywayReach = p.BoreDiameter > 0 && p.KeywayWidth > 0 ? (p.BoreDiameter / 2) + p.KeywayDepth : p.BoreDiameter / 2;

        if (errors.Count == 0 && keywayReach >= rimLimit)
        {
            errors.Add(FormattableString.Invariant($"Bore/keyway reach ({keywayReach:0.###} mm) leaves less than 1.5 * module of rim under the root circle ({spec.RootRadius:0.###} mm)."));
        }

        if (p.KeywayWidth > 0 && p.BoreDiameter > 0 && (p.KeywayWidth >= p.BoreDiameter || p.KeywayDepth <= 0))
        {
            errors.Add("keywayWidth must be smaller than the bore and keywayDepth must be positive.");
        }

        if (p.KeywayWidth > 0 && p.BoreDiameter == 0)
        {
            errors.Add("A keyway needs a bore (boreDiameter > 0).");
        }

        if (errors.Count > 0)
        {
            throw new PartDefinitionException(errors);
        }

        var teethProfile = new GearToothProfile(spec);
        var twistRadPerMm = spec.TwistRadPerMm;

        // The profile drops from the top face (z = faceWidth) to z = 0. A right-hand helix turns +C as Z grows,
        // so the accumulated twist from top to bottom is negative.
        var twistDeg = AxisConvention.RadiansToDegrees(-p.FaceWidth * twistRadPerMm);

        var features = new List<FeatureDefinition>
        {
            new(
                Id: "teeth",
                Label: helical ? "Dientes: involuta que cae en Z girando en C" : "Dientes: involuta que cae en Z",
                Operation: FeatureOperation.Add,
                Sweep: new LinearSweepDefinition(LinearAxis.Z, p.FaceWidth, 0, twistDeg),
                Profile: teethProfile,
                Instances: Placement.Single(Vec3.Zero))
        };

        if (p.BoreDiameter > 0)
        {
            IProfile2D cut = new CircleProfile(Vec2.Zero, p.BoreDiameter / 2);

            if (p.KeywayWidth > 0)
            {
                cut = new ProfileUnion(new IProfile2D[]
                {
                    cut,
                    new RectangleProfile(new Vec2(0, -p.KeywayWidth / 2), new Vec2((p.BoreDiameter / 2) + p.KeywayDepth, p.KeywayWidth / 2))
                });
            }

            features.Add(new FeatureDefinition(
                Id: "bore",
                Label: p.KeywayWidth > 0 ? "Barreno y cuñero: taladro en Z" : "Barreno: taladro en Z",
                Operation: FeatureOperation.Subtract,
                Sweep: new LinearSweepDefinition(LinearAxis.Z, p.FaceWidth + 1, -1),
                Profile: cut,
                Instances: Placement.Single(Vec3.Zero)));
        }

        var expectedBox = ExpectedBoundingBox(spec, p.FaceWidth, twistRadPerMm);
        var checks = BuildChecks(spec, p, helical, twistRadPerMm);

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("Módulo normal", Mm(p.Module)),
            new("Dientes", p.Teeth.ToString(CultureInfo.InvariantCulture)),
            new("Ángulo de presión", Deg(p.PressureAngleDeg)),
            new("Ángulo de hélice", Deg(p.HelixAngleDeg)),
            new("Ancho de cara", Mm(p.FaceWidth)),
            new("Diámetro primitivo", Mm(2 * spec.PitchRadius)),
            new("Diámetro exterior", Mm(2 * spec.TipRadius)),
            new("Diámetro de raíz", Mm(2 * spec.RootRadius)),
            new("Diámetro base", Mm(2 * spec.BaseRadius)),
            new("Espesor del diente (primitivo)", Mm(spec.PitchToothThickness)),
            new("Corrección de perfil x", p.ProfileShift.ToString("0.###", CultureInfo.InvariantCulture)),
            new("Barreno", p.BoreDiameter > 0 ? Mm(p.BoreDiameter) : "sin barreno"),
            new("Cuñero", p.KeywayWidth > 0 ? FormattableString.Invariant($"{p.KeywayWidth:0.###} x {p.KeywayDepth:0.###} mm") : "sin cuñero"),
            new("Cremallera", "ISO 53 perfil A (1.0 / 1.25 / 0.38 m)")
        };

        return new PartDefinition(
            id,
            title ?? (helical ? FormattableString.Invariant($"Engrane helicoidal m{p.Module:0.##} z{p.Teeth} β{p.HelixAngleDeg:0.#}°") : FormattableString.Invariant($"Engrane recto m{p.Module:0.##} z{p.Teeth}")),
            helical ? HelicalType : SpurType,
            parameters,
            features,
            new PartExpectations(expectedBox, null, p.BoreDiameter > 0 ? 1 : 0, checks),
            mesh);
    }

    /// <summary>Extents of the tip lands over the face width (independent of the distance field).</summary>
    internal static Box3 ExpectedBoundingBox(GearSpec spec, double faceWidth, double twistRadPerMm)
    {
        var tipHalfAngle = spec.HalfToothAngleAt(spec.TipRadius);
        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity;
        var maxY = double.NegativeInfinity;
        var zSteps = twistRadPerMm == 0 ? 0 : 400;

        for (var iz = 0; iz <= zSteps; iz++)
        {
            var z = zSteps == 0 ? faceWidth : faceWidth * iz / zSteps;
            var rotation = twistRadPerMm * (z - faceWidth);

            for (var k = 0; k < spec.Teeth; k++)
            {
                var center = (2 * Math.PI * k / spec.Teeth) + rotation;

                for (var s = 0; s <= 64; s++)
                {
                    var angle = center - tipHalfAngle + (2 * tipHalfAngle * s / 64);
                    var x = spec.TipRadius * Math.Cos(angle);
                    var y = spec.TipRadius * Math.Sin(angle);
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        return new Box3(new Vec3(minX, minY, 0), new Vec3(maxX, maxY, faceWidth));
    }

    private static IReadOnlyList<IPartCheck> BuildChecks(GearSpec spec, GearParameters p, bool helical, double twistRadPerMm)
    {
        var top = p.FaceWidth;
        var mid = p.FaceWidth / 2;
        var midRotation = twistRadPerMm * (mid - top);
        var gap = Math.PI / spec.Teeth;

        // Transverse section just below the top face, where the helix has not turned the profile yet.
        var section = top - 1e-6;

        Vec3 AtTopPolar(double radius, double angle) => new(radius * Math.Cos(angle), radius * Math.Sin(angle), section);

        var checks = new List<IPartCheck>
        {
            new DelegateCheck(
                "gear.pitch-thickness",
                "Espesor del diente en el círculo primitivo = m_t (π/2 + 2 x tan α) − holgura (medido en el sólido)",
                context =>
                {
                    var measured = 2 * spec.PitchRadius * HalfAngleOnSolid(context, spec, spec.PitchRadius, section);
                    return (Mm(spec.PitchToothThickness) + " ± 0.002 mm", Mm(measured), Math.Abs(measured - spec.PitchToothThickness) <= 0.002);
                }),
            new DelegateCheck(
                "gear.involute",
                "El flanco sigue la involuta: medio espesor angular ψ(ρ) en tres radios entre base y punta (medido en el sólido)",
                context =>
                {
                    var start = Math.Max(spec.BaseRadius, spec.RootRadius);
                    var worst = 0.0;

                    foreach (var fraction in new[] { 0.2, 0.5, 0.8 })
                    {
                        var rho = start + (fraction * (spec.TipRadius - start));
                        var arcError = rho * (HalfAngleOnSolid(context, spec, rho, section) - spec.HalfToothAngleAt(rho));
                        worst = Math.Max(worst, Math.Abs(arcError));
                    }

                    return ("error de arco <= 0.002 mm", Mm(worst), worst <= 0.002);
                }),
            new SurfacePointCheck("gear.tip-diameter", "Diámetro exterior = " + Mm(2 * spec.TipRadius), AtTopPolar(spec.TipRadius, 0)),
            new SurfacePointCheck("gear.root-diameter", "Diámetro de raíz = " + Mm(2 * spec.RootRadius), AtTopPolar(spec.RootRadius, gap)),
            new CircleCrossingCountCheck(
                "gear.tooth-count",
                FormattableString.Invariant($"{spec.Teeth} dientes en el círculo primitivo a media altura"),
                spec.PitchRadius,
                mid,
                2 * spec.Teeth),
            new DelegateCheck(
                "gear.tooth-at-mid-height",
                "El diente 0 está centrado en C = giro de la hélice a media altura",
                context =>
                {
                    var measured = ToothCenterAngle(context, spec, mid, midRotation);
                    var error = spec.PitchRadius * Math.Abs(measured - midRotation);
                    return (Deg(AxisConvention.RadiansToDegrees(midRotation)), Deg(AxisConvention.RadiansToDegrees(measured)), error <= 0.005);
                })
        };

        if (p.BoreDiameter > 0)
        {
            // Opposite the keyway (-X) the bore surface is at the bore radius.
            checks.Add(new SurfacePointCheck("gear.bore-diameter", "Diámetro del barreno = " + Mm(p.BoreDiameter), new Vec3(-p.BoreDiameter / 2, 0, mid)));
        }

        if (p.BoreDiameter > 0 && p.KeywayWidth > 0)
        {
            checks.Add(new SurfacePointCheck("gear.keyway-depth", "Fondo del cuñero a " + Mm((p.BoreDiameter / 2) + p.KeywayDepth) + " del eje", new Vec3((p.BoreDiameter / 2) + p.KeywayDepth, 0, mid)));
        }

        if (helical)
        {
            checks.Add(new DelegateCheck(
                "gear.helix-twist",
                "Giro entre caras = b · tan β / r (hélice derecha: +C al subir en Z)",
                context =>
                {
                    var expected = p.FaceWidth * twistRadPerMm;
                    var atTop = ToothCenterAngle(context, spec, top - 1e-4, 0);
                    var atBottom = ToothCenterAngle(context, spec, 1e-4, -expected);
                    var measured = atTop - atBottom;
                    var error = spec.PitchRadius * Math.Abs(measured - expected);
                    return (Deg(AxisConvention.RadiansToDegrees(expected)), Deg(AxisConvention.RadiansToDegrees(measured)), error <= 0.005);
                }));
        }

        return checks;
    }

    /// <summary>
    /// Half angular thickness (radians) of tooth 0 on the 3D solid at height z: both flank crossings on the circle
    /// of the given radius, halved, so a small rotation of the section cannot bias the measurement.
    /// </summary>
    private static double HalfAngleOnSolid(PartCheckContext context, GearSpec spec, double radius, double z)
    {
        var halfPitch = Math.PI / spec.Teeth;

        double Crossing(double inside, double outside)
        {
            for (var i = 0; i < 80; i++)
            {
                var m = (inside + outside) / 2;

                if (context.Sdf.Distance(new Vec3(radius * Math.Cos(m), radius * Math.Sin(m), z)) < 0)
                {
                    inside = m;
                }
                else
                {
                    outside = m;
                }
            }

            return (inside + outside) / 2;
        }

        return (Crossing(0, halfPitch) - Crossing(0, -halfPitch)) / 2;
    }

    /// <summary>Center angle of the tooth nearest to <paramref name="guess"/> on the pitch circle at height z (3D solid).</summary>
    private static double ToothCenterAngle(PartCheckContext context, GearSpec spec, double z, double guess)
    {
        var r = spec.PitchRadius;
        var halfPitch = Math.PI / spec.Teeth;

        double Edge(double inside, double outside)
        {
            for (var i = 0; i < 80; i++)
            {
                var m = (inside + outside) / 2;

                if (context.Sdf.Distance(new Vec3(r * Math.Cos(m), r * Math.Sin(m), z)) < 0)
                {
                    inside = m;
                }
                else
                {
                    outside = m;
                }
            }

            return (inside + outside) / 2;
        }

        var left = Edge(guess, guess - halfPitch);
        var right = Edge(guess, guess + halfPitch);
        return (left + right) / 2;
    }

    private static string Mm(double value) => value.ToString("0.####", CultureInfo.InvariantCulture) + " mm";

    private static string Deg(double value) => value.ToString("0.####", CultureInfo.InvariantCulture) + "°";
}
