using PicoMoldForge.Core.Engineering;
using PicoMoldForge.Core.Engineering.Separation;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering.Separation;

public sealed class PartingPlaneScorerTests
{
    [Fact]
    public void GenerateDefaultCandidates_ReturnsSixCandidates()
    {
        var scorer = new PartingPlaneScorer();

        var candidates = scorer.GenerateDefaultCandidates(
            new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            new MoldSeparationBounds(20m, 30m, 40m, 80m, 90m, 100m));

        Assert.Equal(6, candidates.Count);
        Assert.Contains(candidates, candidate => candidate.Axis == PartingAxis.X && candidate.OffsetMm == 50m);
        Assert.Contains(candidates, candidate => candidate.Axis == PartingAxis.Y && candidate.OffsetMm == 60m);
        Assert.Contains(candidates, candidate => candidate.Axis == PartingAxis.Z && candidate.OffsetMm == 70m);
    }

    [Fact]
    public void Score_WithCenteredCandidate_ReturnsPass()
    {
        var scorer = new PartingPlaneScorer();

        var result = scorer.Score(new PartingPlaneScoringInput(
            MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
            VoxelResolutionMm: 1m,
            Candidates: new[]
            {
                new PartingPlaneCandidate(PartingAxis.Z, 50m)
            },
            HasShutoffStrategy: true));

        Assert.Equal(EngineeringSeverity.Pass, result.BestScore.Severity);
        Assert.True(result.BestScore.QualityScore >= 0.85m);
        Assert.True(result.BestScore.IsInsideMoldBounds);
        Assert.True(result.BestScore.IsInsidePartBounds);
    }

    [Fact]
    public void Score_WithOutsideMoldCandidate_ReturnsFail()
    {
        var scorer = new PartingPlaneScorer();

        var result = scorer.Score(new PartingPlaneScoringInput(
            MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
            VoxelResolutionMm: 1m,
            Candidates: new[]
            {
                new PartingPlaneCandidate(PartingAxis.X, 150m)
            },
            HasShutoffStrategy: true));

        Assert.Equal(EngineeringSeverity.Fail, result.BestScore.Severity);
        Assert.Equal(0m, result.BestScore.QualityScore);
        Assert.Contains("outside-mold-bounds", result.BestScore.Reasons);
    }

    [Fact]
    public void Score_RanksCenteredCandidateAboveEdgeCandidate()
    {
        var scorer = new PartingPlaneScorer();

        var result = scorer.Score(new PartingPlaneScoringInput(
            MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
            VoxelResolutionMm: 1m,
            Candidates: new[]
            {
                new PartingPlaneCandidate(PartingAxis.Z, 10m, "EdgeCandidate"),
                new PartingPlaneCandidate(PartingAxis.Z, 50m, "CenteredCandidate")
            },
            HasShutoffStrategy: true));

        Assert.Equal("CenteredCandidate", result.BestScore.Candidate.Source);
        Assert.True(result.Scores[0].QualityScore > result.Scores[1].QualityScore);
    }

    [Fact]
    public void Score_WithMissingShutoffStrategy_LowersQualityAndRequiresReviewReason()
    {
        var scorer = new PartingPlaneScorer();

        var result = scorer.Score(new PartingPlaneScoringInput(
            MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
            PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
            VoxelResolutionMm: 1m,
            Candidates: new[]
            {
                new PartingPlaneCandidate(PartingAxis.Z, 50m)
            },
            HasShutoffStrategy: false));

        Assert.True(result.BestScore.QualityScore < 0.95m);
        Assert.Contains("requires-engineer-review", result.BestScore.Reasons);
    }

    [Fact]
    public void Score_WithNoCandidates_Throws()
    {
        var scorer = new PartingPlaneScorer();

        Assert.Throws<ArgumentException>(() =>
            scorer.Score(new PartingPlaneScoringInput(
                MoldBlockBounds: new MoldSeparationBounds(0m, 0m, 0m, 100m, 100m, 100m),
                PartBounds: new MoldSeparationBounds(20m, 20m, 20m, 80m, 80m, 80m),
                VoxelResolutionMm: 1m,
                Candidates: Array.Empty<PartingPlaneCandidate>(),
                HasShutoffStrategy: true)));
    }
}