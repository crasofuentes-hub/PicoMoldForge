namespace PicoMoldForge.Parts.Gears;

/// <summary>Basic rack tooth proportions, as multiples of the normal module.</summary>
public sealed record GearToothSystem(double AddendumCoefficient, double DedendumCoefficient, double RootFilletCoefficient)
{
    /// <summary>ISO 53 basic rack, profile A: addendum 1.0 m, dedendum 1.25 m, root fillet 0.38 m.</summary>
    public static readonly GearToothSystem Iso53ProfileA = new(1.0, 1.25, 0.38);
}

/// <summary>
/// Involute cylindrical gear definition (spur when HelixAngleDeg = 0). Positive helix angle = right-hand helix
/// (teeth advance in +C as Z increases). All lengths in millimeters.
/// </summary>
public sealed record GearSpec(
    double NormalModule,
    int Teeth,
    double NormalPressureAngleDeg,
    double HelixAngleDeg,
    double ProfileShiftCoefficient,
    double ThicknessAllowanceMm,
    GearToothSystem ToothSystem)
{
    public static GearSpec Spur(double module, int teeth, double pressureAngleDeg = 20, double profileShift = 0, double thicknessAllowanceMm = 0) =>
        new(module, teeth, pressureAngleDeg, 0, profileShift, thicknessAllowanceMm, GearToothSystem.Iso53ProfileA);

    public static GearSpec Helical(double normalModule, int teeth, double helixAngleDeg, double normalPressureAngleDeg = 20, double profileShift = 0, double thicknessAllowanceMm = 0) =>
        new(normalModule, teeth, normalPressureAngleDeg, helixAngleDeg, profileShift, thicknessAllowanceMm, GearToothSystem.Iso53ProfileA);

    public double HelixAngleRad => HelixAngleDeg * Math.PI / 180.0;

    public double NormalPressureAngleRad => NormalPressureAngleDeg * Math.PI / 180.0;

    /// <summary>m_t = m_n / cos(beta).</summary>
    public double TransverseModule => NormalModule / Math.Cos(HelixAngleRad);

    /// <summary>alpha_t = atan(tan(alpha_n) / cos(beta)).</summary>
    public double TransversePressureAngleRad => Math.Atan(Math.Tan(NormalPressureAngleRad) / Math.Cos(HelixAngleRad));

    public double PitchRadius => TransverseModule * Teeth / 2.0;

    public double BaseRadius => PitchRadius * Math.Cos(TransversePressureAngleRad);

    public double ProfileShiftMm => ProfileShiftCoefficient * NormalModule;

    public double AddendumMm => ToothSystem.AddendumCoefficient * NormalModule;

    public double DedendumMm => ToothSystem.DedendumCoefficient * NormalModule;

    public double TipRadius => PitchRadius + AddendumMm + ProfileShiftMm;

    public double RootRadius => PitchRadius - DedendumMm + ProfileShiftMm;

    public double TransversePitch => Math.PI * TransverseModule;

    public double CutterTipFilletRadius => ToothSystem.RootFilletCoefficient * NormalModule;

    /// <summary>Transverse arc tooth thickness on the pitch circle: m_t (pi/2 + 2 x tan(alpha_n)) - allowance.</summary>
    public double PitchToothThickness =>
        (TransverseModule * ((Math.PI / 2.0) + (2.0 * ProfileShiftCoefficient * Math.Tan(NormalPressureAngleRad)))) - ThicknessAllowanceMm;

    /// <summary>Helix lead rate: right-hand twist (radians) per millimeter of axial travel.</summary>
    public double TwistRadPerMm => Math.Tan(HelixAngleRad) / PitchRadius;

    public static double Involute(double angleRad) => Math.Tan(angleRad) - angleRad;

    /// <summary>
    /// Half angular tooth thickness (radians) on a circle of the given radius, valid on the involute flank
    /// (base radius &lt;= radius &lt;= tip radius).
    /// </summary>
    public double HalfToothAngleAt(double radius)
    {
        if (radius < BaseRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "The involute flank only exists outside the base circle.");
        }

        var alphaAtRadius = Math.Acos(BaseRadius / radius);
        return (PitchToothThickness / (2.0 * PitchRadius)) + Involute(TransversePressureAngleRad) - Involute(alphaAtRadius);
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!(NormalModule > 0) || NormalModule > 100)
        {
            errors.Add("module must be greater than 0 and at most 100 mm.");
        }

        if (Teeth < 6 || Teeth > 400)
        {
            errors.Add("teeth must be between 6 and 400.");
        }

        if (!(NormalPressureAngleDeg >= 10 && NormalPressureAngleDeg <= 35))
        {
            errors.Add("pressureAngleDeg must be between 10 and 35 degrees.");
        }

        if (!(Math.Abs(HelixAngleDeg) <= 45))
        {
            errors.Add("helixAngleDeg must be between -45 and 45 degrees.");
        }

        if (!(Math.Abs(ProfileShiftCoefficient) <= 1.0))
        {
            errors.Add("profileShift must be between -1 and 1.");
        }

        if (!(ThicknessAllowanceMm >= 0) || ThicknessAllowanceMm > 0.25 * NormalModule)
        {
            errors.Add("thicknessAllowanceMm must be between 0 and 0.25 * module.");
        }

        if (errors.Count == 0)
        {
            if (!(RootRadius > 0.5 * NormalModule))
            {
                errors.Add("Root radius is too small for this module and tooth count.");
            }

            var tipHalfAngle = HalfToothAngleAt(Math.Max(TipRadius, BaseRadius));

            if (!(tipHalfAngle > 0))
            {
                errors.Add("Teeth become pointed before the tip circle (reduce profile shift or increase teeth).");
            }
        }

        return errors;
    }
}
