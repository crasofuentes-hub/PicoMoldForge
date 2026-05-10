namespace PicoMoldForge.Core.DfAM;

public sealed class PreliminaryDfAMAnalyzer
{
    public DfAMReport Analyze(DfAMInputSnapshot snapshot)
    {
        var validationErrors = snapshot.Validate();

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid DfAM input snapshot: " + string.Join(" ", validationErrors),
                nameof(snapshot));
        }

        var checks = new List<DfAMCheckResult>
        {
            CheckMinimumWallThickness(snapshot),
            CheckCoolingClearance(snapshot),
            CheckLatticeBeamRadius(snapshot),
            CheckEjectorPinDiameter(snapshot),
            CreateNonCertificationNotice(snapshot)
        };

        var warnings = new List<string>
        {
            "Phase 9A performs preliminary DfAM checks only.",
            "DfAM checks are deterministic sanity checks, not manufacturability certification.",
            "Minimum wall thickness currently depends on an input snapshot value; no geometric wall-thickness solver exists yet."
        };

        if (snapshot.UsesPreliminaryGeometry)
        {
            warnings.Add("Input geometry is preliminary diagnostic geometry and must not be treated as production-ready tooling.");
        }

        foreach (var failedWarning in checks.Where(check =>
            !check.IsPassed && check.Rule.Severity == DfAMRuleSeverity.Warning))
        {
            warnings.Add($"DfAM warning: {failedWarning.Rule.Code}: {failedWarning.Message}");
        }

        var isSuccessful = checks.All(check =>
            check.IsPassed || check.Rule.Severity != DfAMRuleSeverity.Error);

        return new DfAMReport(
            IsSuccessful: isSuccessful,
            Checks: checks,
            Warnings: warnings);
    }

    private static DfAMCheckResult CheckMinimumWallThickness(DfAMInputSnapshot snapshot)
    {
        var rule = new DfAMRule(
            "MINIMUM_WALL_THICKNESS_PRELIMINARY",
            DfAMRuleSeverity.Warning,
            "Checks whether the supplied minimum wall thickness meets the supplied preliminary recommendation.");

        var passed = snapshot.MinimumWallThicknessMm >= snapshot.RecommendedMinimumWallThicknessMm;

        var message = passed
            ? $"Minimum wall thickness {snapshot.MinimumWallThicknessMm} mm meets preliminary recommendation {snapshot.RecommendedMinimumWallThicknessMm} mm."
            : $"Minimum wall thickness {snapshot.MinimumWallThicknessMm} mm is below preliminary recommendation {snapshot.RecommendedMinimumWallThicknessMm} mm.";

        return new DfAMCheckResult(rule, passed, message);
    }

    private static DfAMCheckResult CheckCoolingClearance(DfAMInputSnapshot snapshot)
    {
        var rule = new DfAMRule(
            "COOLING_CLEARANCE_SANITY",
            DfAMRuleSeverity.Warning,
            "Checks whether cooling clearance is greater than half of the cooling channel diameter.");

        var requiredMinimum = snapshot.CoolingChannelDiameterMm / 2.0m;
        var passed = snapshot.CoolingMinimumClearanceMm > requiredMinimum;

        var message = passed
            ? $"Cooling clearance {snapshot.CoolingMinimumClearanceMm} mm is greater than required preliminary minimum {requiredMinimum} mm."
            : $"Cooling clearance {snapshot.CoolingMinimumClearanceMm} mm is not greater than required preliminary minimum {requiredMinimum} mm.";

        return new DfAMCheckResult(rule, passed, message);
    }

    private static DfAMCheckResult CheckLatticeBeamRadius(DfAMInputSnapshot snapshot)
    {
        var rule = new DfAMRule(
            "LATTICE_BEAM_RADIUS_SANITY",
            DfAMRuleSeverity.Warning,
            "Checks whether lattice beam radius is less than half of lattice cell size.");

        var requiredMaximum = snapshot.LatticeCellSizeMm / 2.0m;
        var passed = snapshot.LatticeBeamRadiusMm < requiredMaximum;

        var message = passed
            ? $"Lattice beam radius {snapshot.LatticeBeamRadiusMm} mm is less than preliminary maximum {requiredMaximum} mm."
            : $"Lattice beam radius {snapshot.LatticeBeamRadiusMm} mm must be less than preliminary maximum {requiredMaximum} mm.";

        return new DfAMCheckResult(rule, passed, message);
    }

    private static DfAMCheckResult CheckEjectorPinDiameter(DfAMInputSnapshot snapshot)
    {
        var rule = new DfAMRule(
            "EJECTOR_PIN_DIAMETER_SANITY",
            DfAMRuleSeverity.Warning,
            "Checks whether ejector pin diameter is positive and reportable.");

        var passed = snapshot.EjectorPinDiameterMm > 0;

        var message = passed
            ? $"Ejector pin diameter {snapshot.EjectorPinDiameterMm} mm is positive."
            : "Ejector pin diameter must be greater than zero.";

        return new DfAMCheckResult(rule, passed, message);
    }

    private static DfAMCheckResult CreateNonCertificationNotice(DfAMInputSnapshot snapshot)
    {
        var rule = new DfAMRule(
            "NON_CERTIFICATION_NOTICE",
            DfAMRuleSeverity.Info,
            "States that the current DfAM report is not production certification.");

        return new DfAMCheckResult(
            rule,
            IsPassed: true,
            Message: snapshot.UsesPreliminaryGeometry
                ? "DfAM report is informational only and is based on preliminary diagnostic geometry."
                : "DfAM report is informational only and does not certify production manufacturability.");
    }
}