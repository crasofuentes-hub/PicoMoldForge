namespace PicoMoldForge.Core.Engineering;

public sealed record EngineeringRuleResult(
    string RulePackVersion,
    string Category,
    IReadOnlyList<EngineeringIssue> Issues)
{
    public int PassCount => Issues.Count(issue => issue.Severity == EngineeringSeverity.Pass);

    public int InfoCount => Issues.Count(issue => issue.Severity == EngineeringSeverity.Info);

    public int WarningCount => Issues.Count(issue => issue.Severity == EngineeringSeverity.Warning);

    public int FailureCount => Issues.Count(issue => issue.Severity == EngineeringSeverity.Fail);

    public int EngineerReviewCount => Issues.Count(issue => issue.Severity == EngineeringSeverity.NeedsEngineerReview);

    public bool HasFailures => FailureCount > 0;

    public bool RequiresEngineerReview => Issues.Any(issue =>
        issue.RequiresEngineerReview ||
        issue.Severity == EngineeringSeverity.NeedsEngineerReview);

    public EngineeringSeverity HighestSeverity => Issues.Count == 0
        ? EngineeringSeverity.Pass
        : Issues.Max(issue => issue.Severity);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(RulePackVersion))
        {
            errors.Add("EngineeringRuleResult.RulePackVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(Category))
        {
            errors.Add("EngineeringRuleResult.Category is required.");
        }

        foreach (var issue in Issues)
        {
            foreach (var issueError in issue.Validate())
            {
                errors.Add(issueError);
            }
        }

        return errors;
    }

    public static EngineeringRuleResult Empty(string rulePackVersion, string category)
    {
        return new EngineeringRuleResult(
            rulePackVersion,
            category,
            Array.Empty<EngineeringIssue>());
    }
}