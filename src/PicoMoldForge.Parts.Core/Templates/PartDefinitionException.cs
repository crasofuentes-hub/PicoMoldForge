namespace PicoMoldForge.Parts.Templates;

/// <summary>Raised when part parameters are invalid; carries every problem found (not only the first).</summary>
public sealed class PartDefinitionException : Exception
{
    public PartDefinitionException(IReadOnlyList<string> errors)
        : base("Invalid part definition: " + string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
