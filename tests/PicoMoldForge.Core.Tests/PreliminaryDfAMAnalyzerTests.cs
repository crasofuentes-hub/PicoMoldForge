using PicoMoldForge.Core.DfAM;
using Xunit;

namespace PicoMoldForge.Core.Tests;

public sealed class PreliminaryDfAMAnalyzerTests
{
    [Fact]
    public void DfAMInputSnapshot_WithValidValues_ReturnsNoValidationErrors()
    {
        var snapshot = CreatePassingSnapshot();

        var errors = snapshot.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void DfAMInputSnapshot_WithInvalidRecommendedWallThickness_ReturnsValidationError()
    {
        var snapshot = CreatePassingSnapshot() with
        {
            RecommendedMinimumWallThicknessMm = 0
        };

        var errors = snapshot.Validate();

        Assert.Contains(errors, error => error.Contains("RecommendedMinimumWallThicknessMm", StringComparison.Ordinal));
    }

    [Fact]
    public void DfAMInputSnapshot_WithInvalidCoolingDiameter_ReturnsValidationError()
    {
        var snapshot = CreatePassingSnapshot() with
        {
            CoolingChannelDiameterMm = 0
        };

        var errors = snapshot.Validate();

        Assert.Contains(errors, error => error.Contains("CoolingChannelDiameterMm", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_WithPassingSnapshot_ReturnsSuccessfulReport()
    {
        var analyzer = new PreliminaryDfAMAnalyzer();

        var report = analyzer.Analyze(CreatePassingSnapshot());

        Assert.True(report.IsSuccessful);
        Assert.Equal(5, report.Checks.Count);
        Assert.All(report.Checks, check => Assert.True(check.IsPassed));
        Assert.Contains(report.Checks, check => check.Rule.Code == "MINIMUM_WALL_THICKNESS_PRELIMINARY");
        Assert.Contains(report.Checks, check => check.Rule.Code == "COOLING_CLEARANCE_SANITY");
        Assert.Contains(report.Checks, check => check.Rule.Code == "LATTICE_BEAM_RADIUS_SANITY");
        Assert.Contains(report.Checks, check => check.Rule.Code == "EJECTOR_PIN_DIAMETER_SANITY");
        Assert.Contains(report.Checks, check => check.Rule.Code == "NON_CERTIFICATION_NOTICE");
        Assert.Contains(report.Warnings, warning => warning.Contains("not manufacturability certification", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_WithThinWallSnapshot_ReturnsWarningFailureButSuccessfulReport()
    {
        var analyzer = new PreliminaryDfAMAnalyzer();

        var report = analyzer.Analyze(CreatePassingSnapshot() with
        {
            MinimumWallThicknessMm = 0.7m,
            RecommendedMinimumWallThicknessMm = 1.2m
        });

        var wallCheck = Assert.Single(report.Checks, check => check.Rule.Code == "MINIMUM_WALL_THICKNESS_PRELIMINARY");

        Assert.True(report.IsSuccessful);
        Assert.False(wallCheck.IsPassed);
        Assert.Equal(DfAMRuleSeverity.Warning, wallCheck.Rule.Severity);
        Assert.Contains(report.Warnings, warning => warning.Contains("MINIMUM_WALL_THICKNESS_PRELIMINARY", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_WithInsufficientCoolingClearance_ReturnsCoolingWarning()
    {
        var analyzer = new PreliminaryDfAMAnalyzer();

        var report = analyzer.Analyze(CreatePassingSnapshot() with
        {
            CoolingMinimumClearanceMm = 3.0m,
            CoolingChannelDiameterMm = 6.0m
        });

        var coolingCheck = Assert.Single(report.Checks, check => check.Rule.Code == "COOLING_CLEARANCE_SANITY");

        Assert.False(coolingCheck.IsPassed);
        Assert.Contains("Cooling clearance", coolingCheck.Message);
    }

    [Fact]
    public void Analyze_WithLargeLatticeBeamRadius_ReturnsLatticeWarning()
    {
        var analyzer = new PreliminaryDfAMAnalyzer();

        var report = analyzer.Analyze(CreatePassingSnapshot() with
        {
            LatticeBeamRadiusMm = 5.0m,
            LatticeCellSizeMm = 10.0m
        });

        var latticeCheck = Assert.Single(report.Checks, check => check.Rule.Code == "LATTICE_BEAM_RADIUS_SANITY");

        Assert.False(latticeCheck.IsPassed);
        Assert.Contains("must be less than", latticeCheck.Message);
    }

    [Fact]
    public void Analyze_WithPreliminaryGeometry_AddsPreliminaryGeometryWarning()
    {
        var analyzer = new PreliminaryDfAMAnalyzer();

        var report = analyzer.Analyze(CreatePassingSnapshot() with
        {
            UsesPreliminaryGeometry = true
        });

        Assert.Contains(report.Warnings, warning => warning.Contains("preliminary diagnostic geometry", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Checks, check => check.Rule.Code == "NON_CERTIFICATION_NOTICE");
    }

    [Fact]
    public void Analyze_WithInvalidSnapshot_Throws()
    {
        var analyzer = new PreliminaryDfAMAnalyzer();

        var exception = Assert.Throws<ArgumentException>(() =>
            analyzer.Analyze(CreatePassingSnapshot() with
            {
                LatticeCellSizeMm = 0
            }));

        Assert.Contains("Invalid DfAM input snapshot", exception.Message);
    }

    private static DfAMInputSnapshot CreatePassingSnapshot()
    {
        return new DfAMInputSnapshot(
            MinimumWallThicknessMm: 1.5m,
            RecommendedMinimumWallThicknessMm: 1.2m,
            CoolingMinimumClearanceMm: 5.0m,
            CoolingChannelDiameterMm: 6.0m,
            LatticeBeamRadiusMm: 1.0m,
            LatticeCellSizeMm: 10.0m,
            EjectorPinDiameterMm: 4.0m,
            UsesPreliminaryGeometry: true);
    }
}