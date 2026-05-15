namespace PicoMoldForge.Core.Engineering;

public sealed record EngineeringIssue(
    string RuleId,
    EngineeringSeverity Severity,
    string Category,
    string Message,
    string? FeatureType,
    string? Material,
    decimal? ActualValue,
    decimal? RequiredValue,
    decimal? RecommendedValue,
    string? Unit,
    string CorrectiveAction,
    bool RequiresEngineerReview,
    string SourceRulePackVersion)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(RuleId))
        {
            errors.Add("EngineeringIssue.RuleId is required.");
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            errors.Add("EngineeringIssue.Category is required.");
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            errors.Add("EngineeringIssue.Message is required.");
        }

        if (string.IsNullOrWhiteSpace(CorrectiveAction))
        {
            errors.Add("EngineeringIssue.CorrectiveAction is required.");
        }

        if (string.IsNullOrWhiteSpace(SourceRulePackVersion))
        {
            errors.Add("EngineeringIssue.SourceRulePackVersion is required.");
        }

        if (ActualValue is not null &&
            string.IsNullOrWhiteSpace(Unit))
        {
            errors.Add("EngineeringIssue.Unit is required when ActualValue is provided.");
        }

        if (RequiredValue is not null &&
            string.IsNullOrWhiteSpace(Unit))
        {
            errors.Add("EngineeringIssue.Unit is required when RequiredValue is provided.");
        }

        if (RecommendedValue is not null &&
            string.IsNullOrWhiteSpace(Unit))
        {
            errors.Add("EngineeringIssue.Unit is required when RecommendedValue is provided.");
        }

        if (Severity == EngineeringSeverity.NeedsEngineerReview && !RequiresEngineerReview)
        {
            errors.Add("EngineeringIssue.RequiresEngineerReview must be true when Severity is NeedsEngineerReview.");
        }

        return errors;
    }
}