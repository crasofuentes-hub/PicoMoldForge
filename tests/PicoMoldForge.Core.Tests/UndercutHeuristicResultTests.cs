using PicoMoldForge.Core.Analysis;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class UndercutHeuristicResultTests
{
    [Fact]
    public void UndercutHeuristicResult_CanRepresentPreliminaryRisk()
    {
        var result = new UndercutHeuristicResult(
            OpeningDirection3.PositiveZ,
            TotalTriangleCount: 12,
            OpposingNormalTriangleCount: 2,
            OpposingNormalRatio: 2.0f / 12.0f,
            HasPotentialUndercutRisk: true,
            Method: "Triangle normal opposition heuristic",
            Limitation: "Preliminary only.");

        Assert.True(result.HasPotentialUndercutRisk);
        Assert.Equal(2, result.OpposingNormalTriangleCount);
        Assert.False(result.OpeningDirection.IsZero);
    }
}