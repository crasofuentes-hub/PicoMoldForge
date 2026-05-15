namespace PicoMoldForge.Core.Engineering;

public static class EngineeringIssueFactory
{
    public static EngineeringIssue Pass(
        string ruleId,
        string category,
        string message,
        string correctiveAction,
        string sourceRulePackVersion,
        string? featureType = null,
        string? material = null,
        decimal? actualValue = null,
        decimal? requiredValue = null,
        decimal? recommendedValue = null,
        string? unit = null)
    {
        return Create(
            ruleId,
            EngineeringSeverity.Pass,
            category,
            message,
            correctiveAction,
            sourceRulePackVersion,
            requiresEngineerReview: false,
            featureType,
            material,
            actualValue,
            requiredValue,
            recommendedValue,
            unit);
    }

    public static EngineeringIssue Warning(
        string ruleId,
        string category,
        string message,
        string correctiveAction,
        string sourceRulePackVersion,
        string? featureType = null,
        string? material = null,
        decimal? actualValue = null,
        decimal? requiredValue = null,
        decimal? recommendedValue = null,
        string? unit = null,
        bool requiresEngineerReview = false)
    {
        return Create(
            ruleId,
            EngineeringSeverity.Warning,
            category,
            message,
            correctiveAction,
            sourceRulePackVersion,
            requiresEngineerReview,
            featureType,
            material,
            actualValue,
            requiredValue,
            recommendedValue,
            unit);
    }

    public static EngineeringIssue Fail(
        string ruleId,
        string category,
        string message,
        string correctiveAction,
        string sourceRulePackVersion,
        string? featureType = null,
        string? material = null,
        decimal? actualValue = null,
        decimal? requiredValue = null,
        decimal? recommendedValue = null,
        string? unit = null,
        bool requiresEngineerReview = true)
    {
        return Create(
            ruleId,
            EngineeringSeverity.Fail,
            category,
            message,
            correctiveAction,
            sourceRulePackVersion,
            requiresEngineerReview,
            featureType,
            material,
            actualValue,
            requiredValue,
            recommendedValue,
            unit);
    }

    public static EngineeringIssue NeedsEngineerReview(
        string ruleId,
        string category,
        string message,
        string correctiveAction,
        string sourceRulePackVersion,
        string? featureType = null,
        string? material = null,
        decimal? actualValue = null,
        decimal? requiredValue = null,
        decimal? recommendedValue = null,
        string? unit = null)
    {
        return Create(
            ruleId,
            EngineeringSeverity.NeedsEngineerReview,
            category,
            message,
            correctiveAction,
            sourceRulePackVersion,
            requiresEngineerReview: true,
            featureType,
            material,
            actualValue,
            requiredValue,
            recommendedValue,
            unit);
    }

    private static EngineeringIssue Create(
        string ruleId,
        EngineeringSeverity severity,
        string category,
        string message,
        string correctiveAction,
        string sourceRulePackVersion,
        bool requiresEngineerReview,
        string? featureType,
        string? material,
        decimal? actualValue,
        decimal? requiredValue,
        decimal? recommendedValue,
        string? unit)
    {
        return new EngineeringIssue(
            RuleId: ruleId,
            Severity: severity,
            Category: category,
            Message: message,
            FeatureType: featureType,
            Material: material,
            ActualValue: actualValue,
            RequiredValue: requiredValue,
            RecommendedValue: recommendedValue,
            Unit: unit,
            CorrectiveAction: correctiveAction,
            RequiresEngineerReview: requiresEngineerReview,
            SourceRulePackVersion: sourceRulePackVersion);
    }
}