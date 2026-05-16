using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Separation;

public sealed class PartingPlaneScorer
{
    private readonly MoldSeparationEngine separationEngine = new();

    public PartingPlaneScoringResult Score(PartingPlaneScoringInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Candidates.Count == 0)
        {
            throw new ArgumentException("At least one parting plane candidate is required.", nameof(input));
        }

        var scores = input.Candidates
            .Select(candidate => ScoreCandidate(input, candidate))
            .OrderByDescending(score => score.QualityScore)
            .ThenBy(score => score.Severity)
            .ToArray();

        return new PartingPlaneScoringResult(
            BestScore: scores[0],
            Scores: scores);
    }

    public IReadOnlyList<PartingPlaneCandidate> GenerateDefaultCandidates(
        MoldSeparationBounds moldBlockBounds,
        MoldSeparationBounds partBounds)
    {
        ArgumentNullException.ThrowIfNull(moldBlockBounds);
        ArgumentNullException.ThrowIfNull(partBounds);

        return new[]
        {
            new PartingPlaneCandidate(
                Axis: PartingAxis.X,
                OffsetMm: Midpoint(partBounds.MinXmm, partBounds.MaxXmm),
                Source: "PartBoundsMidpoint"),

            new PartingPlaneCandidate(
                Axis: PartingAxis.Y,
                OffsetMm: Midpoint(partBounds.MinYmm, partBounds.MaxYmm),
                Source: "PartBoundsMidpoint"),

            new PartingPlaneCandidate(
                Axis: PartingAxis.Z,
                OffsetMm: Midpoint(partBounds.MinZmm, partBounds.MaxZmm),
                Source: "PartBoundsMidpoint"),

            new PartingPlaneCandidate(
                Axis: PartingAxis.X,
                OffsetMm: Midpoint(moldBlockBounds.MinXmm, moldBlockBounds.MaxXmm),
                Source: "MoldBoundsMidpoint"),

            new PartingPlaneCandidate(
                Axis: PartingAxis.Y,
                OffsetMm: Midpoint(moldBlockBounds.MinYmm, moldBlockBounds.MaxYmm),
                Source: "MoldBoundsMidpoint"),

            new PartingPlaneCandidate(
                Axis: PartingAxis.Z,
                OffsetMm: Midpoint(moldBlockBounds.MinZmm, moldBlockBounds.MaxZmm),
                Source: "MoldBoundsMidpoint")
        };
    }

    private PartingPlaneScore ScoreCandidate(
        PartingPlaneScoringInput input,
        PartingPlaneCandidate candidate)
    {
        var moldMinimum = GetAxisMinimum(input.MoldBlockBounds, candidate.Axis);
        var moldMaximum = GetAxisMaximum(input.MoldBlockBounds, candidate.Axis);
        var moldLength = moldMaximum - moldMinimum;

        var partMinimum = GetAxisMinimum(input.PartBounds, candidate.Axis);
        var partMaximum = GetAxisMaximum(input.PartBounds, candidate.Axis);

        var isInsideMoldBounds = candidate.OffsetMm > moldMinimum &&
            candidate.OffsetMm < moldMaximum;

        var isInsidePartBounds = candidate.OffsetMm >= partMinimum &&
            candidate.OffsetMm <= partMaximum;

        var reasons = new List<string>();

        if (!isInsideMoldBounds)
        {
            reasons.Add("outside-mold-bounds");
        }

        if (!isInsidePartBounds)
        {
            reasons.Add("outside-part-bounds");
        }

        if (moldLength <= 0m)
        {
            reasons.Add("invalid-mold-axis-length");

            return new PartingPlaneScore(
                Candidate: candidate,
                QualityScore: 0m,
                BalanceRatio: 0m,
                NormalizedPosition: 0m,
                IsInsideMoldBounds: false,
                IsInsidePartBounds: false,
                Severity: EngineeringSeverity.Fail,
                Reasons: reasons);
        }

        var normalizedPosition = Clamp(
            (candidate.OffsetMm - moldMinimum) / moldLength,
            0m,
            1m);

        if (!isInsideMoldBounds)
        {
            return new PartingPlaneScore(
                Candidate: candidate,
                QualityScore: 0m,
                BalanceRatio: 0m,
                NormalizedPosition: normalizedPosition,
                IsInsideMoldBounds: false,
                IsInsidePartBounds: isInsidePartBounds,
                Severity: EngineeringSeverity.Fail,
                Reasons: reasons);
        }

        var separation = separationEngine.Split(new MoldSeparationEngineInput(
            MoldBlockBounds: input.MoldBlockBounds,
            PartBounds: input.PartBounds,
            PartingAxis: candidate.Axis,
            PartingOffsetMm: candidate.OffsetMm,
            VoxelResolutionMm: input.VoxelResolutionMm,
            HasPartingMetadata: true,
            HasShutoffStrategy: input.HasShutoffStrategy));

        var centerDistancePenalty = Math.Abs(normalizedPosition - 0.5m) * 0.35m;
        var partBoundsPenalty = isInsidePartBounds ? 0m : 0.20m;
        var shutoffPenalty = input.HasShutoffStrategy ? 0m : 0.10m;

        var qualityScore = Clamp(
            separation.Summary.QualityScore -
            centerDistancePenalty -
            partBoundsPenalty -
            shutoffPenalty,
            0m,
            1m);

        var severity = qualityScore switch
        {
            >= 0.85m => EngineeringSeverity.Pass,
            >= 0.65m => EngineeringSeverity.Warning,
            _ => EngineeringSeverity.NeedsEngineerReview
        };

        if (separation.ValidationResult.HasFailures)
        {
            severity = EngineeringSeverity.Fail;
            reasons.Add("separation-validation-failed");
        }

        if (separation.ValidationResult.RequiresEngineerReview)
        {
            reasons.Add("requires-engineer-review");
        }

        if (qualityScore >= 0.85m)
        {
            reasons.Add("quality-pass");
        }
        else if (qualityScore >= 0.65m)
        {
            reasons.Add("quality-warning");
        }
        else
        {
            reasons.Add("quality-review");
        }

        return new PartingPlaneScore(
            Candidate: candidate,
            QualityScore: qualityScore,
            BalanceRatio: separation.Summary.BalanceRatio,
            NormalizedPosition: normalizedPosition,
            IsInsideMoldBounds: isInsideMoldBounds,
            IsInsidePartBounds: isInsidePartBounds,
            Severity: severity,
            Reasons: reasons);
    }

    private static decimal Midpoint(decimal minimum, decimal maximum)
    {
        return minimum + ((maximum - minimum) / 2m);
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