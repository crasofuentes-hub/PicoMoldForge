using PicoMoldForge.Core.Engineering.MoldAccess;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;
using PicoMoldForge.Core.Tests.Engineering.WallThickness;

namespace PicoMoldForge.Core.Tests.Engineering.MoldAccess;

public sealed class PartingPlaneSearchTests
{
    [Fact]
    public void Box_IsSplitOnItsLargestFace_AcrossItsThinSide()
    {
        var result = PartingPlaneSearch.Search(MeshWallThicknessSamplerTests.GeneratorTestBox());

        // Nothing is trapped on any axis; across Z only the 300 mm2 of side walls lack draft (X: 600, Y: 500), and
        // the bottom face (200 mm2) becomes the parting surface.
        Assert.Equal(PartingAxis.Z, result.Best.Axis);
        Assert.Equal(0.0, result.Best.OffsetMm, 9);
        Assert.Equal("Flat face level", result.Best.Source);
        Assert.Equal(0.0, result.Best.TrappedAreaMm2, 9);
        Assert.Equal(300.0, result.Best.UndraftedAreaMm2, 9);
        Assert.Equal(200.0, result.Best.FlatAreaInPlaneMm2, 9);
        Assert.Equal(new[] { 600.0, 500.0, 300.0 }, result.BestPerAxis.Select(c => Math.Round(c.UndraftedAreaMm2, 9)));
    }

    [Fact]
    public void TShape_IsSplitOnItsTopFace_WhereNothingIsTrapped()
    {
        var result = PartingPlaneSearch.Search(MeshMoldAccessAnalyzerTests.TShape());

        Assert.Equal(PartingAxis.Z, result.Best.Axis);
        Assert.Equal(15.0, result.Best.OffsetMm, 9);
        Assert.Equal(0.0, result.Best.TrappedAreaMm2, 9);
        Assert.Equal(300.0, result.Best.FlatAreaInPlaneMm2, 9);
    }

    [Fact]
    public void Reference_ScoresLikeTheDraftAndUndercutSummaries()
    {
        var t = MeshMoldAccessAnalyzerTests.TShape();

        var result = PartingPlaneSearch.Search(t, reference: (PartingAxis.Z, 5.0));
        var analysis = MeshMoldAccessAnalysis.Analyze(t, PartingAxis.Z, 5m, minimumDraftDeg: 1.0m);

        Assert.Equal((double)analysis.Undercut.RiskySurfaceAreaMm2, result.Reference!.TrappedAreaMm2, 4);
        Assert.Equal((double)analysis.Draft.Summary.RiskySurfaceAreaMm2, result.Reference.UndraftedAreaMm2, 4);
        Assert.Equal(300.0, result.Reference.TrappedAreaMm2, 4);
    }

    [Fact]
    public void HoleAcrossTheLongestSide_TurnsTheSplitAlongTheHole()
    {
        // The bounding-box rule splits the 20 x 20 x 10 frame across X, which traps the 320 mm2 of hole walls.
        var frame = MeshMoldAccessAnalyzerTests.SquareFrame();

        var result = PartingPlaneSearch.Search(frame, reference: (PartingAxis.X, 10.0));

        Assert.Equal(320.0, result.Reference!.TrappedAreaMm2, 6);
        Assert.Equal(PartingAxis.Z, result.Best.Axis);
        Assert.Equal(0.0, result.Best.TrappedAreaMm2, 6);
    }

    [Fact]
    public void InternalVoid_StaysTrappedWhateverTheSplit()
    {
        var hollow = MeshWallThicknessSamplerTests.Box(0, 0, 0, 20, 20, 20)
            .Concat(MeshWallThicknessSamplerTests.Box(5, 5, 5, 10, 10, 10).Select(t => new MeshTriangle(t.A, t.C, t.B)))
            .ToArray();

        var result = PartingPlaneSearch.Search(hollow);

        Assert.Equal(600.0, result.Best.TrappedAreaMm2, 6);
        Assert.All(result.BestPerAxis, c => Assert.Equal(600.0, c.TrappedAreaMm2, 6));
    }

    [Fact]
    public void InsideOutMesh_AndThreadCount_DoNotChangeTheResult()
    {
        var t = MeshMoldAccessAnalyzerTests.TShape();
        var reversed = t.Select(x => new MeshTriangle(x.A, x.C, x.B)).ToArray();

        var one = PartingPlaneSearch.Search(t, new PartingPlaneSearchOptions(MaxDegreeOfParallelism: 1));
        var four = PartingPlaneSearch.Search(reversed, new PartingPlaneSearchOptions(MaxDegreeOfParallelism: 4));

        Assert.Equal(one.Best, four.Best);
        Assert.Equal(one.BestPerAxis, four.BestPerAxis);
    }
}
