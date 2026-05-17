using PicoMoldForge.Core.Engineering.DraftAnalysis;
using PicoMoldForge.Core.Engineering.Separation;

namespace PicoMoldForge.Core.Exports;

public sealed record FunctionalMoldAlphaReport(
    CoreCavitySeparationSummary? Separation,
    ShutoffStrategySummary? Shutoff,
    DraftBasicGeometryAnalysisSummary? DraftGeometry,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Separation is null &&
            Shutoff is null &&
            DraftGeometry is null)
        {
            errors.Add("At least one Functional Mold Alpha metric group is required.");
        }

        if (Warnings is null)
        {
            errors.Add("Warnings is required.");
        }

        if (Separation is not null)
        {
            if (Separation.QualityScore < 0m || Separation.QualityScore > 1m)
            {
                errors.Add("Separation QualityScore must be between 0 and 1.");
            }

            if (Separation.OverlapRatio < 0m)
            {
                errors.Add("Separation OverlapRatio cannot be negative.");
            }

            if (Separation.GapRatio < 0m)
            {
                errors.Add("Separation GapRatio cannot be negative.");
            }
        }

        if (Shutoff is not null)
        {
            if (Shutoff.QualityScore < 0m || Shutoff.QualityScore > 1m)
            {
                errors.Add("Shutoff QualityScore must be between 0 and 1.");
            }

            if (Shutoff.MaximumGapMm < 0m)
            {
                errors.Add("Shutoff MaximumGapMm cannot be negative.");
            }

            if (Shutoff.MaximumOverlapMm < 0m)
            {
                errors.Add("Shutoff MaximumOverlapMm cannot be negative.");
            }
        }

        if (DraftGeometry is not null)
        {
            if (DraftGeometry.FaceCount < 0)
            {
                errors.Add("DraftGeometry FaceCount cannot be negative.");
            }

            if (DraftGeometry.RiskySurfaceAreaMm2 < 0m)
            {
                errors.Add("DraftGeometry RiskySurfaceAreaMm2 cannot be negative.");
            }
        }

        return errors;
    }
}