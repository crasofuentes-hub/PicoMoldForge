namespace PicoMoldForge.Core.Exports;

public sealed record BaselineStatus(
    bool IsPassing,
    int TotalTests,
    string Summary)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (TotalTests < 0)
        {
            errors.Add("TotalTests cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            errors.Add("Baseline summary is required.");
        }

        return errors;
    }
}