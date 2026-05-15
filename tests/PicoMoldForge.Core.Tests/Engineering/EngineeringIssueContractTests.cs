using PicoMoldForge.Core.Engineering;
using Xunit;

namespace PicoMoldForge.Core.Tests.Engineering;

public sealed class EngineeringIssueContractTests
{
    [Fact]
    public void Validate_WithCompleteIssue_ReturnsNoErrors()
    {
        var issue = new EngineeringIssue(
            RuleId: "draft.wall.abs.smooth.minimum",
            Severity: EngineeringSeverity.Warning,
            Category: "Draft",
            Message: "ABS smooth wall draft is below the expert minimum.",
            FeatureType: "Wall",
            Material: "ABS",
            ActualValue: 0.75m,
            RequiredValue: 1.0m,
            RecommendedValue: 1.5m,
            Unit: "deg",
            CorrectiveAction: "Increase draft angle or document an engineer-approved override.",
            RequiresEngineerReview: false,
            SourceRulePackVersion: "expert-injection-mold-rules.v1");

        Assert.Empty(issue.Validate());
    }

    [Fact]
    public void Validate_WithMissingRequiredFields_ReturnsErrors()
    {
        var issue = new EngineeringIssue(
            RuleId: "",
            Severity: EngineeringSeverity.Warning,
            Category: "",
            Message: "",
            FeatureType: null,
            Material: null,
            ActualValue: null,
            RequiredValue: null,
            RecommendedValue: null,
            Unit: null,
            CorrectiveAction: "",
            RequiresEngineerReview: false,
            SourceRulePackVersion: "");

        var errors = issue.Validate();

        Assert.Contains("EngineeringIssue.RuleId is required.", errors);
        Assert.Contains("EngineeringIssue.Category is required.", errors);
        Assert.Contains("EngineeringIssue.Message is required.", errors);
        Assert.Contains("EngineeringIssue.CorrectiveAction is required.", errors);
        Assert.Contains("EngineeringIssue.SourceRulePackVersion is required.", errors);
    }

    [Fact]
    public void Validate_WithActualValueAndMissingUnit_ReturnsUnitError()
    {
        var issue = new EngineeringIssue(
            RuleId: "draft.wall.abs.smooth.minimum",
            Severity: EngineeringSeverity.Warning,
            Category: "Draft",
            Message: "ABS smooth wall draft is below the expert minimum.",
            FeatureType: "Wall",
            Material: "ABS",
            ActualValue: 0.75m,
            RequiredValue: 1.0m,
            RecommendedValue: 1.5m,
            Unit: null,
            CorrectiveAction: "Increase draft angle.",
            RequiresEngineerReview: false,
            SourceRulePackVersion: "expert-injection-mold-rules.v1");

        var errors = issue.Validate();

        Assert.Contains("EngineeringIssue.Unit is required when ActualValue is provided.", errors);
        Assert.Contains("EngineeringIssue.Unit is required when RequiredValue is provided.", errors);
        Assert.Contains("EngineeringIssue.Unit is required when RecommendedValue is provided.", errors);
    }

    [Fact]
    public void Validate_WithNeedsEngineerReviewSeverityAndFalseReviewFlag_ReturnsError()
    {
        var issue = new EngineeringIssue(
            RuleId: "texture.heavy.manual.review",
            Severity: EngineeringSeverity.NeedsEngineerReview,
            Category: "Draft",
            Message: "Heavy texture above 0.10 mm requires manual review.",
            FeatureType: "Wall",
            Material: "PC",
            ActualValue: 0.12m,
            RequiredValue: null,
            RecommendedValue: null,
            Unit: "mm",
            CorrectiveAction: "Request engineer review for texture-specific draft.",
            RequiresEngineerReview: false,
            SourceRulePackVersion: "expert-injection-mold-rules.v1");

        var errors = issue.Validate();

        Assert.Contains("EngineeringIssue.RequiresEngineerReview must be true when Severity is NeedsEngineerReview.", errors);
    }
}

public sealed class EngineeringRuleResultContractTests
{
    [Fact]
    public void Empty_ReturnsPassHighestSeverityAndNoFailures()
    {
        var result = EngineeringRuleResult.Empty(
            "expert-injection-mold-rules.v1",
            "Draft");

        Assert.Equal(EngineeringSeverity.Pass, result.HighestSeverity);
        Assert.False(result.HasFailures);
        Assert.False(result.RequiresEngineerReview);
        Assert.Equal(0, result.WarningCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(result.Validate());
    }

    [Fact]
    public void RuleResult_ComputesCountsAndHighestSeverity()
    {
        var issues = new[]
        {
            EngineeringIssueFactory.Pass(
                ruleId: "draft.abs.smooth.pass",
                category: "Draft",
                message: "ABS smooth wall draft passes.",
                correctiveAction: "No action required.",
                sourceRulePackVersion: "expert-injection-mold-rules.v1",
                featureType: "Wall",
                material: "ABS",
                actualValue: 2.0m,
                requiredValue: 1.0m,
                recommendedValue: 1.5m,
                unit: "deg"),

            EngineeringIssueFactory.Warning(
                ruleId: "draft.abs.smooth.warning",
                category: "Draft",
                message: "ABS smooth wall draft is below recommended value.",
                correctiveAction: "Increase draft if cosmetic or deep.",
                sourceRulePackVersion: "expert-injection-mold-rules.v1",
                featureType: "Wall",
                material: "ABS",
                actualValue: 0.75m,
                requiredValue: 1.0m,
                recommendedValue: 1.5m,
                unit: "deg"),

            EngineeringIssueFactory.Fail(
                ruleId: "draft.shutoff.fail",
                category: "Draft",
                message: "Shutoff draft is below fail threshold.",
                correctiveAction: "Increase shutoff draft to at least 3 degrees.",
                sourceRulePackVersion: "expert-injection-mold-rules.v1",
                featureType: "Shutoff",
                material: "Any",
                actualValue: 0.0m,
                requiredValue: 3.0m,
                recommendedValue: 4.0m,
                unit: "deg")
        };

        var result = new EngineeringRuleResult(
            RulePackVersion: "expert-injection-mold-rules.v1",
            Category: "Draft",
            Issues: issues);

        Assert.Equal(1, result.PassCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(1, result.FailureCount);
        Assert.True(result.HasFailures);
        Assert.True(result.RequiresEngineerReview);
        Assert.Equal(EngineeringSeverity.Fail, result.HighestSeverity);
        Assert.Empty(result.Validate());
    }

    [Fact]
    public void RuleResult_WithNeedsEngineerReview_ComputesReviewFlag()
    {
        var result = new EngineeringRuleResult(
            RulePackVersion: "expert-injection-mold-rules.v1",
            Category: "Draft",
            Issues: new[]
            {
                EngineeringIssueFactory.NeedsEngineerReview(
                    ruleId: "texture.heavy.review",
                    category: "Draft",
                    message: "Heavy texture above 0.10 mm requires review.",
                    correctiveAction: "Request engineer review.",
                    sourceRulePackVersion: "expert-injection-mold-rules.v1",
                    featureType: "Wall",
                    material: "PC",
                    actualValue: 0.12m,
                    unit: "mm")
            });

        Assert.False(result.HasFailures);
        Assert.True(result.RequiresEngineerReview);
        Assert.Equal(1, result.EngineerReviewCount);
        Assert.Equal(EngineeringSeverity.NeedsEngineerReview, result.HighestSeverity);
        Assert.Empty(result.Validate());
    }
}